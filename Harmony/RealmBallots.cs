using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Election;

namespace CasusBelli
{
    [HarmonyPatch]
    internal static class ElectoratePatch
    {
        internal static bool Eligible(Clan clan, Kingdom realm) => clan?.Leader?.IsAlive == true &&
            clan.Kingdom == realm && RealmPeers.Rank(clan) > 0;
        private static IEnumerable<MethodBase> TargetMethods() => typeof(KingdomDecision).Assembly.GetTypes()
            .Where(t => typeof(KingdomDecision).IsAssignableFrom(t))
            .Select(t => AccessTools.DeclaredMethod(t,nameof(KingdomDecision.DetermineSupporters))).Where(m => m != null && !m.IsAbstract);
        private static void Postfix(KingdomDecision __instance,ref IEnumerable<Supporter> __result)
        {
            if (RealmPolitics.Current == null) return;
            var realm = __instance.Kingdom;
            var voters = __result.Where(v => Eligible(v.Clan,realm)).GroupBy(v => v.Clan).ToDictionary(g => g.Key,g => g.First());
            foreach (var clan in realm.Clans)
                if (Eligible(clan,realm) && !voters.ContainsKey(clan)) voters.Add(clan,new Supporter(clan));
            __result = voters.Values;
        }
    }

    [HarmonyPatch]
    internal static class NoblePreferencesPatch
    {
        private static IEnumerable<MethodBase> TargetMethods() => typeof(KingdomDecision).Assembly.GetTypes()
            .Where(t => typeof(KingdomDecision).IsAssignableFrom(t))
            .Select(t => AccessTools.DeclaredMethod(t,nameof(KingdomDecision.DetermineSupport),new[]{typeof(Clan),typeof(DecisionOutcome)})).Where(m => m != null && !m.IsAbstract);
        private static void Postfix(KingdomDecision __instance,Clan __0,DecisionOutcome __1,ref float __result)
        {
            if (RealmPolitics.Current == null || __0?.Leader == null || __0.Kingdom != __instance.Kingdom) return;
            if (__1 is KingSelectionKingdomDecision.KingSelectionDecisionOutcome crown && crown.King?.Clan?.Kingdom == __0.Kingdom)
            {
                __result += RealmLaw.Candidacy(__0,crown.King);
                return;
            }
            var beneficiary = (__1 as SettlementClaimantDecision.ClanAsDecisionOutcome)?.Clan ?? __1.SponsorClan;
            if (beneficiary?.Leader == null) return;
            var voter = __0.Leader;
            __result += .2f * voter.GetRelation(beneficiary.Leader) + (voter.Culture == beneficiary.Culture ? 4 : 0)
                - .25f * RealmPolitics.Controversy(beneficiary.Leader) + (beneficiary == __0 ? 5 * voter.GetTraitLevel(DefaultTraits.Calculating) : 0)
                + (beneficiary == __0.Kingdom.RulingClan ? .4f * (RealmPolitics.Loyalty(voter,__0.Kingdom)-50) : 0);
        }
    }

    [HarmonyPatch(typeof(KingdomElection),"DetermineOfficialSupport")]
    internal static class BallotCountPatch
    {
        private static readonly Action<DecisionOutcome,float> Points = AccessTools.PropertySetter(typeof(DecisionOutcome),"TotalSupportPoints").CreateDelegate(typeof(Action<DecisionOutcome,float>)) as Action<DecisionOutcome,float>;
        private static readonly Action<DecisionOutcome,float> Chance = AccessTools.PropertySetter(typeof(DecisionOutcome),"WinChance").CreateDelegate(typeof(Action<DecisionOutcome,float>)) as Action<DecisionOutcome,float>;
        internal static int Strength(Supporter voter) => Math.Max(0,(int)voter.SupportWeight - (int)Supporter.SupportWeights.StayNeutral);
        private static void Postfix(KingdomElection __instance,KingdomDecision ____decision)
        {
            if (RealmPolitics.Current == null) return;
            float total = 0;
            foreach (var outcome in __instance.PossibleOutcomes)
            {
                int points = outcome.SupporterList.Where(v => ElectoratePatch.Eligible(v.Clan,____decision.Kingdom))
                    .Sum(v => Strength(v) * RealmLaw.Weight(v.Clan));
                Points(outcome,points);
                total += points;
            }
            foreach (var outcome in __instance.PossibleOutcomes)
                Chance(outcome,total > 0 ? outcome.TotalSupportPoints / total : 0);
        }
    }

    [HarmonyPatch(typeof(KingdomElection),"ApplyChosenOutcome")]
    internal static class BallotResultPatch
    {
        private static void Postfix(KingdomElection __instance,KingdomDecision ____decision,DecisionOutcome ____chosenOutcome)
        {
            if (!(____decision is KingSelectionKingdomDecision) || RealmFactions.Current == null) return;
            var realm = ____decision.Kingdom;
            var runner = __instance.PossibleOutcomes.OfType<KingSelectionKingdomDecision.KingSelectionDecisionOutcome>()
                .Where(o => o != ____chosenOutcome && o.King?.Clan?.Kingdom == realm && o.TotalSupportPoints > 0)
                .OrderByDescending(o => o.TotalSupportPoints).FirstOrDefault();
            if (runner != null && runner.King.Clan != Clan.PlayerClan && runner.TotalSupportPoints >= ____chosenOutcome.TotalSupportPoints * .65f)
                RealmFactions.Current.Start(realm,runner.King,Disputes.CivilWar.Throne);
        }
        private static void Prefix(KingdomElection __instance,KingdomDecision ____decision,ref DecisionOutcome ____chosenOutcome)
        {
            var politics = RealmPolitics.Current;
            if (politics == null || ____chosenOutcome == null || __instance.PossibleOutcomes.Count == 0) return;
            var realm = ____decision.Kingdom;
            var options = __instance.PossibleOutcomes.OrderByDescending(o => o.TotalSupportPoints).ToList();
            bool overrideVote = ____chosenOutcome.TotalSupportPoints < options[0].TotalSupportPoints;
            bool binding = RealmPolitics.Authority(realm) < 60 || ____decision is KingSelectionKingdomDecision;
            if (overrideVote && binding) ____chosenOutcome = options[0];
            if (overrideVote && !binding)
            {
                RealmPolitics.Conduct(realm.Leader,realm,0,8);
                RealmPolitics.Shift(realm,-5);
            }
            else if (options[0].TotalSupportPoints > 0) RealmPolitics.Shift(realm,1);
            var lines = new List<string> { ____decision.GetGeneralTitle().ToString(), "Resultado: " + ____chosenOutcome.GetDecisionTitle() };
            if (overrideVote) lines.Add(binding ? "Prevaleceu a maioria vinculante." : "A Coroa contrariou a maioria (−5 de autoridade, +8 de controvérsia).");
            foreach (var option in options) lines.Add($"{option.GetDecisionTitle()} — {option.TotalSupportPoints:0} pontos");
            foreach (var clan in realm.Clans.Where(c => ElectoratePatch.Eligible(c,realm)))
            {
                var vote = options.FirstOrDefault(o => o.SupporterList.Any(v => v.Clan == clan && BallotCountPatch.Strength(v) > 0));
                lines.Add($"{clan.Leader.Name} ({clan.Name}, peso {RealmLaw.Weight(clan)}) — {vote?.GetDecisionTitle()?.ToString() ?? "abstenção"}");
            }
            politics.Write("ballot|" + realm.StringId,string.Join("\n",lines));
        }
    }
}