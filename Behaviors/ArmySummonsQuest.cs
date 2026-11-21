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
                MBRandom.RandomFloat >= 0.5f) return;

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
        private const int RelationPenalty = 5;
        [SaveableProperty(100)] private Army SummoningArmy { get; set; }
        public ArmySummonsQuest(Army army, Hero commander)
            : base("cb_army_summons_" + Guid.NewGuid().ToString("N"), commander, CampaignTime.DaysFromNow(5), 0)
        {
            SummoningArmy = army;
            InitializeQuestOnCreation();
        }
        public override TextObject Title => new TextObject("Convocação para o exército");
        public override bool IsRemainingTimeHidden => false;
        internal TextObject Instructions => new TextObject($"{QuestGiver.Name} convoca seu grupo. Encontre o exército no mapa e junte-se a ele em até cinco dias. Recusar ou faltar custa {RelationPenalty} de relação com o comandante.");
        protected override void SetDialogs() { }
        protected override void InitializeQuestOnGameLoad() { }
        protected override void OnStartQuest() => AddLog(Instructions);
        protected override void RegisterEvents()
        {
            CampaignEvents.OnPartyJoinedArmyEvent.AddNonSerializedListener(this, Joined);
            CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, Dispersed);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, Check);
        }

        private bool StillValid => SummoningArmy?.LeaderParty?.Army == SummoningArmy && SummoningArmy != null &&
            SummoningArmy.LeaderParty.LeaderHero == QuestGiver && QuestGiver.IsAlive && !QuestGiver.IsPrisoner &&
            !Hero.MainHero.IsPrisoner && Clan.PlayerClan.Kingdom == SummoningArmy.Kingdom &&
            !Clan.PlayerClan.IsUnderMercenaryService && SummoningArmy.Kingdom.FactionsAtWarWith.Any() &&
            MarshalCommand.MayCall(SummoningArmy.Kingdom, QuestGiver, MobileParty.MainParty);

        private void Joined(MobileParty party)
        {
            if (party == MobileParty.MainParty) Check();
        }
        private void Dispersed(Army army, Army.ArmyDispersionReason reason, bool valid)
        {
            if (IsOngoing && army == SummoningArmy) Cancel();
        }
        private void Cancel() => CompleteQuestWithCancel(new TextObject("A convocação perdeu a validade. Você foi dispensado."));
        private void Check()
        {
            if (!IsOngoing) return;
            if (!StillValid) Cancel();
            else if (MobileParty.MainParty.Army == SummoningArmy)
            {
                AddLog(new TextObject("Você se apresentou ao exército convocante."));
                CompleteQuestWithSuccess();
            }
        }
        internal void Decline()
        {
            Check();
            if (!IsOngoing) return;
            Penalize();
            CompleteQuestWithFail(new TextObject("Você recusou a convocação."));
        }
        protected override void OnTimedOut()
        {
            if (StillValid && MobileParty.MainParty.Army != SummoningArmy) Penalize();
        }
        private void Penalize()
        {
            RealmPolitics.Conduct(Hero.MainHero,QuestGiver.Clan?.Kingdom,-5,3);
            ChangeRelationAction.ApplyPlayerRelation(QuestGiver, -RelationPenalty);
        }
    }
}
