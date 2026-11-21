using TaleWorlds.CampaignSystem;

namespace CasusBelli.Disputes
{
    internal static class PersonalPeace
    {
        private static bool Principal(TitleDispute d, Clan clan) =>
            d.IsActive && d.Contested && (clan == d.Claimant.Clan || clan == d.Holder.Clan);

        internal static bool CanOffer(TitleDispute d) =>
            d.IsActive && d.Contested && d.PeaceProposer == null && !d.NextPeaceOffer.IsFuture;

        internal static void Surrender(TitleDispute d, Clan clan)
        {
            if (Principal(d, clan)) PersonalTitleWar.Resolve(d, clan == d.Holder.Clan);
        }

        internal static void Offer(TitleDispute d, Clan clan)
        {
            if (!CanOffer(d) || !Principal(d, clan)) return;
            d.PeaceProposer = clan;
            d.NextPeaceOffer = CampaignTime.DaysFromNow(7);
            PersonalTitleWar.Report(d, $"{clan.Name} propõe paz branca.");
        }

        internal static void Answer(TitleDispute d, Clan clan, bool accept)
        {
            if (!Principal(d, clan) || d.PeaceProposer == null || d.PeaceProposer == clan) return;
            d.PeaceProposer = null;
            if (accept)
            {
                d.Demanded = d.Contested = false;
                d.Balance = 0;
                d.Offers.Clear();
                d.TruceEnds = CampaignTime.DaysFromNow(60);
            }
            PersonalTitleWar.Report(d, accept ? "Paz branca. Os clãs se desmobilizam por este título." : "Proposta de paz branca recusada.");
        }

        internal static void Tick(TitleDispute d)
        {
            bool attackerBehind = d.Balance <= 0;
            var behind = d.Sponsor(attackerBehind).Clan;
            var ahead = d.Sponsor(!attackerBehind).Clan;
            if (behind != Clan.PlayerClan && System.Math.Abs(d.Balance) >= 80 &&
                PersonalTitleWar.Troops(d, attackerBehind) < PersonalTitleWar.Troops(d, !attackerBehind))
            {
                Surrender(d, behind);
                return;
            }
            if (behind != Clan.PlayerClan && ahead != behind && CanOffer(d)) Offer(d, behind);
            if (d.PeaceProposer == null) return;
            var recipient = d.PeaceProposer == behind ? ahead : behind;
            if (recipient != Clan.PlayerClan)
                Answer(d, recipient, (recipient == d.Claimant.Clan ? d.Balance : -d.Balance) <= 20);
            else if (!d.NextPeaceOffer.IsFuture) Answer(d, recipient, false);
        }
    }
}
