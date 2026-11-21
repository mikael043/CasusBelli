using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace CasusBelli
{
    public sealed class PeerageDecision : KingdomDecision
    {
        public PeerageDecision(Clan clan) : base(clan) { }
        public override bool IsAllowed() => ProposerClan.Kingdom == Kingdom && RealmPeers.Rank(ProposerClan) == 1;
        public override int GetProposalInfluenceCost() => RealmPeers.Cost;
        public override Clan DetermineChooser() => Kingdom.RulingClan;
        public override IEnumerable<DecisionOutcome> DetermineInitialCandidates()
        {
            yield return new PeerVote(true);
            yield return new PeerVote(false);
        }
        public override float DetermineSupport(Clan voter, DecisionOutcome outcome)
        {
            float merit = voter == ProposerClan ? 100 : voter.Leader.GetRelation(ProposerClan.Leader)
                + (ProposerClan.Tier - 3) * 10 + (voter.Culture == ProposerClan.Culture ? 5 : 0);
            return ((PeerVote)outcome).Approve ? merit : -merit;
        }
        public override void DetermineSponsors(MBReadOnlyList<DecisionOutcome> options)
        {
            foreach (PeerVote option in options)
                if (option.Approve) option.SetSponsor(ProposerClan); else AssignDefaultSponsor(option);
        }
        public override DecisionOutcome GetQueriedDecisionOutcome(MBReadOnlyList<DecisionOutcome> options) =>
            options.OfType<PeerVote>().First(o => o.Approve);
        public override void ApplyChosenOutcome(DecisionOutcome outcome)
        {
            if (IsAllowed() && ((PeerVote)outcome).Approve) RealmPeers.Current.Grant(ProposerClan);
        }
        public override void ApplySecondaryEffects(MBReadOnlyList<DecisionOutcome> options, DecisionOutcome chosen) { }
        public override TextObject GetGeneralTitle() => new TextObject($"Paridade plena para {ProposerClan.Name}");
        public override TextObject GetChooseTitle() => GetGeneralTitle();
        public override TextObject GetSupportTitle() => GetGeneralTitle();
        public override TextObject GetChooseDescription() => new TextObject($"Conceder ao clã {ProposerClan.Name} o direito de propor decisões e concorrer a feudos?");
        public override TextObject GetSupportDescription() => GetChooseDescription();
        public override TextObject GetSecondaryEffects() => TextObject.GetEmpty();
        public override TextObject GetChosenOutcomeText(DecisionOutcome outcome, SupportStatus status, bool shortVersion = false) =>
            new TextObject(((PeerVote)outcome).Approve ? $"O clã {ProposerClan.Name} obteve paridade plena." : $"O pedido do clã {ProposerClan.Name} foi recusado.");
    }

    public sealed class PeerVote : DecisionOutcome
    {
        [SaveableProperty(200)] public bool Approve { get; private set; }
        public PeerVote(bool approve) { Approve = approve; }
        public override TextObject GetDecisionTitle() => new TextObject(Approve ? "Conceder paridade plena" : "Recusar o pedido");
        public override TextObject GetDecisionDescription() => GetDecisionTitle();
        public override string GetDecisionLink() => null;
        public override ImageIdentifier GetDecisionImageIdentifier() => null;
    }
}
