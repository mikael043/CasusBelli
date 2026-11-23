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
using TaleWorlds.MountAndBlade;

namespace CasusBelli
{
    // Dados do relatorio de batalha para ser mostrado 1h depois.
    public class BattleReportData
    {
        public string SettlementName;
        public string HeroName; // nome do reino inimigo (perdedor)
        public string WinnerKingdomName; // nome do reino vencedor
        public int Wounded;
        public int Killed;
        public List<Hero> CapturedLords;
        public double WarScoreChange;
        public bool PlayerWon;
        // True quando o jogador está liderando um exército (partido principal
        // tem army anexado). False = companhia pessoal.
        public bool PlayerInArmy;
        public bool PlayerOnArmy;
        // Nome do líder do exército inimigo (lado perdedor), ou vazio se não
        // houver um líder identificado.
        public string EnemyLeaderName;
    }

    // Dados do relatorio de cercos para ser mostrado 1h depois.
    public class SiegeReportData
    {
        public string SettlementName; // nome do feudo conquistado
        public string WinnerKingdomName; // nome do reino que conquistou
        public string LoserKingdomName; // nome do reino que perdeu
        public string GovernorName; // nome do governador do feudo conquistado
        public int Wounded;        // feridos do lado ATACANTE
        public int Killed;         // mortos do lado ATACANTE
        public int WoundedDefender; // feridos do lado DEFENSOR
        public int KilledDefender;  // mortos do lado DEFENSOR
        public double WarScoreChange;
        public bool PlayerWon;
        // true quando o jogador está no lado vencedor
        public bool PlayerInArmy;
    }
    // Baixas reais de um mapEvent de cercos, salvas para usar no relatorio.
    public class SiegeCasualtyData
    {
        public int WoundedAttacker;
        public int KilledAttacker;
        public int WoundedDefender;
        public int KilledDefender;
    }

    // Decomposicao do WarScore. Todos os valores sao da perspectiva do
    // ATACANTE da ficha de guerra (positivo = atacante vencendo).
    public class WarScoreBreakdown
    {
        public double Battles = 0.0;
        public double Raids;
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
    public partial class CasusBelliWarScoreBehavior : CampaignBehaviorBase
    {
        // ---------------- configuracao ----------------
        // Batalhas campais
        private static readonly double BattleCap = 75.0;
        private static readonly double PerBattleCap = 8.0;
        private static readonly double BattleGain = 35.0;       // pontos por 100% das tropas do perdedor
        private static readonly int MinBattleCasualties = 5; // mortos + feridos do perdedor
        private static readonly double MinKingdomTroops = 1500.0;

        // Cercos (feudo conquistado)
        private static readonly double TownPoints = 15.0;
        private static readonly double CastlePoints = 8.0;

        // Objetivo do casus belli: ganho por dia segurando o feudo-alvo
        private static readonly double ObjectiveTickPerDay = 1.0;
        private static readonly double ObjectiveTickCap = 25.0;

        // Prisioneiros (o rei inimigo cativo vale 20 pontos)
        private static readonly double HighTierLeaderPoints = 4.0;
        private static readonly double LeaderPoints = 3.0;
        private static readonly double LordPoints = 2.0;
        private static readonly double MinorPoints = 1.0;

        // Limiares das acoes
        public static readonly double EnforceThreshold = 100.0;
        public static readonly double WhitePeaceBand = 15.0;
        public static readonly double WhitePeaceMinDays = 45.0;

        // Exigencias de uma guerra sem feudo-alvo: parte do ouro do soberano
        private static readonly float IndemnityShare = 0.5f;
        private static readonly int IndemnityMax = 30000;

        // IA
        private static readonly double AiOfferCooldownDays = 30.0;

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

        // Baixas do ultimo mapEvent de cercos por feudo (StringId do Settlement).
        // Preenchido em OnMapEventEnded para eventos de cercos (MapEventSettlement != null)
        // e lido em QueueSiegeReport quando o feudo muda de dono.
        private Dictionary<string, SiegeCasualtyData> _siegeCasualtyCache =
            new Dictionary<string, SiegeCasualtyData>();

        // Guerra mostrada no menu e no botao WS.
        public string SelectedWarId = "";

        // Avisa a interface (icone WS) que algum valor mudou.
        public static event Action Changed;

        // Evento disparado quando ha um relatorio de batalha para ser mostrado.
        public static event Action<BattleReportData> BattleReportReady;

        // Evento disparado quando ha um relatorio de cercos para ser mostrado.
        public static event Action<SiegeReportData> SiegeReportReady;

        // Preenchido pela interface Gauntlet (CasusBelliWarScoreHud.cs)
        // quando ela esta ativa. Se for null, o jogo usa o menu de texto.
        public static Action WindowOpener;

        // true quando a interface (UIExtenderEx ou Gauntlet) criou o icone.
        // Usado so no diagnostico (casus_belli.ws_window).
        public static bool UiLoaded;

        // Ligue/desligue a interface do mapa via casus_belli.ws_ui.
        // Usado para diagnostico do tremer da camera: se o tremer
        // desaparecer com isso desligado, a causa esta na UI.
        public static bool UiEnabled = true;

        public static void RaiseChanged()
        {
            Action handler = Changed;

            if (handler != null)
            {
                handler();
            }
        }

        // Para teste via comando debug
        public void TriggerTestBattleReport(BattleReportData report)
        {
            DebugLog.Log("TriggerTestBattleReport CHAMADO");
            Action<BattleReportData> handler = BattleReportReady;
            if (handler != null)
            {
                handler(report);
                DebugLog.Log("TriggerTestBattleReport handler disparado");
            }
            else
            {
                DebugLog.Log("TriggerTestBattleReport handler e NULO");
            }
        }

        public CasusBelliWarScoreBehavior()
        {
            DebugLog.Log("CONSTRUTOR do WarScoreBehavior");
        }

        public override void RegisterEvents()
        {
            CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(this, OnRaidCompleted);
            DebugLog.Log("RegisterEvents do WarScoreBehavior INICIO");
            try {
                CampaignEvents.MapEventEnded.AddNonSerializedListener(
                    this,
                    OnMapEventEnded);
                DebugLog.Log("RegisterEvents: MapEventEnded OK");
            } catch (Exception ex) { DebugLog.Log("RegisterEvents EXCEPTION MapEventEnded: " + ex.Message); }

            try {
                CampaignEvents.OnSettlementOwnerChangedEvent
                    .AddNonSerializedListener(
                        this,
                        OnSettlementOwnerChanged);
                DebugLog.Log("RegisterEvents: SettlementOwnerChanged OK");
            } catch (Exception ex) { DebugLog.Log("RegisterEvents EXCEPTION SettlementOwnerChanged: " + ex.Message); }

            try {
                CampaignEvents.DailyTickEvent.AddNonSerializedListener(
                    this,
                    OnDailyTick);
                DebugLog.Log("RegisterEvents: DailyTick OK");
            } catch (Exception ex) { DebugLog.Log("RegisterEvents EXCEPTION DailyTick: " + ex.Message); }

            try {
                // Mantem o icone WS atualizado durante o dia (prisioneiros etc.).
                CampaignEvents.HourlyTickEvent.AddNonSerializedListener(
                    this,
                    OnHourlyTick);
                DebugLog.Log("RegisterEvents: HourlyTick OK");
            } catch (Exception ex) { DebugLog.Log("RegisterEvents EXCEPTION HourlyTick: " + ex.Message); }

            try {
                // Para processar fila de relatorios de batalha a cada tick (frame)
                CampaignEvents.TickEvent.AddNonSerializedListener(
                    this,
                    OnTick);
                DebugLog.Log("RegisterEvents: Tick OK");
            } catch (Exception ex) { DebugLog.Log("RegisterEvents EXCEPTION Tick: " + ex.Message); }

            DebugLog.Log("RegisterEvents do WarScoreBehavior FIM");
        }

        private void OnHourlyTick()
        {
            RaiseChanged();
        }

        private void OnTick(float dt)
        {
            // Chama RaiseChanged a cada tick para que a UI processe a fila de relatorios
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

            SyncRaidBans(dataStore);
            SyncWarContributions(dataStore);
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

            result.Raids = RaidCount(war, war.AttackerKingdomId) - RaidCount(war, war.DefenderKingdomId);
            double sum = result.Raids + result.Battles + result.Sieges +
                result.Objective + result.Prisoners;

            result.Total = war.IsTerritorial &&
                result.AttackerHoldsEnemyKing && !result.DefenderHoldsEnemyKing ? 100 : Clamp(sum, -100.0, 100.0);
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
            if (!war.IsTerritorial ||
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

            double days = (war.ArmisticeEnds >= 0 ? war.ArmisticeStarted : CampaignTime.Now.ToDays) - war.ObjectiveAchievedOnDay;

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
                    total += sign * 20;

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
            if (side == null)
            {
                DebugLog.Log("KingdomOfSide: side e null");
                return null;
            }
            if (side.LeaderParty == null)
            {
                DebugLog.Log("KingdomOfSide: side.LeaderParty e null");
                return null;
            }
            if (side.LeaderParty.MapFaction == null)
            {
                DebugLog.Log("KingdomOfSide: side.LeaderParty.MapFaction e null");
                return null;
            }

            Kingdom result = side.LeaderParty.MapFaction as Kingdom;
            DebugLog.Log("KingdomOfSide: MapFaction=" + side.LeaderParty.MapFaction.GetType().Name +
                " StringId=" + (side.LeaderParty.MapFaction.StringId ?? "null") +
                " IsKingdom=" + (result != null));
            return result;
        }

        private static int CountDead(MapEventSide side)
        {
            int dead = 0;
            foreach (MapEventParty party in side.Parties)
                if (party?.DiedInBattle != null) dead += party.DiedInBattle.TotalManCount;
            return dead;
        }

        private static int CountStartingTroops(MapEventSide side)
        {
            int start = 0;
            foreach (MapEventParty party in side.Parties)
                if (party != null) start += Math.Max(0, party.HealthyManCountAtStart);
            return start > 0 ? start : Math.Max(0, side.HealthyTroopCountAtMapEventStart);
        }

        // Pontos sempre positivos para o vencedor; AddPoints aplica o sinal do reino.
        // Feridos valem metade dos mortos. Vantagem numerica e eficiencia do vencedor
        // alteram a importancia da batalha, com limites para evitar distorcoes.
        internal static double CalculateBattlePoints(
            int loserDead, int loserWounded, int winnerDead, int winnerWounded,
            int winnerStarting, int loserStarting, double loserKingdomTroops)
        {
            int loserCasualties = Math.Max(0, loserDead) + Math.Max(0, loserWounded);
            if (loserCasualties < MinBattleCasualties) return 0.0;

            double loserLosses = Math.Max(0, loserDead) + Math.Max(0, loserWounded) * .5;
            double winnerLosses = Math.Max(0, winnerDead) + Math.Max(0, winnerWounded) * .5;
            double basePoints = loserLosses / Math.Max(MinKingdomTroops, Math.Max(0.0, loserKingdomTroops)) * BattleGain;

            // Vencer em inferioridade numerica concede ate +35%;
            // vencer com ampla superioridade concede no minimo 80% do valor-base.
            double strengthFactor = winnerStarting > 0 && loserStarting > 0
                ? Clamp(Math.Sqrt((double)loserStarting / winnerStarting), .8, 1.35) : 1.0;

            // Vitoria limpa vale mais; vitoria com perdas equivalentes vale o normal.
            double efficiency = loserLosses + winnerLosses > 0
                ? Clamp(.75 + .5 * loserLosses / (loserLosses + winnerLosses), .8, 1.2) : 1.0;

            return Clamp(basePoints * strengthFactor * efficiency, 0.0, PerBattleCap);
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
            DebugLog.Log("CasusBelli: OnMapEventEnded CHAMADO mapEvent=" +
                (mapEvent != null ? mapEvent.Id.ToString() : "null") +
                " IsFieldBattle=" + (mapEvent != null && mapEvent.IsFieldBattle) +
                " HasWinner=" + (mapEvent != null && mapEvent.HasWinner) +
                " MapEventSettlement=" + (mapEvent != null && mapEvent.MapEventSettlement != null
                    ? mapEvent.MapEventSettlement.StringId : "null"));

            try
            {
                TrackCommanders(mapEvent);
                // Salva as baixas de qualquer mapEvent de cercos (assalto)
                // para usar no relatorio de cercos quando o feudo mudar de dono.
                if (mapEvent != null && mapEvent.MapEventSettlement != null &&
                    mapEvent.HasWinner)
                {
                    CacheSiegeCasualties(mapEvent);
                }

                if (mapEvent == null ||
                    !mapEvent.IsFieldBattle ||
                    !mapEvent.HasWinner)
                {
                    DebugLog.Log("CasusBelli: OnMapEventEnded SAINDO cedo (não é batalha de campo ou sem vencedor)");
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

                DebugLog.Log("CasusBelli: OnMapEventEnded winnerKingdom=" +
                    (winnerKingdom != null ? winnerKingdom.StringId : "null") +
                    " loserKingdom=" + (loserKingdom != null ? loserKingdom.StringId : "null") +
                    " main=" + (main != null));

                if (winnerKingdom == null ||
                    loserKingdom == null ||
                    winnerKingdom == loserKingdom ||
                    main == null)
                {
                    DebugLog.Log("CasusBelli: OnMapEventEnded SAINDO cedo (kingdoms nulos, iguais ou main null)");
                    return;
                }

                WarRecord war;

                if (!main.TryGetActiveWarBetween(
                    winnerKingdom.StringId,
                    loserKingdom.StringId,
                    out war))
                {
                    DebugLog.Log("CasusBelli: OnMapEventEnded SAINDO cedo (NÃO há guerra ativa entre os reinos)");
                    return;
                }

                DebugLog.Log("CasusBelli: OnMapEventEnded guerra encontrada warId=" + war.WarId);

                int loserDead = CountDead(loser);
                int loserWounded = CountWounded(loser);
                int winnerDead = CountDead(winner);
                int winnerWounded = CountWounded(winner);
                int winnerStarting = CountStartingTroops(winner);
                int loserStarting = CountStartingTroops(loser);
                double kingdomTroops = KingdomTroops(loserKingdom);
                double points = CalculateBattlePoints(
                    loserDead, loserWounded, winnerDead, winnerWounded,
                    winnerStarting, loserStarting, kingdomTroops);

                DebugLog.Log("CasusBelli: OnMapEventEnded battle loserDead=" + loserDead +
                    " loserWounded=" + loserWounded + " winnerDead=" + winnerDead +
                    " winnerWounded=" + winnerWounded + " start=" + winnerStarting +
                    "/" + loserStarting + " kingdomTroops=" + kingdomTroops +
                    " points=" + points);
                if (points <= 0.0)
                {
                    DebugLog.Log("CasusBelli: OnMapEventEnded SAINDO cedo (baixas insuficientes)");
                    return;
                }

                AddPoints(war, winnerKingdom.StringId, points, true);

                // Verifica se o jogador é governante do reino vencedor (ou se o reino vencedor é o do jogador e ele é o líder)
                Kingdom playerKingdom = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
                bool isPlayerRuler = playerKingdom != null && playerKingdom.Leader == Hero.MainHero;

                if (isPlayerRuler && playerKingdom == winnerKingdom)
                {
                    // Prepara relatorio de batalha para ser mostrado 1h depois
                    QueueBattleReport(mapEvent, war, winnerKingdom, loserKingdom, points);
                }
            }
            catch (Exception ex)
            {
                // WarScore nunca pode derrubar o fim de uma batalha.
                DebugLog.Log("CasusBelli: OnMapEventEnded EXCEPTION: " +
                    ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static bool IsPlayerInArmy()
        {
            if (MobileParty.MainParty == null)
            {
                return false;
            }

            Army army = MobileParty.MainParty.Army;

            return army != null && army.LeaderParty != null && army.LeaderParty.LeaderHero == Hero.MainHero;
        }

        private static string EnemyArmyLeaderName(MapEvent mapEvent, Kingdom loserKingdom)
        {
            if (loserKingdom == null || mapEvent == null)
            {
                return "";
            }

            // Procura o líder do exército inimigo entre os participantes DESTE mapEvent
            foreach (PartyBase party in mapEvent.InvolvedParties)
            {
                if (party == null || party.MobileParty == null)
                {
                    continue;
                }

                MobileParty mp = party.MobileParty;

                if (!mp.IsLordParty || mp.MapFaction != loserKingdom)
                {
                    continue;
                }

                if (mp.Army == null || mp.Army.LeaderParty == null ||
                    mp.Army.LeaderParty.LeaderHero == null)
                {
                    continue;
                }

                return mp.Army.LeaderParty.LeaderHero.Name.ToString();
            }

            return "";
        }

        private static string KingdomName(Kingdom kingdom)
        {
            return kingdom != null ? kingdom.Name.ToString() : "Desconhecido";
        }

        private void QueueBattleReport(
            MapEvent mapEvent,
            WarRecord war,
            Kingdom winnerKingdom,
            Kingdom loserKingdom,
            double warScoreChange)
        {
            try
            {
                // Descobre o assentamento mais proximo (se houver)
                string settlementName = "";
                Settlement nearest = FindNearestSettlement(mapEvent.Position);
                if (nearest != null)
                {
                    settlementName = nearest.Name.ToString();
                }

                // Nome do reino perdedor (para o texto "forças de X")
                string heroName = KingdomName(loserKingdom);

                // Conta feridos e mortos do lado PERDEDOR
                BattleSideEnum losingSide = winnerKingdom.StringId == war.AttackerKingdomId
                    ? BattleSideEnum.Defender
                    : BattleSideEnum.Attacker;
                int wounded = CountWounded(mapEvent.GetMapEventSide(losingSide));
                int killed = CountDead(mapEvent.GetMapEventSide(losingSide));

                // Identifica lordes capturados (prisioneiros) desta batalha
                List<Hero> capturedLords = GetCapturedLordsFromBattle(mapEvent, loserKingdom, winnerKingdom);

                // Determina se o jogador venceu
                bool playerWon = false;
                Kingdom playerKingdom = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
                if (playerKingdom != null && playerKingdom == winnerKingdom)
                {
                    playerWon = true;
                }

                // Detecta se o jogador participou desta batalha (estava no mapEvent)
                // Verifica se o MainParty está em algum dos lados da batalha
                bool playerInThisBattle = false;
                if (MobileParty.MainParty != null && mapEvent != null)
                {
                    foreach (PartyBase party in mapEvent.InvolvedParties)
                    {
                        if (party != null && party.MobileParty == MobileParty.MainParty)
                        {
                            playerInThisBattle = true;
                            break;
                        }
                    }
                }

                // Detecta se o jogador estava liderando o exército VENCEDOR nesta batalha
                // (apenas se playerWon e playerInThisBattle e player é o líder do exército)
                bool playerLedWinningArmy = false;
                if (playerInThisBattle)
                {
                    Army army = MobileParty.MainParty.Army;
                    if (army != null && army.LeaderParty != null && army.LeaderParty.LeaderHero == Hero.MainHero)
                    {
                        playerLedWinningArmy = true;
                    }
                }

                // playerInArmy para o relatório: true apenas se o jogador liderou o exército vencedor
                // false = "Sua companhia pessoal" (ou exército do vassalo se perdeu)
                bool playerInArmy = playerLedWinningArmy;
                string enemyLeaderName = EnemyArmyLeaderName(mapEvent, loserKingdom);

                // Cria o relatorio
                BattleReportData report = new BattleReportData
                {
                    SettlementName = settlementName,
                    HeroName = heroName,
                    WinnerKingdomName = KingdomName(winnerKingdom),
                    Wounded = wounded,
                    Killed = killed,
                    CapturedLords = capturedLords,
                    WarScoreChange = warScoreChange,
                    PlayerWon = playerWon,
                    PlayerInArmy = playerInArmy,
                    PlayerOnArmy = playerInThisBattle && MobileParty.MainParty.Army != null && !playerLedWinningArmy,
                    EnemyLeaderName = enemyLeaderName
                };

                // Dispara evento para a interface mostrar 1h depois
                Action<BattleReportData> handler = BattleReportReady;
                if (handler != null)
                {
                    handler(report);
                }
            }
            catch (Exception)
            {
                // Falha silenciosa no relatorio - nao pode derrubar o jogo
            }
        }

        private void QueueSiegeReport(
            Settlement settlement,
            WarRecord war,
            string winnerKingdomId,
            string loserKingdomId,
            Hero capturerHero)
        {
            try
            {
                Kingdom winnerKingdom = FindKingdom(winnerKingdomId);
                Kingdom loserKingdom = FindKingdom(loserKingdomId);

                if (winnerKingdom == null || loserKingdom == null)
                {
                    return;
                }

                // Determina se o jogador está no lado vencedor
                bool playerWon = false;
                Kingdom playerKingdom = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
                if (playerKingdom != null && playerKingdom == winnerKingdom)
                {
                    playerWon = true;
                }

                // Only attribute the conquest to the player if the game identifies the player
                // as the actual capturer. Mere membership in an army is not siege participation.
                bool playerInArmy = playerWon && capturerHero == Hero.MainHero;

                // Nome do governador do feudo conquistado (apenas governador oficial, se houver)
                string governorName = "";
                if (settlement != null && settlement.Town != null && settlement.Town.Governor != null)
                {
                    governorName = settlement.Town.Governor.Name.ToString();
                }

                // Conta feridos e mortos do cerco.
                // Prefere as baixas REAIS do mapEvent de assalto (cacheadas em
                // OnMapEventEnded). Se não houver (ex.: feudo conquistado sem
                // batalha de assalto registrada), usa estimativa simples.
                int woundedAttacker = 0;
                int killedAttacker = 0;
                int woundedDefender = 0;
                int killedDefender = 0;
                if (settlement != null)
                {
                    SiegeCasualtyData cached;
                    if (_siegeCasualtyCache.TryGetValue(settlement.StringId, out cached))
                    {
                        woundedAttacker = cached.WoundedAttacker;
                        killedAttacker = cached.KilledAttacker;
                        woundedDefender = cached.WoundedDefender;
                        killedDefender = cached.KilledDefender;
                    }
                    else if (settlement.Town != null)
                    {
                        int totalWounded = (int)(settlement.Town.Security * 50);
                        int totalKilled = (int)(settlement.Town.Prosperity * 0.5);
                        woundedAttacker = (int)(totalWounded * 0.6);
                        killedAttacker = (int)(totalKilled * 0.6);
                        woundedDefender = totalWounded - woundedAttacker;
                        killedDefender = totalKilled - killedAttacker;
                    }
                }

                // WarScore change - usar os pontos do cerco
                double basePoints = settlement.IsTown ? TownPoints : CastlePoints;
                double warScoreChange;
                Kingdom playerKingdomForScore = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
                if (playerKingdomForScore != null && playerKingdomForScore.StringId == winnerKingdomId)
                {
                    warScoreChange = basePoints;
                }
                else if (playerKingdomForScore != null && playerKingdomForScore.StringId == loserKingdomId)
                {
                    warScoreChange = -basePoints;
                }
                else
                {
                    warScoreChange = winnerKingdomId == war.AttackerKingdomId
                        ? basePoints
                        : -basePoints;
                }

                SiegeReportData report = new SiegeReportData
                {
                    SettlementName = settlement.Name.ToString(),
                    WinnerKingdomName = winnerKingdom.Name.ToString(),
                    LoserKingdomName = loserKingdom.Name.ToString(),
                    GovernorName = governorName,
                    Wounded = woundedAttacker,
                    Killed = killedAttacker,
                    WoundedDefender = woundedDefender,
                    KilledDefender = killedDefender,
                    WarScoreChange = warScoreChange,
                    PlayerWon = playerWon,
                    PlayerInArmy = playerInArmy
                };

                Action<SiegeReportData> handler = SiegeReportReady;
                if (handler != null)
                {
                    handler(report);
                }
            }
            catch (Exception)
            {
                // Falha silenciosa
            }
        }

        private static int CountWounded(MapEventSide side)
        {
            int wounded = 0;

            foreach (MapEventParty party in side.Parties)
            {
                if (party != null && party.WoundedInBattle != null)
                {
                    wounded += party.WoundedInBattle.TotalManCount;
                }
            }

            return wounded;
        }

        // Salva as baixas reais de um mapEvent de assalto de cercos na cache,
        // associadas ao StringId do feudo. Lido em QueueSiegeReport.
        private void CacheSiegeCasualties(MapEvent mapEvent)
        {
            try
            {
                Settlement settlement = mapEvent.MapEventSettlement;
                string key = settlement.StringId;

                BattleSideEnum winningSide = mapEvent.WinningSide;
                BattleSideEnum losingSide = winningSide == BattleSideEnum.Attacker
                    ? BattleSideEnum.Defender
                    : BattleSideEnum.Attacker;

                MapEventSide winner = mapEvent.GetMapEventSide(winningSide);
                MapEventSide loser = mapEvent.GetMapEventSide(losingSide);

                int wAttacker = CountWounded(mapEvent.GetMapEventSide(BattleSideEnum.Attacker));
                int kAttacker = CountDead(mapEvent.GetMapEventSide(BattleSideEnum.Attacker));
                int wDefender = CountWounded(mapEvent.GetMapEventSide(BattleSideEnum.Defender));
                int kDefender = CountDead(mapEvent.GetMapEventSide(BattleSideEnum.Defender));

                _siegeCasualtyCache[key] = new SiegeCasualtyData
                {
                    WoundedAttacker = wAttacker,
                    KilledAttacker = kAttacker,
                    WoundedDefender = wDefender,
                    KilledDefender = kDefender
                };

                DebugLog.Log("CasusBelli: CacheSiegeCasualties settlement=" + key +
                    " wAtt=" + wAttacker + " kAtt=" + kAttacker +
                    " wDef=" + wDefender + " kDef=" + kDefender);
            }
            catch (Exception ex)
            {
                DebugLog.Log("CasusBelli: CacheSiegeCasualties EXCEPTION: " + ex.Message);
            }
        }

        private static Settlement FindNearestSettlement(CampaignVec2 position)
        {
            Settlement nearest = null;
            float bestDist = float.MaxValue;

            foreach (Settlement settlement in Settlement.All)
            {
                if (settlement == null || !settlement.IsVisible || settlement.IsHideout)
                {
                    continue;
                }

                CampaignVec2 settlementPos = settlement.GatePosition;
                float dist = position.Distance(settlementPos);

                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = settlement;
                }
            }

            return nearest;
        }

        private static List<Hero> GetCapturedLordsFromBattle(MapEvent mapEvent, Kingdom loserKingdom, Kingdom winnerKingdom)
        {
            List<Hero> captured = new List<Hero>();

            if (loserKingdom == null)
            {
                return captured;
            }

            // Verifica herois do reino perdedor que foram feitos prisioneiros NESTA batalha
            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                if (hero == null || !hero.IsPrisoner)
                {
                    continue;
                }

                // Deve ser do reino perdedor
                if (hero.Clan == null || hero.Clan.Kingdom != loserKingdom)
                {
                    continue;
                }

                // Deve ser um lorde (lider de cla ou tier >= 3)
                bool isLord = hero.Clan.Leader == hero || hero.Clan.Tier >= 3;
                if (!isLord)
                {
                    continue;
                }

                // Verifica se foi capturado recentemente (nesta batalha)
                // Um jeito simples: verifica se o captor esta envolvido na batalha
                PartyBase holder = hero.PartyBelongedToAsPrisoner;
                if (holder != null)
                {
                    Kingdom captorKingdom = holder.MapFaction as Kingdom;
                    if (captorKingdom != null && (captorKingdom == winnerKingdom || captorKingdom.IsAtWarWith(loserKingdom)))
                    {
                        captured.Add(hero);
                    }
                }
            }

            return captured;
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

                _contributions.Add(Entry(war, newKingdomId, "fort", settlement.StringId));
                AddPoints(
                    war,
                    newKingdomId,
                    settlement.IsTown ? TownPoints : CastlePoints,
                    false);

                // Só mostra relatório de cerco se o jogador está envolvido nesta guerra
                Kingdom playerKingdom = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
                if (playerKingdom == null ||
                    (playerKingdom.StringId != war.AttackerKingdomId && playerKingdom.StringId != war.DefenderKingdomId))
                {
                    return;
                }

                // Prepara relatorio de cerco para ser mostrado 1h depois
                QueueSiegeReport(settlement, war, newKingdomId, oldKingdomId, capturerHero);
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
        private Settlement GetDemandTarget(
            WarRecord war,
            Kingdom winner,
            Kingdom loser)
        {
            if (!war.IsTerritorial ||
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
            return PreviewSettlementTerms(war, winner) + CivilSettlementTerms(war, winner);
        }

        private static string CivilSettlementTerms(WarRecord war, Kingdom winner)
        {
            if (war == null || winner == null) return "";
            var movement = RealmFactions.Current?.Find(winner);
            if (movement?.Rebels == null ||
                !((war.AttackerKingdomId == movement.Rebels.StringId && war.DefenderKingdomId == movement.Realm.StringId) ||
                  (war.DefenderKingdomId == movement.Rebels.StringId && war.AttackerKingdomId == movement.Realm.StringId))) return "";
            if (winner == movement.Realm) return " A Coroa mantém o poder e os clãs da liga regressam ao reino.";
            if (movement.Demand == Disputes.CivilWar.Throne) return $" A casa de {movement.Pretender.Name} assume o trono e os clãs voltam a se reunir.";
            if (movement.Demand == Disputes.CivilWar.Independence) return " A liga passa a ser um reino independente.";
            return $" A Coroa concede {movement.Demand} e os clãs regressam ao reino.";
        }

        private string PreviewSettlementTerms(WarRecord war, Kingdom winner)
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

            if (AttackerClaim(war, winner))
                return "A guerra termina com as posses atuais do feudo reivindicado.";
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
            if (!CanEnforce(war, winner, out message)) return false;
            return SettleDemands(war, winner, out message);
        }

        private bool SettleDemands(WarRecord war, Kingdom winner, out string message)
        {
            message = "";
            Kingdom loser = winner != null ? OtherSide(war, winner) : null;

            if (!CanSurrender(war, winner, out message) || loser == null)
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

                RoyalSettlement.ConfirmPossession(target);
                text = loser.Name + " cedeu " + target.Name + " a " +
                    winner.Name + ".";
            }
            else
            {
                if (AttackerClaim(war, winner))
                {
                    var held = Settlement.Find(war.TargetSettlementId);
                    if (held?.OwnerClan?.Kingdom == winner) RoyalSettlement.ConfirmPossession(held);
                }
                int amount = AttackerClaim(war, winner) ? 0 : GetIndemnity(loserLeader);

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

            text += CivilSettlementTerms(war, winner);
            war.Outcome = winner.StringId == war.AttackerKingdomId ? "VICTORY" : "DEFENDER_VICTORY";
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

            return SettleDemands(war, winner, out message);
        }

        public bool TryWhitePeace(WarRecord war, Kingdom proposer, out string message)
        {
            if (!CanWhitePeace(war, proposer, out message)) return false;
            Kingdom a = FindKingdom(war.AttackerKingdomId);
            Kingdom d = FindKingdom(war.DefenderKingdomId);

            if (a == null || d == null || !a.IsAtWarWith(d))
            {
                message = "guerra indisponivel";
                return false;
            }

            war.Outcome = "WHITE_PEACE";
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

            if (war.IsTerritorial)
            {
                Settlement target = Settlement.Find(war.TargetSettlementId);

                text.Append("Casus belli: " + war.Description +
                    (target != null ? " (alvo: " + target.Name + ")" : "") +
                    (viewerIsAttacker ? "" : " - voce e o defensor") + "\n");
            }
            else
            {
                text.Append("Casus belli: " + war.Description + "\n");
            }

            text.Append("Duracao: " + GetWarAgeDays(war).ToString("0") + " dias\n\n");

            double total = b.Total * sign;

            text.Append("WARSCORE: " + FormatPercent(total) + "\n");
            text.Append(BuildBar(total) + "\n");
            text.Append("(esquerda = derrota, direita = vitoria)\n\n");

            text.Append("Fatores:\n");
            text.Append(FactorLine("Batalhas (max 75)", b.Battles * sign));
            text.Append(FactorLine("Saques", b.Raids * sign));
            text.Append(FactorLine("Cercos", b.Sieges * sign));

            if (war.IsTerritorial)
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
                text.Append("  Voce mantem o soberano inimigo cativo: +20 pontos.\n");
            }
            else if (theyHoldKing)
            {
                text.Append("  O inimigo mantem o seu soberano cativo: -20 pontos.\n");
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

            _contributions.RemoveWhere(k => !activeIds.Contains(k.Split('|')[0]));
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
                // Saneia saves antigos ou mudancas de posse nao notificadas:
                // o bonus de ocupacao precisa recomeçar se o alvo for perdido.
                if (war.IsTerritorial && war.ObjectiveAchievedOnDay >= 0.0)
                {
                    Settlement target = Settlement.Find(war.TargetSettlementId);
                    if (target != null && OwnerKingdomId(target) != war.AttackerKingdomId)
                        war.ObjectiveAchievedOnDay = -1.0;
                }
                GetState(war, true);

                Kingdom a = FindKingdom(war.AttackerKingdomId);
                Kingdom d = FindKingdom(war.DefenderKingdomId);

                if (war.ArmisticeEnds >= 0)
                {
                    TickArmistice(war, a, d, now);
                    continue;
                }
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

                if (TryStartArmistice(war, a, d, now)) continue;

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
                    samePair)
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
            Kingdom ahead = ScoreFor(war, a.StringId) >= 0 ? a : d;
            if (CanEnforce(war, ahead, out _) && TryEnforceDemands(war, ahead, out var result))
                AnnounceIfPlayerKingdom(a, d, result);
            else if (WantsWhite(war, ahead) && TryWhitePeace(war, ahead, out var peace))
                AnnounceIfPlayerKingdom(a, d, peace);
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
            double aiScore = ScoreFor(war, ai.StringId);
            if (Math.Abs(aiScore) >= EnforceThreshold)
            {
                Kingdom winner = aiScore > 0 ? ai : player;
                if (TryEnforceDemands(war, winner, out var result)) ShowMessage("CB: " + result);
                return true;
            }
            double last;

            if (_offerDay.TryGetValue(war.WarId, out last) &&
                now - last < AiOfferCooldownDays)
            {
                return false;
            }

            double age = GetWarAgeDays(war);
            if (age < 10.0)
            {
                return false;
            }

            if (CanEnforce(war, player, out _))
            {
                _offerDay[war.WarId] = now;
                InformationManager.ShowInquiry(new InquiryData(
                    "Oferta de rendição", ai.Name + " aceita suas exigências. " + PreviewDemands(war, player),
                    true, true, "Aceitar rendição", "Continuar a guerra",
                    () =>
                    {
                        if (IsPlayerSovereignOf(player) && TryEnforceDemands(war, player, out var result))
                            ShowMessage("CB: " + result);
                    }, () => { }));
                return true;
            }

            if (aiScore >= AcceptanceThreshold(war, player))
            {
                _offerDay[war.WarId] = now;

                InformationManager.ShowInquiry(new InquiryData(
                    "Termos de paz de " + ai.Name,
                    ai.Name + " propõe encerrar a guerra nos seguintes termos: " +
                        PreviewDemands(war, ai) + "\nAceitar?",
                    true,
                    true,
                    "Aceitar",
                    "Recusar",
                    () =>
                    {
                        string result;
                        TrySurrender(war, player, out result);
                        ShowMessage("CB: " + result);
                    },
                    () => { }));

                return true;
            }

            if (WantsWhite(war, ai))
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
                        TryWhitePeace(war, player, out result);
                        ShowMessage("CB: " + result);
                    },
                    () => { }));

                return true;
            }

            return false;
        }
    }
}