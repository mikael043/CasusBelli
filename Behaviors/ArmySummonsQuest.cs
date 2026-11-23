using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace CasusBelli
{
    public sealed class ArmySummonsBehavior : CampaignBehaviorBase
    {
        private double _nextSummons;
        public override void RegisterEvents() =>
            CampaignEvents.ArmyCreated.AddNonSerializedListener(this, OnArmyCreated);
        public override void SyncData(IDataStore store) =>
            store.SyncData("CasusBelli_NextArmySummons_v1", ref _nextSummons);

        private void OnArmyCreated(Army army)
        {
            var player = Hero.MainHero;
            var party = MobileParty.MainParty;
            var realm = army?.Kingdom;
            var commander = army?.LeaderParty?.LeaderHero;
            if (realm == null || commander == null || player == null || party == null || commander == player ||
                player.Clan?.Kingdom != realm || player.Clan.IsUnderMercenaryService || player.IsPrisoner ||
                party.LeaderHero != player || party.Army != null || party.MapEvent != null ||
                !MarshalCommand.MayCall(realm, commander, party) || !realm.FactionsAtWarWith.Any() ||
                CampaignTime.Now.ToDays < _nextSummons ||
                Campaign.Current.QuestManager.Quests.OfType<ArmySummonsQuest>().Any(q => q.IsOngoing) ||
                MBRandom.RandomFloat >= 0.7f) return;

            int cost = Campaign.Current.Models.ArmyManagementCalculationModel.CalculatePartyInfluenceCost(army.LeaderParty, party);
            if (cost < 0 || commander.Clan.Influence < cost) return;
            var quest = new ArmySummonsQuest(army, commander);
            quest.StartQuest();
            if (cost > 0) ChangeClanInfluenceAction.Apply(commander.Clan, -cost);
            _nextSummons = CampaignTime.DaysFromNow(7).ToDays;
            InformationManager.ShowInquiry(new InquiryData("Convocação para o exército", quest.Instructions.ToString(),
                true, true, "Vou me apresentar", "Recusar convocação", () => { }, quest.Decline));
        }
    }

    public sealed class ArmySummonsQuest : QuestBase
    {
        private const int RelationPenalty = 10;
        private const int DesertionRelationPenalty = 15;
        private const int CompletionRelationReward = 4;

        [SaveableProperty(100)] private Army SummoningArmy { get; set; }
        [SaveableProperty(101)] private bool ServiceStarted { get; set; }
        [SaveableProperty(102)] private CampaignTime ServiceEnds { get; set; }
        [SaveableProperty(103)] private int RequiredServiceDays { get; set; }
        [SaveableProperty(104)] private bool CompletionRewardGranted { get; set; }

        public ArmySummonsQuest(Army army, Hero commander)
            : base("cb_army_summons_" + Guid.NewGuid().ToString("N"), commander, CampaignTime.DaysFromNow(5), 0)
        {
            SummoningArmy = army;
            RequiredServiceDays = MarshalCommand.ServiceDays(army?.Kingdom, commander, Hero.MainHero);
            DebugLog.Log($"MilitaryService: convocacao criada commander={commander?.StringId} realm={army?.Kingdom?.StringId} serviceDays={RequiredServiceDays}");
            InitializeQuestOnCreation();
        }

        public override TextObject Title => new TextObject("Convocação para o exército");
        public override bool IsRemainingTimeHidden => false;
        internal TextObject Instructions => new TextObject(
            $"{QuestGiver.Name} convoca seu grupo. Encontre o exército no mapa e junte-se a ele em até cinco dias. " +
            $"Depois da apresentação, seu contrato exige {RequiredServiceDays} dias de serviço ativo. " +
            $"Recusar ou faltar custa {RelationPenalty} de relação com o comandante.");

        protected override void SetDialogs() { }

        protected override void InitializeQuestOnGameLoad()
        {
            if (RequiredServiceDays <= 0)
                RequiredServiceDays = MarshalCommand.ServiceDays(SummoningArmy?.Kingdom, QuestGiver, Hero.MainHero);
        }

        protected override void OnStartQuest() => AddLog(Instructions);

        protected override void RegisterEvents()
        {
            CampaignEvents.OnPartyJoinedArmyEvent.AddNonSerializedListener(this, Joined);
            CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, Dispersed);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, Check);
        }

        private bool StillValid => SummoningArmy != null && SummoningArmy.LeaderParty?.Army == SummoningArmy &&
            SummoningArmy.LeaderParty.LeaderHero == QuestGiver && QuestGiver.IsAlive && !QuestGiver.IsPrisoner &&
            !Hero.MainHero.IsPrisoner && Clan.PlayerClan.Kingdom == SummoningArmy.Kingdom &&
            !Clan.PlayerClan.IsUnderMercenaryService && SummoningArmy.Kingdom.FactionsAtWarWith.Any() &&
            MarshalCommand.MayCall(SummoningArmy.Kingdom, QuestGiver, MobileParty.MainParty);

        internal static bool ShouldPunishEarlyDeparture(bool serviceStarted, bool stillValid, bool inSummoningArmy) =>
            serviceStarted && stillValid && !inSummoningArmy;

        private void Joined(MobileParty party)
        {
            if (party == MobileParty.MainParty) Check();
        }

        private void Dispersed(Army army, Army.ArmyDispersionReason reason, bool valid)
        {
            if (IsOngoing && army == SummoningArmy) Cancel();
        }

        private void Cancel()
        {
            DebugLog.Log($"MilitaryService: dispensa legitima commander={QuestGiver?.StringId} started={ServiceStarted}");
            CompleteQuestWithCancel(new TextObject("A convocação perdeu a validade. Você foi dispensado sem penalidade."));
        }

        private void BeginService()
        {
            if (ServiceStarted) return;
            if (RequiredServiceDays <= 0)
                RequiredServiceDays = MarshalCommand.ServiceDays(SummoningArmy?.Kingdom, QuestGiver, Hero.MainHero);
            ServiceStarted = true;
            ServiceEnds = CampaignTime.DaysFromNow(RequiredServiceDays);
            ChangeQuestDueTime(ServiceEnds);
            DebugLog.Log($"MilitaryService: apresentado commander={QuestGiver?.StringId} serviceDays={RequiredServiceDays} ends={ServiceEnds.ToDays:0.00}");
            AddLog(new TextObject(
                $"Você se apresentou ao exército convocante. O serviço feudal agora exige {RequiredServiceDays} dias junto ao exército, salvo dispensa legítima."));
        }

        private void Check()
        {
            if (!IsOngoing) return;
            if (!StillValid)
            {
                Cancel();
                return;
            }

            bool inArmy = MobileParty.MainParty.Army == SummoningArmy;
            if (!ServiceStarted)
            {
                if (inArmy) BeginService();
                return;
            }

            if (ShouldPunishEarlyDeparture(ServiceStarted, true, inArmy))
            {
                DebugLog.Log($"MilitaryService: abandono commander={QuestGiver?.StringId} remaining={Math.Max(0, ServiceEnds.RemainingDaysFromNow):0.0}");
                PenalizeDesertion();
                CompleteQuestWithFail(new TextObject("Você abandonou o serviço feudal antes da dispensa."));
                return;
            }

            if (!ServiceEnds.IsFuture) FinishService();
        }

        private void FinishService()
        {
            DebugLog.Log($"MilitaryService: concluido commander={QuestGiver?.StringId} serviceDays={RequiredServiceDays}");
            RewardCompletion();
            AddLog(new TextObject($"Você cumpriu {RequiredServiceDays} dias de serviço feudal e foi dispensado."));
            CompleteQuestWithSuccess();
        }

        private void RewardCompletion()
        {
            if (CompletionRewardGranted) return;
            CompletionRewardGranted = true;
            RealmPolitics.Conduct(Hero.MainHero, QuestGiver.Clan?.Kingdom, 4, -2);
            ChangeRelationAction.ApplyPlayerRelation(QuestGiver, CompletionRelationReward);
        }

        internal void Decline()
        {
            Check();
            if (!IsOngoing) return;
            DebugLog.Log($"MilitaryService: convocacao recusada commander={QuestGiver?.StringId}");
            PenalizeRefusal();
            CompleteQuestWithFail(new TextObject("Você recusou a convocação."));
        }

        protected override void OnBeforeTimedOut(ref bool completeWithSuccess, ref bool doNotResolveTheQuest)
        {
            if (!ServiceStarted || !StillValid || MobileParty.MainParty.Army != SummoningArmy) return;
            RewardCompletion();
            completeWithSuccess = true;
        }

        protected override void OnTimedOut()
        {
            if (ServiceStarted)
            {
                if (ShouldPunishEarlyDeparture(true, StillValid, MobileParty.MainParty.Army == SummoningArmy))
                    PenalizeDesertion();
                return;
            }
            if (StillValid && MobileParty.MainParty.Army != SummoningArmy) PenalizeRefusal();
        }

        private void PenalizeRefusal()
        {
            RealmPolitics.Conduct(Hero.MainHero, QuestGiver.Clan?.Kingdom, -5, 3);
            ChangeRelationAction.ApplyPlayerRelation(QuestGiver, -RelationPenalty);
        }

        private void PenalizeDesertion()
        {
            RealmPolitics.Conduct(Hero.MainHero, QuestGiver.Clan?.Kingdom, -8, 6);
            ChangeRelationAction.ApplyPlayerRelation(QuestGiver, -DesertionRelationPenalty);
        }
    }
}