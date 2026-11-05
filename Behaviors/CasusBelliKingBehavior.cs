using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli
{
    public class KingCaptureRecord
    {
        public string KingHeroId = "";
        public string KingKingdomId = "";
        public string CaptorKingdomId = "";
        public string CaptorHeroId = "";
        public double CapturedOnDay = 0.0;
        public bool IsResolved = false;
        public string Resolution = "";
    }

    // Para ADICIONAR uma oferta nova: veja os 5 passos marcados com
    // "[NOVA OFERTA]" neste arquivo, no menu e no KingDebugCommands.
    public enum NegotiationOfferType
    {
        Objective = 0,
        RansomModerate = 1,
        RansomHeavy = 2,
        Mercy = 3
    }

    public class NegotiationOffer
    {
        public NegotiationOfferType Type = NegotiationOfferType.Objective;
        public string Title = "";
        public string Detail = "";
        public bool IsEnabled = false;
        public string DisabledReason = "";
        public int Amount = 0;
        public bool WillAccept = true;
    }

    // Captura do soberano inimigo: abre uma negociacao em que o SOBERANO
    // DO REINO CAPTOR dita os termos (mesmo que a captura tenha sido feita
    // por um vassalo), protege o rei contra fuga durante a negociacao e
    // registra rancor.
    //
    // Se o soberano captor e o jogador, ele decide pelo menu. Se for a IA,
    // ela decide sozinha, com as mesmas ofertas do jogador.
    // Isolado de proposito: para desligar, remova o AddBehavior no SubModule.
    public class CasusBelliKingBehavior : CampaignBehaviorBase
    {
        // ---------------- configuracao ----------------
        private static readonly double ProtectionDays = 30.0;
        private static readonly double AiNegotiationDelayDays = 2.0;
        private static readonly double GrievanceWindowDays = 365.0;

        private static readonly float ModerateRansomMultiple = 1.0f;
        private static readonly float HeavyRansomMultiple = 2.5f;

        // Multiplo do resgate-base que o reino do rei aceita pagar,
        // por ponto de RansomPayFactor do perfil.
        private static readonly float AcceptableMultiplePerPayFactor = 1.8f;

        private static readonly int RansomMinimum = 5000;
        private static readonly int RansomPerClanTier = 4000;

        // Parte do resgate que fica com quem capturou o rei, quando ele
        // nao e o proprio soberano (0 = tudo para o soberano, 1 = tudo
        // para o capturador).
        private static readonly float CaptorRansomShare = 0.5f;

        private const int FieldsPerKingCapture = 7;

        private List<KingCaptureRecord> _captures =
            new List<KingCaptureRecord>();
        private List<string> _savedCaptureData = new List<string>();

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(
                this,
                OnHourlyTick);

            CampaignEvents.DailyTickEvent.AddNonSerializedListener(
                this,
                OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                if (_savedCaptureData == null)
                {
                    _savedCaptureData = new List<string>();
                }

                _savedCaptureData.Clear();

                foreach (KingCaptureRecord record in _captures)
                {
                    _savedCaptureData.Add(record.KingHeroId ?? "");
                    _savedCaptureData.Add(record.KingKingdomId ?? "");
                    _savedCaptureData.Add(record.CaptorKingdomId ?? "");
                    _savedCaptureData.Add(record.CaptorHeroId ?? "");
                    _savedCaptureData.Add(
                        record.CapturedOnDay.ToString(
                            "R",
                            CultureInfo.InvariantCulture));
                    _savedCaptureData.Add(record.IsResolved.ToString());
                    _savedCaptureData.Add(record.Resolution ?? "");
                }
            }

            dataStore.SyncData(
                "CasusBelli_KingCaptureData_v1",
                ref _savedCaptureData);

            if (dataStore.IsLoading)
            {
                _captures.Clear();

                if (_savedCaptureData == null)
                {
                    _savedCaptureData = new List<string>();
                }

                for (int i = 0;
                     i + FieldsPerKingCapture <= _savedCaptureData.Count;
                     i += FieldsPerKingCapture)
                {
                    double capturedOnDay;
                    bool isResolved;

                    if (!double.TryParse(
                            _savedCaptureData[i + 4],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out capturedOnDay) ||
                        !bool.TryParse(_savedCaptureData[i + 5], out isResolved))
                    {
                        continue;
                    }

                    KingCaptureRecord record = new KingCaptureRecord();
                    record.KingHeroId = _savedCaptureData[i];
                    record.KingKingdomId = _savedCaptureData[i + 1];
                    record.CaptorKingdomId = _savedCaptureData[i + 2];
                    record.CaptorHeroId = _savedCaptureData[i + 3];
                    record.CapturedOnDay = capturedOnDay;
                    record.IsResolved = isResolved;
                    record.Resolution = _savedCaptureData[i + 6];

                    _captures.Add(record);
                }
            }
        }

        // ------------------------------------------------------------
        // AJUDANTES
        // ------------------------------------------------------------
        private static CasusBelliCampaignBehavior GetMain()
        {
            if (Campaign.Current == null)
            {
                return null;
            }

            return Campaign.Current.GetCampaignBehavior<
                CasusBelliCampaignBehavior>();
        }

        private static Hero FindHero(string heroId)
        {
            if (string.IsNullOrEmpty(heroId))
            {
                return null;
            }

            return Hero.FindFirst(h => h.StringId == heroId);
        }

        private static Kingdom GetKingdom(string kingdomId)
        {
            if (string.IsNullOrEmpty(kingdomId))
            {
                return null;
            }

            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom != null && kingdom.StringId == kingdomId)
                {
                    return kingdom;
                }
            }

            return null;
        }

        private static bool IsPlayerKingdom(Kingdom kingdom)
        {
            return kingdom != null &&
                Clan.PlayerClan != null &&
                Clan.PlayerClan.Kingdom == kingdom;
        }

        // O jogador e o SOBERANO deste reino?
        private static bool IsPlayerSovereignOf(Kingdom kingdom)
        {
            return kingdom != null &&
                kingdom.Leader != null &&
                Hero.MainHero != null &&
                kingdom.Leader == Hero.MainHero;
        }

        private static void ShowMessage(string text)
        {
            InformationManager.DisplayMessage(new InformationMessage(text));
        }

        private KingCaptureRecord FindOpenRecord(Hero king)
        {
            foreach (KingCaptureRecord record in _captures)
            {
                if (!record.IsResolved && record.KingHeroId == king.StringId)
                {
                    return record;
                }
            }

            return null;
        }

        public IEnumerable<KingCaptureRecord> AllCaptures
        {
            get { return _captures; }
        }

        private void Resolve(KingCaptureRecord record, string resolution)
        {
            record.IsResolved = true;
            record.Resolution = resolution;
        }

        // Heroi que capturou, se ainda for do reino captor; senao null.
        private Hero GetValidCapturer(KingCaptureRecord record, Kingdom captor)
        {
            Hero capturer = FindHero(record.CaptorHeroId);

            if (capturer == null ||
                !capturer.IsAlive ||
                capturer.Clan == null ||
                capturer.Clan.Kingdom != captor)
            {
                return null;
            }

            return capturer;
        }

        // ------------------------------------------------------------
        // DETECCAO DA CAPTURA (varre os reis a cada hora: custo minimo)
        // ------------------------------------------------------------
        private void OnHourlyTick()
        {
            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom == null)
                {
                    continue;
                }

                Hero king = kingdom.Leader;

                if (king == null || !king.IsPrisoner)
                {
                    continue;
                }

                PartyBase holder = king.PartyBelongedToAsPrisoner;
                Kingdom captor = holder != null
                    ? holder.MapFaction as Kingdom
                    : null;

                if (captor == null ||
                    captor == kingdom ||
                    !captor.IsAtWarWith(kingdom) ||
                    FindOpenRecord(king) != null)
                {
                    continue;
                }

                RegisterCapture(king, kingdom, captor, holder);
            }
        }

        private void RegisterCapture(
            Hero king,
            Kingdom kingdom,
            Kingdom captor,
            PartyBase holder)
        {
            KingCaptureRecord record = new KingCaptureRecord();
            record.KingHeroId = king.StringId;
            record.KingKingdomId = kingdom.StringId;
            record.CaptorKingdomId = captor.StringId;
            record.CaptorHeroId = holder.Owner != null
                ? holder.Owner.StringId
                : "";
            record.CapturedOnDay = CampaignTime.Now.ToDays;

            _captures.Add(record);

            Hero capturer = FindHero(record.CaptorHeroId);
            string capturerName = capturer != null
                ? capturer.Name.ToString()
                : captor.Name.ToString();

            if (IsPlayerSovereignOf(captor))
            {
                ShowMessage("CB: " + king.Name + ", soberano de " +
                    kingdom.Name + ", foi capturado por " + capturerName +
                    ". Voce dita os termos: abra o botao WS ou o menu " +
                    "\"Casus Belli: guerra e paz\" numa cidade ou castelo.");
            }
            else if (IsPlayerKingdom(captor))
            {
                string rulerName = captor.Leader != null
                    ? captor.Leader.Name.ToString()
                    : "o soberano";

                ShowMessage("CB: " + king.Name + ", soberano de " +
                    kingdom.Name + ", foi capturado por " + capturerName +
                    ". " + rulerName + " decidira os termos.");
            }
            else if (king == Hero.MainHero)
            {
                ShowMessage("CB: voce foi capturado por " + captor.Name +
                    ". O soberano captor decidira os termos.");
            }
        }

        // ------------------------------------------------------------
        // PROTECAO CONTRA FUGA
        // ------------------------------------------------------------
        public bool IsEscapeBlocked(Hero hero)
        {
            if (hero == null || !hero.IsPrisoner)
            {
                return false;
            }

            Kingdom kingdom = hero.Clan != null ? hero.Clan.Kingdom : null;

            if (kingdom == null || kingdom.Leader != hero)
            {
                return false;
            }

            PartyBase holder = hero.PartyBelongedToAsPrisoner;
            Kingdom captor = holder != null
                ? holder.MapFaction as Kingdom
                : null;

            if (captor == null ||
                captor == kingdom ||
                !captor.IsAtWarWith(kingdom))
            {
                return false;
            }

            KingCaptureRecord record = FindOpenRecord(hero);

            if (record == null)
            {
                return true;   // acabou de ser capturado: ainda sem ficha
            }

            return CampaignTime.Now.ToDays - record.CapturedOnDay <=
                ProtectionDays;
        }

        // ------------------------------------------------------------
        // RANCOR: o reino do rei capturado quer revanche
        // (clemencia nao gera rancor)
        // ------------------------------------------------------------
        public float GetGrievanceFactor(
            Kingdom attacker,
            Kingdom defender,
            KingdomProfile attackerProfile)
        {
            double now = CampaignTime.Now.ToDays;

            foreach (KingCaptureRecord record in _captures)
            {
                if (record.KingKingdomId == attacker.StringId &&
                    record.CaptorKingdomId == defender.StringId &&
                    record.Resolution != "MERCY" &&
                    now - record.CapturedOnDay <= GrievanceWindowDays)
                {
                    return 1f + 0.3f * attackerProfile.Vengefulness;
                }
            }

            return 1f;
        }

        // ------------------------------------------------------------
        // NEGOCIACAO
        // ------------------------------------------------------------
        public int GetBaseRansom(Hero king)
        {
            int tier = king.Clan != null ? king.Clan.Tier : 3;

            return Math.Max(RansomMinimum, tier * RansomPerClanTier);
        }

        // Ouro que o cla do rei consegue juntar.
        public int GetPayableGold(Hero king)
        {
            int total = Math.Max(0, king.Gold);

            if (king.Clan != null)
            {
                foreach (Hero member in king.Clan.Heroes)
                {
                    if (member != null && member != king && member.IsAlive)
                    {
                        total += Math.Max(0, member.Gold);
                    }
                }
            }

            return total;
        }

        public List<NegotiationOffer> GetOffers(KingCaptureRecord record)
        {
            List<NegotiationOffer> offers = new List<NegotiationOffer>();

            CasusBelliCampaignBehavior main = GetMain();
            Hero king = FindHero(record.KingHeroId);
            Kingdom captive = GetKingdom(record.KingKingdomId);
            Kingdom captor = GetKingdom(record.CaptorKingdomId);

            if (main == null || king == null || captive == null || captor == null)
            {
                return offers;
            }

            KingdomProfile captorProfile = KingdomProfiles.Get(captor.StringId);
            KingdomProfile captiveProfile = KingdomProfiles.Get(captive.StringId);

            // 1) Cumprir o objetivo de guerra
            NegotiationOffer objective = new NegotiationOffer();
            objective.Type = NegotiationOfferType.Objective;
            objective.Title = "Exigir o objetivo de guerra";
            objective.WillAccept = true;

            WarRecord war;
            Settlement target = null;

            if (main.TryGetActiveWarBetween(
                    captor.StringId,
                    captive.StringId,
                    out war) &&
                war.CasusBelliType == CasusBelliCampaignBehavior.ClaimWarType &&
                war.AttackerKingdomId == captor.StringId &&
                war.ObjectiveAchievedOnDay < 0.0)
            {
                target = Settlement.Find(war.TargetSettlementId);
            }

            if (target != null && main.GetSettlementOwnerKingdom(target) == captive)
            {
                objective.IsEnabled = true;
                objective.Detail = target.Name + " passa ao soberano, o rei " +
                    "e libertado e a guerra termina em paz.";
            }
            else
            {
                objective.IsEnabled = false;
                objective.DisabledReason =
                    "Nao ha objetivo de guerra pendente contra este reino.";
            }

            offers.Add(objective);

            // 2 e 3) Resgates
            int payable = GetPayableGold(king);
            int baseRansom = GetBaseRansom(king);
            float acceptableMultiple =
                AcceptableMultiplePerPayFactor * captiveProfile.RansomPayFactor;

            offers.Add(BuildRansomOffer(
                NegotiationOfferType.RansomModerate,
                "Pedir resgate moderado",
                ModerateRansomMultiple,
                baseRansom,
                payable,
                captorProfile,
                acceptableMultiple));

            offers.Add(BuildRansomOffer(
                NegotiationOfferType.RansomHeavy,
                "Exigir resgate pesado",
                HeavyRansomMultiple,
                baseRansom,
                payable,
                captorProfile,
                acceptableMultiple));

            // 4) Clemencia
            // [NOVA OFERTA 2/5] Cada oferta nova e montada aqui, na ordem
            // em que deve aparecer no texto da negociacao.
            NegotiationOffer mercy = new NegotiationOffer();
            mercy.Type = NegotiationOfferType.Mercy;
            mercy.Title = "Libertar por clemencia";
            mercy.IsEnabled = true;
            mercy.WillAccept = true;
            mercy.Detail = "O rei e libertado sem pagar nada e a guerra " +
                "termina em paz. O reino dele nao guarda rancor.";
            offers.Add(mercy);

            return offers;
        }

        private NegotiationOffer BuildRansomOffer(
            NegotiationOfferType type,
            string title,
            float multiple,
            int baseRansom,
            int payable,
            KingdomProfile captorProfile,
            float acceptableMultiple)
        {
            int demanded = (int)(baseRansom * multiple *
                captorProfile.RansomDemandFactor);

            NegotiationOffer offer = new NegotiationOffer();
            offer.Type = type;
            offer.Title = title;
            offer.Amount = Math.Min(demanded, payable);
            offer.WillAccept = multiple <= acceptableMultiple;

            if (payable <= 0)
            {
                offer.IsEnabled = false;
                offer.DisabledReason = "O cla do rei nao tem ouro para pagar.";
                return offer;
            }

            offer.IsEnabled = true;
            offer.Detail = "Exige " + demanded + " denarios" +
                (demanded > payable
                    ? " (o cla so consegue pagar " + payable + ")"
                    : "") +
                ". Reacao provavel: " +
                (offer.WillAccept ? "aceita" : "recusa") + ".";

            return offer;
        }

        // Negociacao que espera uma decisao do JOGADOR: so existe quando o
        // jogador e o SOBERANO do reino captor (nao importa quem capturou).
        public bool TryGetPlayerNegotiation(out KingCaptureRecord found)
        {
            found = null;

            if (Hero.MainHero == null)
            {
                return false;
            }

            foreach (KingCaptureRecord record in _captures)
            {
                if (record.IsResolved)
                {
                    continue;
                }

                Kingdom captor = GetKingdom(record.CaptorKingdomId);

                if (!IsPlayerSovereignOf(captor))
                {
                    continue;
                }

                Hero king = FindHero(record.KingHeroId);

                if (king == null || !king.IsPrisoner)
                {
                    continue;
                }

                found = record;
                return true;
            }

            return false;
        }

        public string BuildNegotiationText(KingCaptureRecord record)
        {
            StringBuilder text = new StringBuilder();

            Hero king = FindHero(record.KingHeroId);
            Kingdom captive = GetKingdom(record.KingKingdomId);
            Kingdom captor = GetKingdom(record.CaptorKingdomId);

            if (king == null || captive == null)
            {
                return "Negociacao indisponivel.";
            }

            double now = CampaignTime.Now.ToDays;
            KingdomProfile profile = KingdomProfiles.Get(captive.StringId);
            Hero capturer = captor != null
                ? GetValidCapturer(record, captor)
                : null;

            text.Append("Soberano capturado: " + king.Name + " (" +
                captive.Name + ")\n");

            if (capturer != null)
            {
                text.Append("Capturado por: " + capturer.Name + "\n");
            }

            text.Append("Cativo ha " +
                (now - record.CapturedOnDay).ToString("0") + " dias. ");
            text.Append("Protecao contra fuga: " +
                Math.Max(0.0, ProtectionDays - (now - record.CapturedOnDay))
                    .ToString("0") + " dias.\n");
            text.Append("Reacao do reino: " + profile.Name + ". " +
                profile.Description + "\n\n");
            text.Append("Voce dita os termos:\n");

            foreach (NegotiationOffer offer in GetOffers(record))
            {
                text.Append("- " + offer.Title + ": " +
                    (offer.IsEnabled ? offer.Detail : offer.DisabledReason) +
                    "\n");
            }

            return text.ToString();
        }

        public bool ExecuteOffer(
            KingCaptureRecord record,
            NegotiationOfferType type,
            out string message)
        {
            message = "";

            CasusBelliCampaignBehavior main = GetMain();
            Hero king = FindHero(record.KingHeroId);
            Kingdom captive = GetKingdom(record.KingKingdomId);
            Kingdom captor = GetKingdom(record.CaptorKingdomId);

            if (record.IsResolved ||
                main == null ||
                king == null ||
                captive == null ||
                captor == null ||
                !king.IsPrisoner)
            {
                message = "negociacao indisponivel";
                return false;
            }

            // Quem decide e recebe: o SOBERANO do reino captor.
            // O heroi que capturou (se ainda for do reino) so recebe parte
            // do resgate.
            Hero decider = captor.Leader;

            if (decider == null)
            {
                message = "o reino captor nao tem soberano";
                return false;
            }

            Hero capturer = GetValidCapturer(record, captor);

            NegotiationOffer offer = GetOffers(record).Find(o => o.Type == type);

            if (offer == null || !offer.IsEnabled)
            {
                message = offer != null ? offer.DisabledReason : "oferta invalida";
                return false;
            }

            if (type == NegotiationOfferType.Objective)
            {
                WarRecord war;

                if (!main.TryGetActiveWarBetween(
                        captor.StringId,
                        captive.StringId,
                        out war))
                {
                    message = "a guerra ja terminou";
                    return false;
                }

                Settlement target = Settlement.Find(war.TargetSettlementId);

                if (target == null)
                {
                    message = "feudo-alvo nao encontrado";
                    return false;
                }

                if (war.ObjectiveAchievedOnDay < 0.0)
                {
                    war.ObjectiveAchievedOnDay = CampaignTime.Now.ToDays;
                }

                // O feudo vai para o soberano. Para dar ao heroi que
                // capturou, troque "decider" por "capturer ?? decider".
                ChangeOwnerOfSettlementAction.ApplyByKingDecision(decider, target);
                EndCaptivityAction.ApplyByPeace(king, decider);
                MakePeaceAction.Apply(captor, captive);

                Resolve(record, "OBJECTIVE");

                message = captive.Name + " cedeu " + target.Name + " a " +
                    captor.Name + " e " + king.Name + " foi libertado.";
                return true;
            }

            // [NOVA OFERTA 3/5] Cada oferta nova ganha um bloco "if (type ==
            // ...)" aqui, ANTES do bloco de resgate, e termina com return.
            if (type == NegotiationOfferType.Mercy)
            {
                EndCaptivityAction.ApplyByPeace(king, decider);
                MakePeaceAction.Apply(captor, captive);

                Resolve(record, "MERCY");

                message = captor.Name + " libertou " + king.Name +
                    " por clemencia; a guerra terminou.";
                return true;
            }

            // Resgate
            if (!offer.WillAccept)
            {
                message = captive.Name + " recusou pagar " + offer.Amount +
                    " denarios por " + king.Name + ". Tente outra oferta.";
                return false;
            }

            int paid = ChargeClan(king, decider, offer.Amount);

            // Parte do capturador (quando nao e o proprio soberano).
            if (capturer != null && capturer != decider && paid > 0)
            {
                int share = (int)(paid * CaptorRansomShare);

                if (share > 0)
                {
                    GiveGoldAction.ApplyBetweenCharacters(
                        decider,
                        capturer,
                        share,
                        true);
                }
            }

            EndCaptivityAction.ApplyByRansom(king, decider);
            MakePeaceAction.Apply(captor, captive);

            Resolve(record, type == NegotiationOfferType.RansomHeavy
                ? "RANSOM_HEAVY"
                : "RANSOM");

            message = captive.Name + " pagou " + paid + " denarios pelo resgate de " +
                king.Name + "; a guerra terminou.";
            return true;
        }

        private int ChargeClan(Hero king, Hero receiver, int amount)
        {
            List<Hero> payers = new List<Hero>();
            payers.Add(king);

            if (king.Clan != null)
            {
                foreach (Hero member in king.Clan.Heroes)
                {
                    if (member != null && member != king && member.IsAlive)
                    {
                        payers.Add(member);
                    }
                }
            }

            int remaining = amount;
            int paid = 0;

            foreach (Hero payer in payers)
            {
                if (remaining <= 0)
                {
                    break;
                }

                int chunk = Math.Min(remaining, Math.Max(0, payer.Gold));

                if (chunk <= 0)
                {
                    continue;
                }

                GiveGoldAction.ApplyBetweenCharacters(payer, receiver, chunk, true);
                remaining -= chunk;
                paid += chunk;
            }

            return paid;
        }

        // ------------------------------------------------------------
        // ROTINA DIARIA: encerra fichas e resolve as da IA
        // ------------------------------------------------------------
        private void OnDailyTick()
        {
            double now = CampaignTime.Now.ToDays;

            List<KingCaptureRecord> snapshot =
                new List<KingCaptureRecord>(_captures);

            foreach (KingCaptureRecord record in snapshot)
            {
                if (record.IsResolved)
                {
                    if (now - record.CapturedOnDay > 730.0)
                    {
                        _captures.Remove(record);
                    }

                    continue;
                }

                Hero king = FindHero(record.KingHeroId);
                Kingdom captive = GetKingdom(record.KingKingdomId);
                Kingdom captor = GetKingdom(record.CaptorKingdomId);

                if (king == null || captive == null || captor == null ||
                    !king.IsPrisoner)
                {
                    Resolve(record, "RELEASED");
                    continue;
                }

                if (!captor.IsAtWarWith(captive))
                {
                    Resolve(record, "PEACE");
                    continue;
                }

                // So o soberano-jogador decide sozinho. Se o jogador e
                // apenas vassalo do reino captor, a IA do soberano resolve.
                if (IsPlayerSovereignOf(captor))
                {
                    continue;
                }

                if (now - record.CapturedOnDay >= AiNegotiationDelayDays)
                {
                    ResolveByAi(record, captor, captive);
                }
            }
        }

        // A IA do soberano captor escolhe entre as mesmas ofertas do jogador.
        // [NOVA OFERTA 4/5] Se quiser que a IA use a oferta nova, acrescente
        // um "else if" aqui. (A clemencia nao e escolhida pela IA.)
        private void ResolveByAi(
            KingCaptureRecord record,
            Kingdom captor,
            Kingdom captive)
        {
            List<NegotiationOffer> offers = GetOffers(record);
            KingdomProfile captorProfile = KingdomProfiles.Get(captor.StringId);

            NegotiationOffer objective =
                offers.Find(o => o.Type == NegotiationOfferType.Objective);
            NegotiationOffer moderate =
                offers.Find(o => o.Type == NegotiationOfferType.RansomModerate);
            NegotiationOffer heavy =
                offers.Find(o => o.Type == NegotiationOfferType.RansomHeavy);

            NegotiationOfferType choice;

            if (objective != null && objective.IsEnabled &&
                (captorProfile.PrefersObjectiveOverRansom ||
                 MBRandom.RandomFloat < 0.6f))
            {
                choice = NegotiationOfferType.Objective;
            }
            else if (heavy != null && heavy.IsEnabled && heavy.WillAccept)
            {
                choice = NegotiationOfferType.RansomHeavy;
            }
            else if (moderate != null && moderate.IsEnabled)
            {
                choice = NegotiationOfferType.RansomModerate;
            }
            else
            {
                return;   // sem acordo possivel: segue cativo, tenta amanha
            }

            string message;

            if (ExecuteOffer(record, choice, out message) &&
                (IsPlayerKingdom(captive) || IsPlayerKingdom(captor)))
            {
                ShowMessage("CB: " + message);
            }
        }
    }
}
