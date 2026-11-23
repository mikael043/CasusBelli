using System;
using System.Collections.Generic;
using System.Linq;
using CasusBelli.Managers.Titles;
using CasusBelli.Managers.Titles.Governments;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace CasusBelli
{
    [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel),"CalculatePartyInfluenceCost")]
    internal static class LevyCostPatch
    {
        private static void Postfix(MobileParty __0,MobileParty __1,ref int __result)
        {
            if (__result <= 0 || RealmPolitics.Current == null) return;
            var calling = __0?.LeaderHero?.Clan;
            var called = __1?.LeaderHero?.Clan;
            var realm = calling?.Kingdom;
            if (realm == null || called?.Kingdom != realm || called == calling || called.IsUnderMercenaryService) return;
            var title = TitleManager.Instance.GetHighestTitle(called.Leader) ?? TitleManager.Instance.GetSovereignTitle(realm);
            float levy = title?.Contract?.DemesneLaws?.FirstOrDefault(l => l.LawType == DemesneLaw.DemesneLawType.Levies)?.LevyModifier ?? 0;
            __result = Math.Max(1,(int)Math.Round(__result * (1.25f - RealmPolitics.Authority(realm)/200f) / (1 + levy)));
        }
    }

    [HarmonyPatch(typeof(KingSelectionKingdomDecision),nameof(KingSelectionKingdomDecision.DetermineInitialCandidates))]
    internal static class RoyalCandidatesPatch
    {
        private static void Postfix(KingSelectionKingdomDecision __instance,ref IEnumerable<DecisionOutcome> __result)
        {
            if (RealmPolitics.Current == null) return;
            var eligible = __result.OfType<KingSelectionKingdomDecision.KingSelectionDecisionOutcome>()
                .Where(o => o.King?.Clan?.Leader == o.King && o.King != null && !o.King.IsChild && o.King.IsAlive &&
                    ElectoratePatch.Eligible(o.King.Clan,__instance.Kingdom)).ToList();
            var peers = eligible.Where(o => RealmPeers.Rank(o.King.Clan) == 2).ToList();
            __result = peers.Count > 0 ? peers : eligible;
        }
    }

    [HarmonyPatch(typeof(KingdomElection),nameof(KingdomElection.GetWinChanceWithPlayerSupport))]
    internal static class BallotPreviewPatch
    {
        private static bool Prefix(KingdomElection __instance, DecisionOutcome __0, Supporter.SupportWeights __1, ref float __result)
        {
            if (RealmPolitics.Current == null) return true;
            float total = 0, selected = 0;
            foreach (var outcome in __instance.PossibleOutcomes)
            {
                float points = outcome.TotalSupportPoints;
                foreach (var previous in outcome.SupporterList.Where(v => v.Clan == Clan.PlayerClan))
                    points -= BallotCountPatch.Strength(previous) * RealmLaw.Weight(previous.Clan);
                points = Math.Max(0,points);
                if (outcome == __0) selected = points += Math.Max(0,(int)__1 - 1) * RealmLaw.Weight(Clan.PlayerClan);
                total += points;
            }
            __result = total > 0 ? selected / total : 0;
            return false;
        }
    }
}