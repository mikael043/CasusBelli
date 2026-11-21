using System.Collections.Generic;
using System.Linq;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace CasusBelli
{
    internal enum CouncilOffice { Steward, Marshal, Chancellor, Spymaster }

    internal static class CouncilSelection
    {
        internal static readonly CouncilOffice[] Offices = { CouncilOffice.Steward, CouncilOffice.Marshal, CouncilOffice.Chancellor, CouncilOffice.Spymaster };
        internal static string Name(CouncilOffice office) => new[] { "Intendente", "Marechal", "Chanceler", "Mestre dos Espiões" }[(int)office];

        internal static List<Hero> Court(Kingdom realm)
        {
            var house = realm.RulingClan;
            var peers = realm.Clans.Where(c => c != house && RealmPeers.Rank(c) > 0);
            return house.Heroes.Concat(house.Companions).Concat(peers.Select(c => c.Leader))
                .Concat(house.Fiefs.Where(t => t.Settlement.IsTown).SelectMany(t => t.Settlement.Notables))
                .Where(h => h != null && h != realm.Leader && h.IsAlive && !h.IsChild && !h.IsPrisoner &&
                    !h.IsDisabled && h.GetRelation(realm.Leader) > -30).Distinct().ToList();
        }

        internal static bool Eligible(Kingdom realm, Hero h, CouncilOffice office)
        {
            if (office != CouncilOffice.Marshal && office != CouncilOffice.Chancellor) return true;
            if (office == CouncilOffice.Marshal && (h.PartyBelongedTo == null || h.PartyBelongedTo.LeaderHero != h || !h.PartyBelongedTo.IsLordParty)) return false;
            if (!h.IsLord && !(office == CouncilOffice.Chancellor && h.IsNotable)) return false;
            if (!h.IsFemale || !TitleManager.Instance.IsInitialized) return true;
            var sovereign = TitleManager.Instance.GetSovereignTitleList(realm).FirstOrDefault();
            return sovereign?.Contract?.GenderLaw?.FemaleCanRule != false;
        }

        internal static float Score(Kingdom realm, Hero h, CouncilOffice office)
        {
            int Value(SkillObject skill) => h.GetSkillValue(skill);
            float skill;
            int profile;
            switch (office)
            {
                case CouncilOffice.Steward:
                    skill = .7f * Value(DefaultSkills.Steward) + .3f * Value(DefaultSkills.Trade);
                    profile = h.Clan == realm.RulingClan || h.IsNotable ? 5 : 0;
                    break;
                case CouncilOffice.Marshal:
                    skill = .6f * Value(DefaultSkills.Leadership) + .25f * Value(DefaultSkills.Tactics) + .15f * Value(DefaultSkills.Engineering)
                        + MarshalCommand.MobilizationScore(h);
                    profile = h.PartyBelongedTo?.Army?.LeaderParty?.LeaderHero == h ? 8 : 0;
                    break;
                case CouncilOffice.Chancellor:
                    var lordship = MBObjectManager.Instance.GetObject<SkillObject>("Lordship") ?? DefaultSkills.Leadership;
                    skill = .7f * Value(DefaultSkills.Charm) + .3f * Value(lordship);
                    profile = h.IsNotable ? 5 : System.Math.Min(8, h.Clan?.Tier ?? 0);
                    break;
                default:
                    skill = .7f * Value(DefaultSkills.Roguery) + .3f * Value(DefaultSkills.Scouting);
                    profile = h.IsGangLeader || h.IsWanderer ? 6 : 0;
                    break;
            }
            return skill + profile + .05f * h.GetRelation(realm.Leader);
        }
    }
}
