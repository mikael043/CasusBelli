using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli
{
    // Decomposicao do WarScore. Todos os valores sao da perspectiva do
    // ATACANTE da ficha de guerra (positivo = atacante vencendo).
    public class WarScoreBreakdown
    {
        public double Battles = 0.0;
        public double Sieges = 0.0;
        public double Objective = 0.0;
        public double Prisoners = 0.0;
        public double Total = 0.0;
        public bool AttackerHoldsEnemyKing = false;
        public bool DefenderHoldsEnemyKing = false;
    }

    public class WarScoreState
    {
        public string WarId = "";
        public double BattlePoints = 0.0;   // perspectiva do atacante
        public double SiegePoints = 0.0;    // perspectiva do atacante
        public double FirstSeenDay = -1.0;
    }

    // WarScore de -100% a +100% por guerra, inspirado em Crusader Kings 2:
    //   Batalhas (cap 75) + Cercos + Objetivo do CB (ticking) + Prisioneiros.
    // Tambem executa as tres acoes de paz (exigencias, paz branca, rendicao)
    // e a IA que encerra guerras de reino contra reino. A paz padrao do jogo
    // e bloqueada para guerras registradas (veja CasusBelliDiplomacyModel).
    //
    // Isolado de proposito: para desligar, remova o AddBehavior no SubModule.
    public class CasusBelliWarScoreBehavior : CampaignBehaviorBase
    {
        // ---------------- configuracao ----------------
        // Batalhas campais
        private static readonly double BattleCap = 75.0;
        private static readonly double PerBattleCap = 25.0;
        private static readonly double BattleGain = 100.0;       // pontos por 100% das tropas do perdedor
        private static readonly int MinBattleDeaths = 5;
        private static readonly double MinKingdomTroops = 1500.0;

        // Cercos (feudo conquistado)
        private static readonly double TownPoints = 15.0;
        private static readonly double CastlePoints = 8.0;

        // Objetivo do casus belli: ganho por dia segurando o feudo-alvo
        private static readonly double ObjectiveTickPerDay = 1.0;
        private static readonly double ObjectiveTickCap = 25.0;

        // Prisioneiros (o rei inimigo cativo vale +100% automatico)
        private static readonly double HighTierLeaderPoints = 15.0;
        private static readonly double LeaderPoints = 10.0;
        private static readonly double LordPoints = 5.0;
        private static readonly double MinorPoints = 2.0;

        // Limiares das acoes
        public static readonly double EnforceThreshold = 100.0;
        public static readonly double SurrenderThreshold = 80.0;
        public static readonly double WhitePeaceBand = 15.0;
        public static readonly double WhitePeaceMinDays = 45.0;

        // Exigencias de uma guerra sem feudo-alvo: parte do ouro do soberano
        private static readonly float IndemnityShare = 0.5f;
        private static readonly int IndemnityMax = 30000;

        // IA
        private static readonly double AiMaxWarDays = 240.0;
        private static readonly double AiWhitePeaceOfferDays = 90.0;
        private static readonly double AiOfferCooldownDays = 30.0;
        private static readonly double KingNegotiationGraceDays = 10.0;
        private static readonly double NotifyStep = 5.0;

        private const int FieldsPerState = 4;

        private Dictionary<string, WarScoreState> _states =
            new Dictionary<string, WarScoreState>();
        private List<string> _savedStates = new List<string>();

        // nao persistidos
        private Dictionary<string, double> _offerDay =
            new Dictionary<string, double>();
        private Dictionary<string, double> _lastShown =
            new Dictionary<string, double>();

        // Guerra mostrada no menu e no botao WS.
        public string SelectedWarId = "";

        // Avisa a interface (icone WS) que algum valor mudou.
        public static event Action Changed;

        // Preenchido pela interface Gauntlet (CasusBelliWarScoreHud.cs)
        // quando ela esta ativa. Se for null, o jogo usa o menu de texto.
        public static Action WindowOpener;

        // true quando a interface (UIExtenderEx ou Gauntlet) criou o icone.
        // Usado so no diagnostico (casus_belli.ws_window).
        public static bool UiLoaded;

        public static void RaiseChanged()
        {
            Action handler = Changed;

            if (handler != null)
            {
                handler();
            }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(
                this,
                OnMapEventEnded);

            CampaignEvents.OnSettlementOwnerChangedEvent
                .AddNonSerializedListener(
                    this,
                    OnSettlementOwnerChanged);

            CampaignEvents.DailyTickEvent.AddNonSerializedListener(
                this,
                OnDailyTick);

            // Mantem o icone WS atualizado durante o dia (prisioneiros etc.).
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(
                this,
                OnHourlyTick);
        }

        private void OnHourlyTick()
        {
            RaiseChanged();
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                if (_savedStates == null)
                {
                    _savedStates = new List<string>();
                }

                _savedStates.Clear();

                foreach (WarScoreState state in _states.Values)
                {
                    _savedStates.Add(state.WarId ?? "");
                    _savedStates.Add(Fmt(state.BattlePoints));
                    _savedStates.Add(Fmt(state.SiegePoints));
                    _savedStates.Add(Fmt(state.FirstSeenDay));
                }
            }

            dataStore.SyncData("CasusBelli_WarScore_v1", ref _savedStates);

            if (dataStore.IsLoading)
            {
                _states.Clear();

                if (_savedStates == null)
                {
                    _savedStates = new List<string>();
                }

                for (int i = 0;
                     i + FieldsPerState <= _savedStates.Count;
                     i += FieldsPerState)
                {
                    WarScoreState state = new WarScoreState();
                    state.WarId = _savedStates[i];
                    state.BattlePoints = Parse(_savedStates[i + 1]);
                    state.SiegePoints = Parse(_savedStates[i + 2]);
                    state.FirstSeenDay = Parse(_savedStates[i + 3]);

                    if (!string.IsNullOrEmpty(state.WarId))
                    {
                        _states[state.WarId] = state;
                    }
                }
            }
        }

        // ------------------------------------------------------------
        // AJUDANTES
        // ------------------------------------------------------------
        private static string Fmt(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static double Parse(string text)
        {
            double value;

            if (double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value))
            {
                return value;
            }

            return 0.0;
        }

        private static double Clamp(double value, double min, double max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        public static string FormatPercent(double value)
        {
            int rounded = (int)Math.Round(value);

            return rounded.ToString("+0;-0;0", CultureInfo.InvariantCulture) + "%";
        }

        private static CasusBelliCampaignBehavior GetMain()
        {
            if (Campaign.Current == null)
            {
                return null;
            }

            return Campaign.Current.GetCampaignBehavior<
                CasusBelliCampaignBehavior>();
        }

        private static Kingdom FindKingdom(string kingdomId)
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

        private static string KingdomIdOfHero(Hero hero)
        {
            if (hero == null || hero.Clan == null || hero.Clan.Kingdom == null)
            {
                return "";
            }

            return hero.Clan.Kingdom.StringId ?? "";
        }

        private static string OwnerKingdomId(Settlement settlement)
        {
            return settlement == null ? "" : KingdomIdOfHero(settlement.Owner);
        }

        private static Kingdom GetPlayerKingdom()
        {
            return Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
        }

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

        private static float Eagerness(Kingdom kingdom)
        {
            return KingdomProfiles.Get(kingdom.StringId).PeaceEagerness;
        }

        private static bool InvolvesKingdom(WarRecord war, string kingdomId)
        {
            return war.AttackerKingdomId == kingdomId ||
                war.DefenderKingdomId == kingdomId;
        }

        private static Kingdom OtherSide(WarRecord war, Kingdom kingdom)
        {
            return FindKingdom(kingdom.StringId == war.AttackerKingdomId
                ? war.DefenderKingdomId
                : war.AttackerKingdomId);
        }

        private WarScoreState GetState(WarRecord war, bool create)
        {
            WarScoreState state;

            if (_states.TryGetValue(war.WarId, out state))
            {
                return state;
            }

            if (!create)
            {
                return null;
            }

            state = new WarScoreState();
            state.WarId = war.WarId;
            state.FirstSeenDay = CampaignTime.Now.ToDays;
            _states[war.WarId] = state;
            return state;
        }

        // ------------------------------------------------------------
        // CONSULTAS
        // ------------------------------------------------------------
        public List<WarRecord> GetActiveWarsOf(Kingdom kingdom)
        {
            List<WarRecord> result = new List<WarRecord>();
            CasusBelliCampaignBehavior main = GetMain();

            if (kingdom == null || main == null)
            {
                return result;
            }

            foreach (WarRecord war in main.GetWarRegistry().AllWars)
            {
                if (war == null ||
                    !war.IsActive ||
                    !InvolvesKingdom(war, kingdom.StringId))
                {
                    continue;
                }

                Kingdom other = OtherSide(war, kingdom);

                if (other == null || other.IsEliminated)
                {
                    continue;
                }

                result.Add(war);
            }

            return result;
        }

        public WarRecord GetSelectedWar(Kingdom kingdom)
        {
            List<WarRecord> wars = GetActiveWarsOf(kingdom);

            if (wars.Count == 0)
            {
                return null;
            }

            foreach (WarRecord war in wars)
            {
                if (war.WarId == SelectedWarId)
                {
                    return war;
                }
            }

            SelectedWarId = wars[0].WarId;
            return wars[0];
        }

        public void CycleSelectedWar(Kingdom kingdom)
        {
            List<WarRecord> wars = GetActiveWarsOf(kingdom);

            if (wars.Count == 0)
            {
                return;
            }

            int index = wars.FindIndex(w => w.WarId == SelectedWarId);

            SelectedWarId = wars[(index + 1) % wars.Count].WarId;
        }

        public Kingdom GetEnemyKingdom(WarRecord war, Kingdom kingdom)
        {
            return OtherSide(war, kingdom);
        }

        public double GetWarAgeDays(WarRecord war)
        {
            double now = CampaignTime.Now.ToDays;

            if (war.StartedOnDay >= 0.0)
            {
                return now - war.StartedOnDay;
            }

            WarScoreState state = GetState(war, false);

            return state != null && state.FirstSeenDay >= 0.0
                ? now - state.FirstSeenDay
                : 0.0;
        }

        public WarScoreBreakdown GetBreakdown(WarRecord war)
        {
            WarScoreBreakdown result = new WarScoreBreakdown();

            if (war == null)
            {
                return result;
            }

            WarScoreState state = GetState(war, false);

            if (state != null)
            {
                result.Battles = Clamp(state.BattlePoints, -BattleCap, BattleCap);
                result.Sieges = state.SiegePoints;
            }

            result.Objective = ObjectivePoints(war);
            result.Prisoners = PrisonerPoints(war, result);

            double sum = result.Battles + result.Sieges +
                result.Objective + result.Prisoners;

            if (result.AttackerHoldsEnemyKing)
            {
                sum = 100.0;
            }
            else if (result.DefenderHoldsEnemyKing)
            {
                sum = -100.0;
            }

            result.Total = Clamp(sum, -100.0, 100.0);
            return result;
        }

        // WarScore da perspectiva de um reino.
        public double ScoreFor(WarRecord war, string kingdomId)
        {
            double total = GetBreakdown(war).Total;

            return kingdomId == war.AttackerKingdomId ? total : -total;
        }

        private double ObjectivePoints(WarRecord war)
        {
            if (war.CasusBelliType != CasusBelliCampaignBehavior.ClaimWarType ||
                war.ObjectiveAchievedOnDay < 0.0)
            {
                return 0.0;
            }

            Settlement target = Settlement.Find(war.TargetSettlementId);

            if (target == null ||
                OwnerKingdomId(target) != war.AttackerKingdomId)
            {
                return 0.0;
            }

            double days = CampaignTime.Now.ToDays - war.ObjectiveAchievedOnDay;

            return Clamp(days * ObjectiveTickPerDay, 0.0, ObjectiveTickCap);
        }

        private double PrisonerPoints(WarRecord war, WarScoreBreakdown flags)
        {
            Kingdom attacker = FindKingdom(war.AttackerKingdomId);
            Kingdom defender = FindKingdom(war.DefenderKingdomId);

            if (attacker == null || defender == null)
            {
                return 0.0;
            }

            double total = 0.0;

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                if (hero == null || !hero.IsPrisoner)
                {
                    continue;
                }

                Kingdom own = hero.Clan != null ? hero.Clan.Kingdom : null;
                PartyBase holder = hero.PartyBelongedToAsPrisoner;
                Kingdom captor = holder != null
                    ? holder.MapFaction as Kingdom
                    : null;

                if (own == null || captor == null)
                {
                    continue;
                }

                int sign;

                if (captor == attacker && own == defender)
                {
                    sign = 1;
                }
                else if (captor == defender && own == attacker)
                {
                    sign = -1;
                }
                else
                {
                    continue;
                }

                if (own.Leader == hero)
                {
                    if (sign > 0)
                    {
                        flags.AttackerHoldsEnemyKing = true;
                    }
                    else
                    {
                        flags.DefenderHoldsEnemyKing = true;
                    }

                    continue;
                }

                total += sign * PrisonerValue(hero);
            }

            return total;
        }

        private static double PrisonerValue(Hero hero)
        {
            if (hero.Clan == null)
            {
                return MinorPoints;
            }

            if (hero.Clan.Leader == hero)
            {
                return hero.Clan.Tier >= 5 ? HighTierLeaderPoints : LeaderPoints;
            }

            return hero.Clan.Tier >= 3 ? LordPoints : MinorPoints;
        }

        // ------------------------------------------------------------
        // EVENTOS: BATALHAS E CERCOS
        // ------------------------------------------------------------
        private static Kingdom KingdomOfSide(MapEventSide side)
        {
            if (side == null || side.LeaderParty == null)
            {
                return null;
            }

            return side.LeaderParty.MapFaction as Kingdom;
        }

        private static int CountDead(MapEventSide side)
        {
            int dead = 0;

            foreach (MapEventParty party in side.Parties)
            {
                if (party != null && party.DiedInBattle != null)
                {
                    dead += party.DiedInBattle.TotalManCount;
                }
            }

            return dead;
        }

        private static double KingdomTroops(Kingdom kingdom)
        {
            double total = 0.0;

            foreach (MobileParty party in MobileParty.All)
            {
                if (party != null &&
                    party.IsLordParty &&
                    party.MemberRoster != null &&
                    party.MapFaction == kingdom)
                {
                    total += party.MemberRoster.TotalManCount;
                }
            }

            return total;
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            try
            {
                if (mapEvent == null ||
                    !mapEvent.IsFieldBattle ||
                    !mapEvent.HasWinner)
                {
                    return;
                }

                BattleSideEnum winningSide = mapEvent.WinningSide;
                BattleSideEnum losingSide =
                    winningSide == BattleSideEnum.Attacker
                        ? BattleSideEnum.Defender
                        : BattleSideEnum.Attacker;

                MapEventSide winner = mapEvent.GetMapEventSide(winningSide);
                MapEventSide loser = mapEvent.GetMapEventSide(losingSide);

                Kingdom winnerKingdom = KingdomOfSide(winner);
                Kingdom loserKingdom = KingdomOfSide(loser);
                CasusBelliCampaignBehavior main = GetMain();

                if (winnerKingdom == null ||
                    loserKingdom == null ||
                    winnerKingdom == loserKingdom ||
                    main == null)
                {
                    return;
                }

                WarRecord war;

                if (!main.TryGetActiveWarBetween(
                    winnerKingdom.StringId,
                    loserKingdom.StringId,
                    out war))
                {
                    return;
                }

                int dead = CountDead(loser);

                if (dead < MinBattleDeaths)
                {
                    return;
                }

                double basis = Math.Max(
                    MinKingdomTroops,
                    KingdomTroops(loserKingdom));
                double points = Math.Min(
                    PerBattleCap,
                    dead / basis * BattleGain);

                AddPoints(war, winnerKingdom.StringId, points, true);
            }
            catch (Exception)
            {
                // WarScore nunca pode derrubar o fim de uma batalha.
            }
        }

        private void OnSettlementOwnerChanged(
            Settlement settlement,
            bool openToClaim,
            Hero newOwner,
            Hero oldOwner,
            Hero capturerHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                if (settlement == null ||
                    !settlement.IsFortification ||
                    detail != ChangeOwnerOfSettlementAction
                        .ChangeOwnerOfSettlementDetail.BySiege)
                {
                    return;
                }

                string newKingdomId = KingdomIdOfHero(newOwner);
                string oldKingdomId = KingdomIdOfHero(oldOwner);
                CasusBelliCampaignBehavior main = GetMain();

                if (string.IsNullOrEmpty(newKingdomId) ||
                    string.IsNullOrEmpty(oldKingdomId) ||
                    newKingdomId == oldKingdomId ||
                    main == null)
                {
                    return;
                }

                WarRecord war;

                if (!main.TryGetActiveWarBetween(
                    newKingdomId,
                    oldKingdomId,
                    out war))
                {
                    return;
                }

                AddPoints(
                    war,
                    newKingdomId,
                    settlement.IsTown ? TownPoints : CastlePoints,
                    false);
            }
            catch (Exception)
            {
            }
        }

        // Soma pontos a favor de um reino (perspectiva convertida).
        private void AddPoints(
            WarRecord war,
            string favoredKingdomId,
            double points,
            bool isBattle)
        {
            WarScoreState state = GetState(war, true);
            double signed = favoredKingdomId == war.AttackerKingdomId
                ? points
                : -points;

            if (isBattle)
            {
                state.BattlePoints = Clamp(
                    state.BattlePoints + signed,
                    -BattleCap,
                    BattleCap);
            }
            else
            {
                state.SiegePoints += signed;
            }

            RaiseChanged();
        }

        // Para testes (comando casus_belli.add_ws).
        public bool DebugAdd(string warId, string kind, double value)
        {
            CasusBelliCampaignBehavior main = GetMain();
            WarRecord war;

            if (main == null || !main.GetWarRegistry().TryGetWar(warId, out war))
            {
                return false;
            }

            WarScoreState state = GetState(war, true);

            if (kind == "siege")
            {
                state.SiegePoints += value;
            }
            else
            {
                state.BattlePoints = Clamp(
                    state.BattlePoints + value,
                    -BattleCap,
                    BattleCap);
            }

            RaiseChanged();
            return true;
        }

        // ------------------------------------------------------------
        // ACOES DE PAZ
        // ------------------------------------------------------------
        public bool CanEnforce(WarRecord war, Kingdom kingdom, out string reason)
        {
            reason = "";
            double score = ScoreFor(war, kingdom.StringId);

            if (score >= EnforceThreshold - 0.5)
            {
                return true;
            }

            reason = "WarScore insuficiente: " + FormatPercent(score) +
                " (precisa de +100% ou do rei inimigo cativo)";
            return false;
        }

        public bool CanWhitePeace(WarRecord war, Kingdom kingdom, out string reason)
        {
            reason = "";
            double score = ScoreFor(war, kingdom.StringId);
            double age = GetWarAgeDays(war);

            if (Math.Abs(score) > WhitePeaceBand)
            {
                reason = "Os lados nao estao equilibrados: " +
                    FormatPercent(score) + " (precisa estar entre -" +
                    WhitePeaceBand.ToString("0") + "% e +" +
                    WhitePeaceBand.ToString("0") + "%)";
                return false;
            }

            if (age < WhitePeaceMinDays)
            {
                reason = "Os dois lados ainda nao estao exaustos (guerra com " +
                    age.ToString("0") + " dias; precisa de " +
                    WhitePeaceMinDays.ToString("0") + ")";
                return false;
            }

            return true;
        }

        public bool CanSurrender(WarRecord war, Kingdom kingdom, out string reason)
        {
            reason = "";
            double score = ScoreFor(war, kingdom.StringId);

            if (score <= -SurrenderThreshold)
            {
                return true;
            }

            reason = "A situacao ainda nao e desesperadora: " +
                FormatPercent(score) + " (precisa de -" +
                SurrenderThreshold.ToString("0") + "% ou menos)";
            return false;
        }

        private Settlement GetDemandTarget(
            WarRecord war,
            Kingdom winner,
            Kingdom loser)
        {
            if (war.CasusBelliType != CasusBelliCampaignBehavior.ClaimWarType ||
                war.AttackerKingdomId != winner.StringId)
            {
                return null;
            }

            Settlement target = Settlement.Find(war.TargetSettlementId);

            return target != null && OwnerKingdomId(target) == loser.StringId
                ? target
                : null;
        }

        private static int GetIndemnity(Hero loserLeader)
        {
            if (loserLeader == null)
            {
                return 0;
            }

            return Math.Max(
                0,
                Math.Min((int)(loserLeader.Gold * IndemnityShare), IndemnityMax));
        }

        public string PreviewDemands(WarRecord war, Kingdom winner)
        {
            Kingdom loser = OtherSide(war, winner);

            if (loser == null)
            {
                return "";
            }

            Settlement target = GetDemandTarget(war, winner, loser);

            if (target != null)
            {
                return loser.Name + " cede " + target.Name + " a " +
                    winner.Name + " e a guerra termina.";
            }

            int amount = GetIndemnity(loser.Leader);

            return amount > 0
                ? loser.Name + " paga " + amount +
                    " denarios de indenizacao e a guerra termina."
                : "A guerra termina sem cessao de terras nem indenizacao.";
        }

        // O reino "winner" impoe as exigencias e a paz e feita.
        public bool TryEnforceDemands(
            WarRecord war,
            Kingdom winner,
            out string message)
        {
            message = "";
            Kingdom loser = winner != null ? OtherSide(war, winner) : null;

            if (loser == null || !winner.IsAtWarWith(loser))
            {
                message = "guerra indisponivel";
                return false;
            }

            Hero winnerLeader = winner.Leader;
            Hero loserLeader = loser.Leader;

            if (winnerLeader == null || loserLeader == null)
            {
                message = "reino sem soberano";
                return false;
            }

            Settlement target = GetDemandTarget(war, winner, loser);
            string text;

            if (target != null)
            {
                if (war.ObjectiveAchievedOnDay < 0.0)
                {
                    war.ObjectiveAchievedOnDay = CampaignTime.Now.ToDays;
                }

                ChangeOwnerOfSettlementAction.ApplyByKingDecision(
                    winnerLeader,
                    target);

                text = loser.Name + " cedeu " + target.Name + " a " +
                    winner.Name + ".";
            }
            else
            {
                int amount = GetIndemnity(loserLeader);

                if (amount > 0)
                {
                    GiveGoldAction.ApplyBetweenCharacters(
                        loserLeader,
                        winnerLeader,
                        amount,
                        true);
                }

                text = amount > 0
                    ? loser.Name + " pagou " + amount +
                        " denarios de indenizacao a " + winner.Name + "."
                    : "Paz imposta por " + winner.Name + ".";
            }

            MakePeaceAction.Apply(winner, loser);
            RaiseChanged();

            message = text;
            return true;
        }

        public bool TrySurrender(
            WarRecord war,
            Kingdom loser,
            out string message)
        {
            message = "";
            Kingdom winner = loser != null ? OtherSide(war, loser) : null;

            if (winner == null)
            {
                message = "guerra indisponivel";
                return false;
            }

            return TryEnforceDemands(war, winner, out message);
        }

        public bool TryWhitePeace(WarRecord war, out string message)
        {
            message = "";
            Kingdom a = FindKingdom(war.AttackerKingdomId);
            Kingdom d = FindKingdom(war.DefenderKingdomId);

            if (a == null || d == null || !a.IsAtWarWith(d))
            {
                message = "guerra indisponivel";
                return false;
            }

            MakePeaceAction.Apply(a, d);
            RaiseChanged();

            message = "paz branca entre " + a.Name + " e " + d.Name + ".";
            return true;
        }

        // ------------------------------------------------------------
        // TEXTO DO MENU
        // ------------------------------------------------------------
        private static string BuildBar(double score)
        {
            const int cells = 10;
            int filled = (int)Math.Round(Math.Abs(score) / (100.0 / cells));
            filled = Math.Max(0, Math.Min(cells, filled));

            string left;
            string right;

            if (score >= 0.0)
            {
                left = new string('.', cells);
                right = new string('#', filled) + new string('.', cells - filled);
            }
            else
            {
                left = new string('.', cells - filled) + new string('#', filled);
                right = new string('.', cells);
            }

            return "-100 [" + left + "|" + right + "] +100";
        }

        private static string FactorLine(string label, double value)
        {
            return "  " + label + ": " + FormatPercent(value) + "\n";
        }

        public string BuildWarText(WarRecord war, Kingdom viewer)
        {
            if (war == null || viewer == null)
            {
                return "Seu reino nao esta em guerra.";
            }

            WarScoreBreakdown b = GetBreakdown(war);
            bool viewerIsAttacker = viewer.StringId == war.AttackerKingdomId;
            double sign = viewerIsAttacker ? 1.0 : -1.0;
            Kingdom enemy = OtherSide(war, viewer);
            StringBuilder text = new StringBuilder();

            text.Append("GUERRA: " + viewer.Name + " x " +
                (enemy != null ? enemy.Name.ToString() : "?") + "\n");

            if (war.CasusBelliType == CasusBelliCampaignBehavior.ClaimWarType)
            {
                Settlement target = Settlement.Find(war.TargetSettlementId);

                text.Append("Casus belli: reivindicacao" +
                    (target != null ? " (alvo: " + target.Name + ")" : "") +
                    (viewerIsAttacker ? "" : " - voce e o defensor") + "\n");
            }
            else
            {
                text.Append("Casus belli: nenhum\n");
            }

            text.Append("Duracao: " + GetWarAgeDays(war).ToString("0") + " dias\n\n");

            double total = b.Total * sign;

            text.Append("WARSCORE: " + FormatPercent(total) + "\n");
            text.Append(BuildBar(total) + "\n");
            text.Append("(esquerda = derrota, direita = vitoria)\n\n");

            text.Append("Fatores:\n");
            text.Append(FactorLine("Batalhas (max 75)", b.Battles * sign));
            text.Append(FactorLine("Cercos", b.Sieges * sign));

            if (war.CasusBelliType == CasusBelliCampaignBehavior.ClaimWarType)
            {
                text.Append(FactorLine("Objetivo do casus belli", b.Objective * sign));
            }

            text.Append(FactorLine("Prisioneiros", b.Prisoners * sign));

            bool youHoldKing = viewerIsAttacker
                ? b.AttackerHoldsEnemyKing
                : b.DefenderHoldsEnemyKing;
            bool theyHoldKing = viewerIsAttacker
                ? b.DefenderHoldsEnemyKing
                : b.AttackerHoldsEnemyKing;

            if (youHoldKing)
            {
                text.Append("  Voce mantem o soberano inimigo cativo: +100%.\n");
            }
            else if (theyHoldKing)
            {
                text.Append("  O inimigo mantem o seu soberano cativo: -100%.\n");
            }

            return text.ToString();
        }

        // Texto curto do botao WS. Vazio se nao ha guerra.
        public string GetHudText(Kingdom kingdom)
        {
            WarRecord war = GetSelectedWar(kingdom);

            if (war == null)
            {
                return "";
            }

            return "WS:" + FormatPercent(ScoreFor(war, kingdom.StringId));
        }

        // ------------------------------------------------------------
        // ROTINA DIARIA: limpeza, avisos ao jogador e IA
        // ------------------------------------------------------------
        public void RunDailyForDebug()
        {
            OnDailyTick();
        }

        private void OnDailyTick()
        {
            CasusBelliCampaignBehavior main = GetMain();

            if (main == null)
            {
                return;
            }

            List<WarRecord> active = new List<WarRecord>();
            HashSet<string> activeIds = new HashSet<string>();

            foreach (WarRecord war in main.GetWarRegistry().AllWars)
            {
                if (war != null && war.IsActive)
                {
                    active.Add(war);
                    activeIds.Add(war.WarId);
                }
            }

            List<string> stale = new List<string>();

            foreach (string warId in _states.Keys)
            {
                if (!activeIds.Contains(warId))
                {
                    stale.Add(warId);
                }
            }

            foreach (string warId in stale)
            {
                _states.Remove(warId);
                _lastShown.Remove(warId);
                _offerDay.Remove(warId);
            }

            double now = CampaignTime.Now.ToDays;
            bool inquiryShown = false;

            foreach (WarRecord war in active)
            {
                GetState(war, true);

                Kingdom a = FindKingdom(war.AttackerKingdomId);
                Kingdom d = FindKingdom(war.DefenderKingdomId);

                if (a == null ||
                    d == null ||
                    a.IsEliminated ||
                    d.IsEliminated ||
                    !a.IsAtWarWith(d))
                {
                    continue;
                }

                NotifyPlayer(war, a, d);

                if (IsKingNegotiationFresh(a, d, now))
                {
                    continue;   // a negociacao do rei capturado tem prioridade
                }

                if (IsPlayerSovereignOf(a) || IsPlayerSovereignOf(d))
                {
                    if (!inquiryShown)
                    {
                        inquiryShown = OfferToPlayer(war, a, d, now);
                    }

                    continue;
                }

                RunAiPeace(war, a, d, now);
            }

            RaiseChanged();
        }

        private bool IsKingNegotiationFresh(Kingdom a, Kingdom d, double now)
        {
            if (Campaign.Current == null)
            {
                return false;
            }

            CasusBelliKingBehavior kings =
                Campaign.Current.GetCampaignBehavior<CasusBelliKingBehavior>();

            if (kings == null)
            {
                return false;
            }

            foreach (KingCaptureRecord record in kings.AllCaptures)
            {
                bool samePair =
                    (record.CaptorKingdomId == a.StringId &&
                     record.KingKingdomId == d.StringId) ||
                    (record.CaptorKingdomId == d.StringId &&
                     record.KingKingdomId == a.StringId);

                if (!record.IsResolved &&
                    samePair &&
                    now - record.CapturedOnDay < KingNegotiationGraceDays)
                {
                    return true;
                }
            }

            return false;
        }

        private void NotifyPlayer(WarRecord war, Kingdom a, Kingdom d)
        {
            Kingdom player = GetPlayerKingdom();

            if (player == null || (player != a && player != d))
            {
                return;
            }

            double score = ScoreFor(war, player.StringId);
            double last;

            if (!_lastShown.TryGetValue(war.WarId, out last))
            {
                _lastShown[war.WarId] = score;
                return;
            }

            if (Math.Abs(score - last) >= NotifyStep)
            {
                _lastShown[war.WarId] = score;

                Kingdom enemy = player == a ? d : a;

                ShowMessage("CB: WarScore contra " + enemy.Name + ": " +
                    FormatPercent(score));
            }
        }

        // IA decidindo guerras entre reinos governados pela IA.
        private void RunAiPeace(WarRecord war, Kingdom a, Kingdom d, double now)
        {
            double score = ScoreFor(war, a.StringId);
            double age = GetWarAgeDays(war);
            Kingdom winner = null;
            Kingdom loser = null;

            if (score >= EnforceThreshold - 0.5)
            {
                winner = a;
                loser = d;
            }
            else if (score <= -EnforceThreshold + 0.5)
            {
                winner = d;
                loser = a;
            }
            else if (score <= -SurrenderThreshold &&
                MBRandom.RandomFloat < 0.15f * Eagerness(a))
            {
                winner = d;
                loser = a;
            }
            else if (score >= SurrenderThreshold &&
                MBRandom.RandomFloat < 0.15f * Eagerness(d))
            {
                winner = a;
                loser = d;
            }

            string message;

            if (winner != null)
            {
                if (TryEnforceDemands(war, winner, out message))
                {
                    AnnounceIfPlayerKingdom(winner, loser, message);
                }

                return;
            }

            bool whitePeace = false;

            if (Math.Abs(score) <= WhitePeaceBand &&
                age >= WhitePeaceMinDays &&
                MBRandom.RandomFloat < 0.05f * (Eagerness(a) + Eagerness(d)) * 0.5f)
            {
                whitePeace = true;
            }
            else if (age >= AiMaxWarDays && Math.Abs(score) < 40.0)
            {
                whitePeace = true;
            }

            if (whitePeace && TryWhitePeace(war, out message))
            {
                AnnounceIfPlayerKingdom(a, d, message);
            }
        }

        private static void AnnounceIfPlayerKingdom(
            Kingdom k1,
            Kingdom k2,
            string message)
        {
            Kingdom player = GetPlayerKingdom();

            if (player != null && (player == k1 || player == k2))
            {
                ShowMessage("CB: " + message);
            }
        }

        // A IA propoe termos ao soberano-jogador (a paz padrao do jogo esta
        // bloqueada para guerras registradas). Devolve true se mostrou algo.
        private bool OfferToPlayer(WarRecord war, Kingdom a, Kingdom d, double now)
        {
            Kingdom ai = IsPlayerSovereignOf(a) ? d : a;
            Kingdom player = IsPlayerSovereignOf(a) ? a : d;
            double last;

            if (_offerDay.TryGetValue(war.WarId, out last) &&
                now - last < AiOfferCooldownDays)
            {
                return false;
            }

            double age = GetWarAgeDays(war);
            double aiScore = ScoreFor(war, ai.StringId);

            if (age < 10.0)
            {
                return false;
            }

            if (aiScore >= EnforceThreshold - 0.5)
            {
                _offerDay[war.WarId] = now;

                InformationManager.ShowInquiry(new InquiryData(
                    "Termos de paz de " + ai.Name,
                    ai.Name + " venceu a guerra e exige: " +
                        PreviewDemands(war, ai) + "\nAceitar?",
                    true,
                    true,
                    "Aceitar",
                    "Recusar",
                    () =>
                    {
                        string result;
                        TryEnforceDemands(war, ai, out result);
                        ShowMessage("CB: " + result);
                    },
                    () => { }));

                return true;
            }

            if (Math.Abs(aiScore) <= WhitePeaceBand && age >= AiWhitePeaceOfferDays)
            {
                _offerDay[war.WarId] = now;

                InformationManager.ShowInquiry(new InquiryData(
                    "Proposta de paz branca",
                    ai.Name + " propoe paz branca apos " + age.ToString("0") +
                        " dias de guerra equilibrada. Aceitar?",
                    true,
                    true,
                    "Aceitar",
                    "Recusar",
                    () =>
                    {
                        string result;
                        TryWhitePeace(war, out result);
                        ShowMessage("CB: " + result);
                    },
                    () => { }));

                return true;
            }

            return false;
        }
    }
}
