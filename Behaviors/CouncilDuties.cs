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
            int income = 0, arrears = 0, armyExpense = 0;
            var unpaid = new List<string>();
            float efficiency = .75f + Math.Min(300,steward.GetSkillValue(DefaultSkills.Steward)) / 400f;
            foreach (var clan in realm.Clans.Where(c => c != realm.RulingClan && !c.IsEliminated && !c.IsUnderMercenaryService && c.Leader != null))
            {
                int due = 0;
                foreach (var fief in clan.Fiefs)
                {
                    var contract = TitleManager.Instance.GetTitle(fief.Settlement)?.Contract;
                    float modifier = contract?.DemesneLaws?.FirstOrDefault(l => l.LawType ==
                        Managers.Titles.Governments.DemesneLaw.DemesneLawType.Taxation)?.TaxModifier ?? 0;
                    due += (int)Math.Round(25 * (1 + modifier) * efficiency * (.75f + RealmPolitics.Authority(realm)/200f));
                }
                int paid = Math.Min(due, Math.Max(0, clan.Leader.Gold));
                clan.Leader.ChangeHeroGold(-paid);
                realm.KingdomBudgetWallet += paid;
                income += paid;
                arrears += due-paid;
                if (paid < due)
                {
                    unpaid.Add($"{clan.Name} ({due-paid})");
                    RealmPolitics.Conduct(clan.Leader,realm,-2,1);
                }
                else if (due > 0) RealmPolitics.Conduct(clan.Leader,realm,1,-1);
                if (paid < due && clan.Influence > 0)
                    ChangeClanInfluenceAction.Apply(clan,-Math.Min(1f,clan.Influence));
            }
            int demesne = 20 * realm.RulingClan.Fiefs.Count;
            realm.KingdomBudgetWallet -= demesne;
            foreach (var army in realm.Armies)
            {
                var leader = army.LeaderParty?.LeaderHero;
                if (leader == null || realm.KingdomBudgetWallet < 100) continue;
                realm.KingdomBudgetWallet -= 100;
                leader.ChangeHeroGold(100);
                armyExpense += 100;
            }
            RealmPolitics.Current?.Write("accounts|" + realm.StringId,
                $"Dia {(int)CampaignTime.Now.ToDays}. Receita: {income}. Domínio real: {demesne}. Exércitos: {armyExpense}. Não arrecadado: {arrears}.\n" +
                string.Join(", ",unpaid));
        }

        internal static void Intelligence(Kingdom realm, List<string> known, HashSet<string> live)
        {
            var spy = Officer(realm,CouncilOffice.Spymaster);
            var movement = RealmFactions.Current?.Find(realm);
            if (movement?.Rebels == null && movement?.Valid == true && !movement.Exposed && spy != null &&
                spy != movement.Pretender && !movement.Members().Contains(spy.Clan) &&
                (spy.GetRelation(movement.Pretender) <= spy.GetRelation(realm.Leader) || RealmPolitics.Loyalty(spy,realm) >= 45) &&
                MBRandom.RandomFloat < .06f + (spy.GetSkillValue(DefaultSkills.Roguery) + spy.GetSkillValue(DefaultSkills.Scouting)) / 900f)
            {
                movement.Exposed = true;
                RealmPolitics.Conduct(movement.Pretender,realm,-8,12);
                RealmPolitics.Current.Write("intelligence|" + realm.StringId,$"{spy.Name} descobriu a coalizão de {movement.Pretender.Name} por {movement.Demand}.");
                if (realm.Leader == Hero.MainHero)
                    InformationManager.DisplayMessage(new InformationMessage($"O mestre dos espiões identificou os partidários de {movement.Pretender.Name}."));
            }
            foreach (var title in TitleManager.Instance.AllTitles.Where(t => t.deJure?.Clan?.Kingdom == realm))
                foreach (var claim in title.OngoingClaims)
                {
                    if (claim.Key == null || claim.Key.Clan == realm.RulingClan) continue;
                    var key = $"{realm.StringId}|{title.StringId}|{claim.Key.StringId}|{(long)claim.Value.ToDays}";
                    live.Add(key);
                    if (spy == null || claim.Key == spy || known.Contains(key) ||
                        (spy.GetRelation(claim.Key) > spy.GetRelation(realm.Leader) && RealmPolitics.Loyalty(spy,realm) < 40) ||
                        MBRandom.RandomFloat >= .05f + RealmPolitics.Loyalty(spy,realm)/500f +
                        Math.Min(.65f,(spy.GetSkillValue(DefaultSkills.Roguery) + spy.GetSkillValue(DefaultSkills.Scouting)) / 650f)) continue;
                    known.Add(key);
                    RealmPolitics.Current?.Write("intelligence|" + realm.StringId,$"{spy.Name} identificou uma reivindicação de {claim.Key.Name} sobre {title.FullName}.");
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(realm.Leader,claim.Key,-2,false);
                    if (realm.Leader == Hero.MainHero)
                        InformationManager.DisplayMessage(new InformationMessage($"{spy.Name} descobriu que {claim.Key.Name} fabrica uma reivindicação sobre {title.FullName}."));
                }
        }
    }
}
