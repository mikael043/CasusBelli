using System;
using System.Linq;
using CasusBelli.Managers.Titles;
using CasusBelli.Managers.Titles.Governments;
using TaleWorlds.CampaignSystem.ViewModelCollection.ArmyManagement;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CasusBelli
{
    internal static class MarshalCommand
    {
        internal const string Restriction = "O soberano e o marechal convocam grupos do reino. Um duque pode convocar os nobres dos condados do seu ducado.";

        internal static int ServiceDaysForLevyLevel(int level)
        {
            level = Math.Max(1, Math.Min(4, level));
            return level * 10;
        }

        internal static FeudalTitle ServiceLawTitle(Kingdom realm, Hero commander, Hero summoned)
        {
            if (realm == null || commander == null) return null;
            var titles = TitleManager.Instance;
            if (realm.Leader == commander || Campaign.Current?.GetCampaignBehavior<RoyalCouncil>()?.Member(realm, CouncilOffice.Marshal) == commander.StringId)
                return titles.GetSovereignTitle(realm);
            var county = titles.GetAllDeJure(summoned?.Clan).FirstOrDefault(t => t.TitleType == TitleType.County &&
                titles.GetDuchy(t)?.deJure == commander);
            return county == null ? titles.GetAllDeJure(commander).FirstOrDefault(t => t.TitleType == TitleType.Dukedom) : titles.GetDuchy(county);
        }

        internal static int ServiceDays(Kingdom realm, Hero commander, Hero summoned)
        {
            var title = ServiceLawTitle(realm, commander, summoned);
            var law = title?.Contract?.DemesneLaws?.FirstOrDefault(l => l.LawType == DemesneLaw.DemesneLawType.Levies);
            return ServiceDaysForLevyLevel(law?.Level ?? 2);
        }

        private static bool RealmCommand(Kingdom realm, Hero commander) =>
            realm.Leader == commander || Campaign.Current?.GetCampaignBehavior<RoyalCouncil>()?.Member(realm, CouncilOffice.Marshal) == commander.StringId;

        internal static bool MayMuster(Kingdom realm, Hero commander)
        {
            if (realm == null) return true;
            return commander?.Clan?.Kingdom == realm && !commander.IsPrisoner &&
                (RealmCommand(realm, commander) || TitleManager.Instance.GetAllDeJure(commander).Any(t => t.TitleType == TitleType.Dukedom));
        }

        internal static bool MayCall(Kingdom realm, Hero commander, MobileParty party)
        {
            if (realm == null) return true;
            if (!MayMuster(realm, commander) || party?.LeaderHero?.Clan?.Kingdom != realm) return false;
            if (RealmCommand(realm, commander) || party?.LeaderHero == commander) return true;
            var noble = party?.LeaderHero;
            if (noble == null || !noble.IsLord || noble.Clan?.Kingdom != realm) return false;
            var titles = TitleManager.Instance;
            return titles.GetAllDeJure(noble.Clan).Any(t => t.TitleType == TitleType.County &&
                titles.GetDuchy(t)?.deJure == commander);
        }

        internal static bool Filter(Kingdom realm, Hero commander, ref MBReadOnlyList<MobileParty> parties)
        {
            if (!MayMuster(realm, commander)) return false;
            if (realm == null || RealmCommand(realm, commander)) return true;
            if (parties == null) return true; // Player orders attach the validated parties after CreateArmy.
            parties = parties.Where(p => MayCall(realm, commander, p)).ToMBList();
            return parties.Any(p => p.LeaderHero != commander);
        }

        internal static float MobilizationScore(Hero hero)
        {
            // The native AI requires more than 100 clan influence before attempting a muster.
            float influence = Math.Max(0, hero.Clan.Influence);
            int typicalCost = Math.Max(1, Campaign.Current.Models.ArmyManagementCalculationModel.AverageCallToArmyCost);
            return (influence > 100 ? 20f : 0f) + Math.Min(30f, 5f * influence / typicalCost);
        }
    }

    [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel), "CanPlayerCreateArmy")]
    internal static class PlayerMarshalPermission
    {
        private static bool Prefix(ref bool __result, ref TextObject __0)
        {
            if (MarshalCommand.MayMuster(Clan.PlayerClan?.Kingdom, Hero.MainHero)) return true;
            __0 = new TextObject(MarshalCommand.Restriction);
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel), "CanLordCreateArmy")]
    internal static class LordMarshalPermission
    {
        private static bool Prefix(MobileParty __0, ref MBList<MobileParty> __1, ref bool __result)
        {
            if (MarshalCommand.MayMuster(__0?.MapFaction as Kingdom, __0?.LeaderHero)) return true;
            __1 = new MBList<MobileParty>();
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel), "CanLordCreateArmy")]
    internal static class DucalArmyCandidates
    {
        private static void Postfix(MobileParty __0, MBList<MobileParty> __1, ref bool __result)
        {
            if (!__result) return;
            var realm = __0.MapFaction as Kingdom;
            __1.RemoveAll(p => !MarshalCommand.MayCall(realm, __0.LeaderHero, p));
            __result = __1.Count > 0;
        }
    }

    [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.CreateArmy))]
    internal static class KingdomMarshalPermission
    {
        private static bool Prefix(Kingdom __instance, Hero __0, ref MBReadOnlyList<MobileParty> __3) => MarshalCommand.Filter(__instance, __0, ref __3);
    }

    [HarmonyPatch(typeof(Army), "Gather")]
    internal static class MarshalGatherPermission
    {
        private static bool Prefix(Army __instance, ref MBReadOnlyList<MobileParty> __1) =>
            MarshalCommand.Filter(__instance.Kingdom, __instance.LeaderParty?.LeaderHero, ref __1);
    }

    [HarmonyPatch(typeof(ArmyManagementVM), "ExecuteDone")]
    internal static class MarshalArmyOrder
    {
        private static bool Prefix(ArmyManagementVM __instance)
        {
            var party = MobileParty.MainParty;
            if (!__instance.PartiesInCart.Any(item => item.Party != party &&
                    (party.Army == null || item.Party.Army != party.Army) &&
                    !MarshalCommand.MayCall(Clan.PlayerClan?.Kingdom, Hero.MainHero, item.Party))) return true;
            InformationManager.DisplayMessage(new InformationMessage(MarshalCommand.Restriction));
            return false;
        }
    }

    [HarmonyPatch(typeof(ArmyManagementVM), "OnAddToCart")]
    internal static class DucalArmySelection
    {
        private static bool Prefix(ArmyManagementItemVM __0)
        {
            if (__0.Party == MobileParty.MainParty || MarshalCommand.MayCall(Clan.PlayerClan?.Kingdom, Hero.MainHero, __0.Party)) return true;
            InformationManager.DisplayMessage(new InformationMessage(MarshalCommand.Restriction));
            return false;
        }
    }

}