using System.Collections.Generic;
using System.Linq;
using CasusBelli.Disputes;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli
{
    public sealed class TitleDisputeBehavior : CampaignBehaviorBase
    {
        private List<TitleDispute> _disputes = new List<TitleDispute>();
        public IEnumerable<TitleDispute> Active => _disputes.Where(d => d.IsActive);

        public override void RegisterEvents() =>
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DailyTick);

        public override void SyncData(IDataStore store)
        {
            store.SyncData("CasusBelli_PersonalDisputes_v1", ref _disputes);
            _disputes ??= new List<TitleDispute>();
        }

        internal void Track(FeudalTitle title, Hero hero)
        {
            if (_disputes.Any(d => d.Title == title && d.Claimant == hero && d.IsActive)) return;
            var dispute = new TitleDispute(title, hero);
            if (!dispute.IsActive) return;
            _disputes.Add(dispute);
            if (hero.Clan == Clan.PlayerClan || dispute.Holder.Clan == Clan.PlayerClan)
                Notify($"Disputa por {title.FullName}: {hero.Name} reivindica o título de {dispute.Holder.Name}. Consulte K → CasusBelli → Disputas internas.");
        }

        private static void Notify(string text) => InformationManager.DisplayMessage(new InformationMessage(text));

        private void DailyTick()
        {
            if (!TitleManager.Instance.IsInitialized) return;
            _disputes.RemoveAll(d => !d.IsActive);
            foreach (var title in TitleManager.Instance.AllTitles.Where(t => t.Fief?.IsFortification == true))
                foreach (var hero in title.OngoingClaims.Keys.Concat(title.Claims.Where(p => p.Value == ClaimType.Fabricated)
                    .Select(p => p.Key)).Distinct()) Track(title, hero);

            // AI vassals can start the same paid fabrication as the player.
            if ((int)CampaignTime.Now.ToDays % 7 == 0)
                foreach (var clan in Clan.All)
                    if (clan != Clan.PlayerClan && !clan.IsEliminated && !clan.IsUnderMercenaryService &&
                        clan.Kingdom != null && clan.Leader != null && MBRandom.RandomFloat < 0.02f)
                        TryStartClaim(clan);

            foreach (var dispute in Active)
            {
                if (dispute.Demanded)
                {
                    PersonalTitleWar.Tick(dispute);
                    continue;
                }

                var advantage = DisputeAi.Advantage(PersonalTitleWar.Troops(dispute, true), PersonalTitleWar.Troops(dispute, false));
                if (dispute.CanDemand && dispute.Claimant.Clan != Clan.PlayerClan && DisputeAi.Press(dispute, advantage))
                {
                    PersonalTitleWar.Demand(dispute);
                    if (dispute.Demanded) continue;
                }
                foreach (var side in new[] { true, false })
                {
                    if (dispute.Sponsor(side).Clan == Clan.PlayerClan) continue;
                    var target = dispute.Realm.Clans.Where(c => dispute.CanSend(c, side))
                        .OrderByDescending(c => DisputeAi.Support(dispute, c, side, advantage)).FirstOrDefault();
                    if (target != null) dispute.Send(target, side);
                }
                var arrivals = dispute.Offers.Where(dispute.CanAnswer)
                    .OrderByDescending(o => DisputeAi.Support(dispute, o.Recipient, o.ForClaimant, advantage));
                foreach (var offer in arrivals)
                {
                    if (!dispute.CanAnswer(offer)) continue;
                    if (offer.Recipient == Clan.PlayerClan)
                    {
                        if (offer.Arrival.ElapsedDaysUntilNow < 1)
                            Notify($"Emissário de {dispute.Sponsor(offer.ForClaimant).Name}: pedido de apoio por {dispute.Title.FullName}. Responda em K → CasusBelli → Disputas internas.");
                    }
                    else offer.Answer(DisputeAi.Support(dispute, offer.Recipient, offer.ForClaimant, advantage) > 0);
                }
            }
        }

        private static void TryStartClaim(Clan clan)
        {
            var manager = TitleManager.Instance;
            if (manager.AllTitles.Any(t => t.OngoingClaims.ContainsKey(clan.Leader))) return;
            var targets = clan.Kingdom.Settlements.Where(s => s.IsFortification && s.OwnerClan != clan)
                .Select(manager.GetTitle).Where(t => t?.deJure != null && t.deJure.Clan == t.Fief.OwnerClan).ToList();
            if (targets.Count == 0) return;
            foreach (var candidate in targets.Select(t => new { Title = t, Score = DisputeAi.TargetValue(clan, t) })
                .Where(t => t.Score > 0).OrderByDescending(t => t.Score))
            {
                var action = manager.GetModel().GetClaimAction(candidate.Title, clan.Leader);
                if (!action.Possible || !action.IsWilling) continue;
                action.TakeAction(null);
                break;
            }
        }
    }
}
