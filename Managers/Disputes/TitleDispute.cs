using System.Collections.Generic;
using System.Linq;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace CasusBelli.Disputes
{
    public sealed class SupportOffer
    {
        [SaveableProperty(1)] public Clan Recipient { get; private set; }
        [SaveableProperty(2)] public bool ForClaimant { get; private set; }
        [SaveableProperty(3)] public CampaignTime Arrival { get; private set; }
        [SaveableProperty(4)] public bool Answered { get; private set; }
        [SaveableProperty(5)] public bool Accepted { get; private set; }

        public SupportOffer(Clan recipient, bool forClaimant)
        {
            Recipient = recipient;
            ForClaimant = forClaimant;
            Arrival = CampaignTime.DaysFromNow(3);
        }

        internal void Answer(bool accept)
        {
            if (Answered || Arrival.IsFuture) return;
            Answered = true;
            Accepted = accept;
        }
    }

    public sealed class TitleDispute
    {
        [SaveableProperty(1)] public FeudalTitle Title { get; private set; }
        [SaveableProperty(2)] public Hero Claimant { get; private set; }
        [SaveableProperty(3)] public Hero Holder { get; private set; }
        [SaveableProperty(4)] public Kingdom Realm { get; private set; }
        [SaveableProperty(5)] public List<SupportOffer> Offers { get; private set; }

        [SaveableProperty(6)] public bool Demanded { get; internal set; }
        [SaveableProperty(7)] public bool Contested { get; internal set; }
        [SaveableProperty(8)] public float Balance { get; internal set; }
        [SaveableProperty(9)] public CampaignTime ReplyDeadline { get; internal set; }
        [SaveableProperty(10)] public CampaignTime NextPeaceOffer { get; internal set; }
        [SaveableProperty(11)] public Clan PeaceProposer { get; internal set; }
        [SaveableProperty(12)] public CampaignTime TruceEnds { get; internal set; }
        [SaveableProperty(13)] public bool MediationAttempted { get; internal set; }
        [SaveableProperty(14)] public bool Finished { get; internal set; }
        [SaveableProperty(15)] public CampaignTime WarStartedAt { get; internal set; }
        public bool IsDucal => Title?.TitleType == TitleType.Dukedom;
        public bool CanDemand => IsActive && !Demanded && (IsDucal ? Title.HeroHasValidClaim(Claimant) : Title.GetHeroClaim(Claimant) == ClaimType.Fabricated);

        public TitleDispute(FeudalTitle title, Hero claimant)
        {
            Title = title;
            Claimant = claimant;
            Holder = title.deJure;
            Realm = Holder?.Clan?.Kingdom;
            Offers = new List<SupportOffer>();
        }

        public bool IsActive => !Finished && Realm != null && Claimant != null && Holder != null &&
            !Claimant.IsDead && !Holder.IsDead && Claimant.Clan?.Kingdom == Realm && Holder.Clan?.Kingdom == Realm &&
            Claimant.Clan != Holder.Clan && Title?.deJure == Holder &&
            (IsDucal ? Claimant.Clan.Leader == Claimant && Holder.Clan.Leader == Holder &&
                !Claimant.Clan.IsEliminated && !Holder.Clan.IsEliminated && !Claimant.Clan.IsUnderMercenaryService &&
                !Holder.Clan.IsUnderMercenaryService && Title.HeroHasValidClaim(Claimant) :
                Title.Fief?.OwnerClan == Holder.Clan &&
                (Title.TitleType == TitleType.County || Title.TitleType == TitleType.Barony) &&
                Title.GetHeroClaim(Claimant) != ClaimType.None);

        public Hero Sponsor(bool forClaimant) => forClaimant ? Claimant : Holder;

        public bool Eligible(Clan clan) => clan?.Leader != null && !clan.IsEliminated &&
            clan.Kingdom == Realm && clan != Claimant.Clan && clan != Holder.Clan && !clan.IsUnderMercenaryService && (!IsDucal || RealmPeers.Rank(clan) == 2);

        public bool CanSend(Clan recipient, bool forClaimant)
        {
            if (Demanded || !IsActive || !Eligible(recipient) || TitleDisputeBehavior.DucalCommitmentBlocked(recipient, this)) return false;
            if (IsDucal && (TruceEnds.IsFuture || (forClaimant && DisputeAi.DucalSponsorBlock(Title, Claimant) != null) ||
                Offers.Any(o => o.Recipient == recipient && o.ForClaimant == forClaimant && o.Answered))) return false;
            // One dispatch per side per week, including the three travel days.
            return !Offers.Any(o =>
                (o.ForClaimant == forClaimant && o.Arrival.ElapsedDaysUntilNow < 4) ||
                (o.Recipient == recipient && (o.Accepted || (!o.Answered && o.ForClaimant == forClaimant))));
        }

        public void Send(Clan recipient, bool forClaimant)
        {
            if (!CanSend(recipient, forClaimant)) return;
            Offers.RemoveAll(o => o.Recipient == recipient && o.ForClaimant == forClaimant && o.Answered);
            Offers.Add(new SupportOffer(recipient, forClaimant));
        }

        public bool CanAnswer(SupportOffer offer) => !Demanded && IsActive && Offers.Contains(offer) &&
            Eligible(offer.Recipient) && !TitleDisputeBehavior.DucalCommitmentBlocked(offer.Recipient, this) && (!IsDucal || !TruceEnds.IsFuture) && !offer.Answered && !offer.Arrival.IsFuture &&
            !Offers.Any(o => o.Recipient == offer.Recipient && o.Accepted);

        public int Affinity(Clan clan, bool forClaimant) =>
            clan.Leader.GetRelation(Sponsor(forClaimant)) - clan.Leader.GetRelation(Sponsor(!forClaimant));
    }
}