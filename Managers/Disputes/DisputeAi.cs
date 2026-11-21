using System;
using System.Linq;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;

namespace CasusBelli.Disputes
{
    internal static class DisputeAi
    {
        internal static int FieldTroops(Clan clan) =>
            clan.WarPartyComponents.Sum(p => p.MobileParty?.MemberRoster.TotalHealthyCount ?? 0);

        // Bounded even when one coalition has no field army.
        internal static float Advantage(int ours, int theirs) =>
            (ours - theirs) / (float)Math.Max(1, ours + theirs);

        private static int Standing(Clan clan) => clan.Tier + (clan.Kingdom?.RulingClan == clan ? 3 : 0);

        private static bool SameCulture(Clan a, Clan b) => a.Culture != null && a.Culture == b.Culture;

        internal static float TargetValue(Clan clan, FeudalTitle title)
        {
            var holder = title.deJure;
            return 10 - 0.45f * clan.Leader.GetRelation(holder)
                + 18 * Advantage(FieldTroops(clan), FieldTroops(holder.Clan))
                + 2 * (Standing(clan) - Standing(holder.Clan))
                + (clan.Culture != null && clan.Culture == title.Fief.Culture ? 8 : 0);
        }

        internal static float Support(TitleDispute dispute, Clan voter, bool side, float attackerAdvantage)
        {
            var ally = dispute.Sponsor(side).Clan;
            var rival = dispute.Sponsor(!side).Clan;
            return 0.55f * dispute.Affinity(voter, side)
                + (SameCulture(voter, ally) ? 8 : 0) - (SameCulture(voter, rival) ? 8 : 0)
                + 2 * (Standing(ally) - Standing(rival))
                + (side ? 14 * attackerAdvantage - 4 : 4 - 14 * attackerAdvantage);
        }

        internal static bool Press(TitleDispute dispute, float advantage)
        {
            var claimant = dispute.Claimant;
            var holder = dispute.Holder;
            return advantage * 60 - claimant.GetRelation(holder) * 0.25f
                + 2 * (Standing(claimant.Clan) - Standing(holder.Clan))
                + (claimant.Culture == dispute.Title.Fief.Culture ? 5 : 0) >= 3;
        }

        internal static bool Yield(TitleDispute dispute, float attackerAdvantage)
        {
            var holder = dispute.Holder;
            return 65 * attackerAdvantage + 0.18f * holder.GetRelation(dispute.Claimant)
                + 2 * (Standing(dispute.Claimant.Clan) - Standing(holder.Clan))
                - (holder.Clan.Fiefs.Count <= 1 ? 12 : 0)
                - (holder.Culture == dispute.Title.Fief.Culture ? 5 : 0) > 20;
        }
    }
}
