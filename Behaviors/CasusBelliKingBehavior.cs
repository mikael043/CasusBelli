using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        public double NextAttempt;
        public int Refusals;
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
        public Settlement Target;
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
    public partial class CasusBelliKingBehavior : CampaignBehaviorBase
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

        private const int FieldsPerKingCapture = 7;

        private List<KingCaptureRecord> _captures =
            new List<KingCaptureRecord>();
        private List<string> _savedCaptureData = new List<string>();

        public override void RegisterEvents()
        {
            DebugLog.Log("CasusBelliKingBehavior.RegisterEvents INICIO");
            try {
                CampaignEvents.HourlyTickEvent.AddNonSerializedListener(
                    this,
                    OnHourlyTick);
                DebugLog.Log("CasusBelliKingBehavior.RegisterEvents: HourlyTick OK");
            } catch (Exception ex) { DebugLog.Log("CasusBelliKingBehavior.RegisterEvents EXCEPTION HourlyTick: " + ex.Message); }

            try {
                CampaignEvents.DailyTickEvent.AddNonSerializedListener(
                    this,
                    OnDailyTick);
                DebugLog.Log("CasusBelliKingBehavior.RegisterEvents: DailyTick OK");
            } catch (Exception ex) { DebugLog.Log("CasusBelliKingBehavior.RegisterEvents EXCEPTION DailyTick: " + ex.Message); }

            DebugLog.Log("CasusBelliKingBehavior.RegisterEvents FIM");
        }

        public override void SyncData(IDataStore dataStore)
        {
            int width = 9;
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
                    _savedCaptureData.Add(record.NextAttempt.ToString("R", CultureInfo.InvariantCulture));
                    _savedCaptureData.Add(record.Refusals.ToString(CultureInfo.InvariantCulture));
                }
            }

            if (!dataStore.SyncData("CasusBelli_KingCaptureData_v2", ref _savedCaptureData) && dataStore.IsLoading)
            {
                width = FieldsPerKingCapture;
                dataStore.SyncData("CasusBelli_KingCaptureData_v1", ref _savedCaptureData);
            }

            if (dataStore.IsLoading)
            {
                _captures.Clear();

                if (_savedCaptureData == null)
                {
                    _savedCaptureData = new List<string>();
                }

                for (int i = 0;
                     i + width <= _savedCaptureData.Count;
                     i += width)
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
                    if (width == 9)
                    {
                        double.TryParse(_savedCaptureData[i + 7], NumberStyles.Float, CultureInfo.InvariantCulture, out record.NextAttempt);
                        int.TryParse(_savedCaptureData[i + 8], out record.Refusals);
                    }

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
                    !captor.IsAtWarWith(kingdom))
                {
                    continue;
                }

                var record = FindOpenRecord(king);
                if (record == null)
                {
                    RegisterCapture(king, kingdom, captor, holder);
                    record = FindOpenRecord(king);
                }
                if (!IsPlayerSovereignOf(captor) && GetMain() != null &&
                    GetMain().TryGetActiveWarBetween(captor.StringId, kingdom.StringId, out var war) &&
                    war.IsTerritorial && war.AttackerKingdomId == captor.StringId)
                {
                    if (ExecuteOffer(record, NegotiationOfferType.Objective, out var result) &&
                        (IsPlayerKingdom(captor) || IsPlayerKingdom(kingdom))) ShowMessage(result);
                }
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
        internal Hero AuthorizedRansom { get; private set; }

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

            return true;
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
            return king?.Clan?.Kingdom == null ? 0 : RoyalSettlement.Available(king.Clan.Kingdom);
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
            text.Append("Janela inicial de negociação restante: " +
                Math.Max(0.0, ProtectionDays - (now - record.CapturedOnDay))
                    .ToString("0") + " dias.\n");
            if (now - record.CapturedOnDay >= ProtectionDays && captive.RulingClan.Influence > 0)
                text.Append("A janela voluntária encerrou. O cativeiro continua até a capitulação ou a libertação.\n");
            text.Append("Reacao do reino: " + profile.Name + ". " +
                profile.Description + "\n\n");
            text.Append("Cativeiro: o clã governante perde 30 de influência por dia. Os demais perdem 1 no primeiro dia, 2 no segundo e assim por diante. Com a influência governante esgotada, os termos são obrigatórios. Os lordes financiam o resgate. Metade cabe ao capturador e o restante aos comandantes participantes.\n");
            if (record.NextAttempt > now)
                text.Append($"Nova negociação em {Math.Ceiling(record.NextAttempt - now)} dias.\n");

            foreach (NegotiationOffer offer in GetOffers(record))
            {
                text.Append("- " + offer.Title + ": " +
                    (offer.IsEnabled ? offer.Detail : offer.DisabledReason) +
                    "\n");
            }

            return text.ToString();
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

                if (king.PartyBelongedToAsPrisoner?.MapFaction != captor || captive.Leader != king)
                {
                    Resolve(record, "CUSTODY_CHANGED");
                    continue;
                }
                if (!captor.IsAtWarWith(captive))
                {
                    Resolve(record, "PEACE");
                    continue;
                }

                int days = (int)(now - record.CapturedOnDay);
                if (days >= 1)
                    foreach (var clan in captive.Clans)
                    {
                        if (clan.IsEliminated || clan.IsUnderMercenaryService || clan.Influence <= 0) continue;
                        ChangeClanInfluenceAction.Apply(clan, -Math.Min(clan.Influence, clan == captive.RulingClan ? 30 : days));
                    }
                if (captive.RulingClan.Influence <= 0) record.NextAttempt = 0;
                if (now < record.NextAttempt) continue;

                // So o soberano-jogador decide sozinho. Se o jogador e
                // apenas vassalo do reino captor, a IA do soberano resolve.
                if (IsPlayerSovereignOf(captor))
                {
                    continue;
                }

                if (captive.RulingClan.Influence <= 0 || now - record.CapturedOnDay >= AiNegotiationDelayDays)
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

            if (record.Refusals > 0 && heavy != null && heavy.IsEnabled) choice = NegotiationOfferType.RansomHeavy;
            else if (objective != null && objective.IsEnabled &&
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
                if (captive.RulingClan.Influence > 0) return;
                choice = NegotiationOfferType.Mercy;
            }

            if (IsPlayerSovereignOf(captive) && captive.RulingClan.Influence > 0)
            {
                var proposal = offers.Find(o => o.Type == choice);
                var target = proposal.Target;
                record.NextAttempt = CampaignTime.Now.ToDays + 3;
                InformationManager.ShowInquiry(new InquiryData(proposal.Title,
                    target != null ? $"Ceder {target.Name} para libertar o soberano e terminar a guerra?" :
                    $"Pagar {proposal.Amount} denários para libertar o soberano? " +
                    (choice == NegotiationOfferType.RansomModerate ? "A guerra continua." : "A guerra termina."),
                    true, true, "Aceitar", "Recusar", () =>
                    {
                        record.NextAttempt = CampaignTime.Now.ToDays;
                        var current = GetOffers(record).Find(o => o.Type == choice);
                        if (current == null || current.Target != target || current.Amount != proposal.Amount) return;
                        ExecuteOffer(record, choice, out var result);
                        ShowMessage(result);
                    }, () => Refuse(record, target != null)));
                return;
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
