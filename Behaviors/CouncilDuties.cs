using System;
using System.Collections.Generic;
using System.Linq;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli
{
    internal static class CouncilDuties
    {
        internal static Hero Officer(Kingdom realm, CouncilOffice office)
        {
            if (realm?.Leader == null || Campaign.Current?.CampaignBehaviorManager == null) return null;
            var id = Campaign.Current?.GetCampaignBehavior<RoyalCouncil>()?.Member(realm,office);
            if (id == null) return null;
            return CouncilSelection.Court(realm).FirstOrDefault(h => h.StringId == id && CouncilSelection.Eligible(realm,h,office));
        }

        internal static float ClaimYears(Hero claimant)
        {
            var realm = claimant?.Clan?.Kingdom;
            var envoy = realm?.Leader == claimant && claimant != null ? Officer(realm,CouncilOffice.Chancellor) : null;
            return envoy == null ? 1f : Math.Max(.5f, 1.15f - envoy.GetSkillValue(DefaultSkills.Charm) / 400f);
        }

        internal static void Accounts(Kingdom realm)
        {
            var steward = Officer(realm,CouncilOffice.Steward);
            if (steward == null) return;
            foreach (var clan in realm.Clans.Where(c => c != realm.RulingClan && !c.IsEliminated && !c.IsUnderMercenaryService && c.Leader != null))
            {
                int due = 25 * clan.Fiefs.Count;
                int paid = Math.Min(due, Math.Max(0, clan.Leader.Gold));
                clan.Leader.ChangeHeroGold(-paid);
                realm.KingdomBudgetWallet += paid;
                if (paid < due && clan.Influence > 0)
                    ChangeClanInfluenceAction.Apply(clan,-Math.Min(1f,clan.Influence));
            }
            realm.KingdomBudgetWallet -= 20 * realm.RulingClan.Fiefs.Count;
            foreach (var army in realm.Armies)
            {
                var leader = army.LeaderParty?.LeaderHero;
                if (leader == null || realm.KingdomBudgetWallet < 100) continue;
                realm.KingdomBudgetWallet -= 100;
                leader.ChangeHeroGold(100);
            }
        }

        internal static void Intelligence(Kingdom realm, List<string> known, HashSet<string> live)
        {
            var spy = Officer(realm,CouncilOffice.Spymaster);
            foreach (var title in TitleManager.Instance.AllTitles.Where(t => t.deJure?.Clan?.Kingdom == realm))
                foreach (var claim in title.OngoingClaims)
                {
                    if (claim.Key?.Clan?.Kingdom == realm && claim.Key.Clan == realm.RulingClan) continue;
                    var key = $"{realm.StringId}|{title.StringId}|{claim.Key.StringId}|{(long)claim.Value.ToDays}";
                    live.Add(key);
                    if (spy == null || known.Contains(key) || MBRandom.RandomFloat >= .1f +
                        Math.Min(.65f,(spy.GetSkillValue(DefaultSkills.Roguery) + spy.GetSkillValue(DefaultSkills.Scouting)) / 650f)) continue;
                    known.Add(key);
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(realm.Leader,claim.Key,-2,false);
                    if (realm.Leader == Hero.MainHero)
                        InformationManager.DisplayMessage(new InformationMessage($"{spy.Name} descobriu que {claim.Key.Name} fabrica uma reivindicação sobre {title.FullName}."));
                }
        }
    }
}
