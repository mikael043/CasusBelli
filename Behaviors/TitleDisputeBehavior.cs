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
            if (title?.TitleType == TitleType.Dukedom) return; // Ducal claims are opened only after political eligibility is evaluated.
            if (_disputes.Any(d => d.Title == title && d.Claimant == hero && d.IsActive)) return;
            var dispute = new TitleDispute(title, hero);
            if (!dispute.IsActive) return;
            _disputes.Add(dispute);
            if (hero.Clan == Clan.PlayerClan || dispute.Holder.Clan == Clan.PlayerClan)
                Notify($"Disputa por {title.FullName}: {hero.Name} reivindica o título de {dispute.Holder.Name}. Consulte K → CasusBelli → Disputas internas.");
        }

        internal string DucalOpeningBlock(FeudalTitle title, Hero claimant)
        {
            var block = DisputeAi.DucalSponsorBlock(title, claimant);
            if (block != null) return block;
            if (Active.Any(d => d.Title == title)) return "Já existe uma disputa ativa por este ducado.";
            if (Active.Any(d => d.IsDucal && (d.Claimant.Clan == claimant.Clan || d.Holder.Clan == claimant.Clan ||
                d.Claimant.Clan == title.deJure.Clan || d.Holder.Clan == title.deJure.Clan)))
                return "Seu clã já é parte principal de outra disputa ducal.";
            return null;
        }

        internal TitleDispute OpenDucal(FeudalTitle title, Hero claimant)
        {
            if (DucalOpeningBlock(title, claimant) != null) return null;
            var dispute = new TitleDispute(title, claimant);
            if (!dispute.IsActive) return null;
            _disputes.Add(dispute);
            if (claimant.Clan != Clan.PlayerClan)
            {
                MarkDucalCooldown(dispute.Realm, title, claimant.Clan, dispute.Holder.Clan);
                RealmPolitics.Current?.Write("ducal_deadline|" + title.StringId, (Day + DisputeAi.DucalRecruitmentDays).ToString());
                if (dispute.Holder.Clan == Clan.PlayerClan)
                    Notify($"{claimant.Name} iniciou uma disputa pelo seu {title.FullName}. Consulte K → CasusBelli → Disputas internas.");
            }
            return dispute;
        }

        private static int Day => (int)CampaignTime.Now.ToDays;
        private static string RealmQuiet(Kingdom realm) => "ducal_realm|" + realm.StringId;
        private static string ClanQuiet(Clan clan) => "ducal_clan|" + clan.StringId;
        private static string TitleQuiet(FeudalTitle title) => "ducal_title|" + title.StringId;

        internal static bool DucalCooldownReady(int day, int realmUntil, int clanUntil, int titleUntil) =>
            day >= realmUntil && day >= clanUntil && day >= titleUntil;

        private static void Later(string key, int days)
        {
            var politics = RealmPolitics.Current;
            if (politics != null) politics.Write(key, System.Math.Max(politics.Number(key, 0), Day + days).ToString());
        }

        private static void MarkDucalCooldown(Kingdom realm, FeudalTitle title, Clan claimant, Clan holder)
        {
            if (realm != null) Later(RealmQuiet(realm), DisputeAi.DucalRealmCooldown);
            if (title != null) Later(TitleQuiet(title), DisputeAi.DucalTitleCooldown);
            if (claimant != null) Later(ClanQuiet(claimant), DisputeAi.DucalTitleCooldown);
            if (holder != null) Later(ClanQuiet(holder), DisputeAi.DucalTitleCooldown);
        }

        private bool AiDucalExpired(TitleDispute dispute)
        {
            if (!dispute.IsDucal || dispute.Demanded || dispute.Claimant.Clan == Clan.PlayerClan) return false;
            var politics = RealmPolitics.Current;
            if (politics == null) return false;
            string key = "ducal_deadline|" + dispute.Title.StringId;
            int deadline = politics.Number(key, 0);
            if (deadline == 0) { politics.Write(key, (Day + DisputeAi.DucalRecruitmentDays).ToString()); return false; }
            if (Day < deadline) return false;
            PersonalTitleWar.Report(dispute, "A coalizão desistiu de pressionar o titular. A reivindicação permanece válida.");
            return true;
        }

        private void ClearEndedDisputes()
        {
            foreach (var d in _disputes.Where(d => !d.IsActive || AiDucalExpired(d)).ToList())
            {
                if (d.IsDucal)
                {
                    MarkDucalCooldown(d.Realm, d.Title, d.Claimant?.Clan, d.Holder?.Clan);
                    RealmPolitics.Current?.Write("ducal_deadline|" + d.Title.StringId, null);
                }
                _disputes.Remove(d);
            }
        }

        internal static bool DucalCommitmentBlocked(Clan clan, TitleDispute current)
        {
            var behavior = Campaign.Current?.CampaignBehaviorManager == null ? null : Campaign.Current.GetCampaignBehavior<TitleDisputeBehavior>();
            return behavior?.Active.Any(d => d != current && (current == null || current.IsDucal || d.IsDucal) &&
                (d.Claimant.Clan == clan || d.Holder.Clan == clan || d.Offers.Any(o => o.Accepted && o.Recipient == clan))) == true;
        }

        private bool PrincipalBusy(Clan clan) => Active.Any(d =>
            d.Claimant.Clan == clan || d.Holder.Clan == clan);

        // First attempt per realm on peaceful weeks only. At most one autonomous ducal action world-wide per check.
        private void TryDucalInitiatives()
        {
            var politics = RealmPolitics.Current;
            if (politics == null || politics.Number("ducal_scan_day", -1) == Day) return;
            politics.Write("ducal_scan_day", Day.ToString());
            politics.Write("ducal_ai_scans", (politics.Number("ducal_ai_scans", 0) + 1).ToString());
            var manager = TitleManager.Instance;
            var duchies = manager.AllTitles.Where(t => t.TitleType == TitleType.Dukedom && t.deJure?.Clan != null).ToList();
            int stable = 0, checkedRealms = 0, opportunities = 0;
            string outcome = "nenhuma iniciativa";
            // Random order prevents the first kingdom in the engine list monopolizing the weekly global cap.
            foreach (var realm in Kingdom.All.Where(DisputeAi.DucalAiRealmStable).OrderBy(k => MBRandom.RandomFloat).ToList())
            {
                if (politics.Number(RealmQuiet(realm), 0) > Day ||
                    Active.Any(d => d.Realm == realm && (d.IsDucal || d.Demanded))) continue;
                var titles = duchies.Where(t => t.deJure.Clan.Kingdom == realm && manager.GetTitleFaction(t) == realm &&
                    t.deJure.Clan != realm.RulingClan && !t.deJure.IsPrisoner).ToList();
                // A fabrication already underway reserves this realm's ducal slot until it matures.
                if (titles.Any(t => t.OngoingClaims.Keys.Any(h => h != null && !h.IsDead &&
                    h.Clan?.Kingdom == realm && h.Clan != Clan.PlayerClan))) continue;
                stable++;
                if (MBRandom.RandomFloat >= DisputeAi.DucalWeeklyChance) continue;
                checkedRealms++;
                var clans = realm.Clans.Where(c => c != Clan.PlayerClan && c != realm.RulingClan &&
                    c.Leader != null && !c.Leader.IsDead && !c.Leader.IsPrisoner && !PrincipalBusy(c) && !DucalCommitmentBlocked(c, null) &&
                    !c.IsEliminated && !c.IsUnderMercenaryService && RealmPeers.Rank(c) == 2 && c.Tier >= 3 &&
                    !manager.AllTitles.Any(t => t.OngoingClaims.ContainsKey(c.Leader))).ToList();
                foreach (var title in titles.OrderByDescending(t => RealmPolitics.Controversy(t.deJure)))
                {
                    if (PrincipalBusy(title.deJure.Clan) || DucalCommitmentBlocked(title.deJure.Clan, null) || politics.Number(ClanQuiet(title.deJure.Clan), 0) > Day) continue;
                    foreach (var clan in clans.Where(c => c != title.deJure.Clan).OrderByDescending(c => c.Tier))
                    {
                        if (!DucalCooldownReady(Day, politics.Number(RealmQuiet(realm), 0),
                            politics.Number(ClanQuiet(clan), 0), politics.Number(TitleQuiet(title), 0))) continue;
                        var hero = clan.Leader;
                        if (hero.IsFriend(title.deJure) || hero.GetRelation(title.deJure) >= 50) continue;
                        var claim = title.GetHeroClaim(hero);
                        if (claim == ClaimType.Ongoing || DisputeAi.ProjectedDucalSponsorBlock(title, hero) != null) continue;
                        TitleModel.GetDucalCountyControl(title, clan, out int controlled, out int total);
                        if (!DisputeAi.DucalAiLocalInterest(controlled, claim == ClaimType.PreviousOwner)) continue;
                        bool mature = title.HeroHasValidClaim(hero);
                        bool territorial = TitleModel.HasDucalMajority(controlled, total);
                        if (!territorial && !DisputeAi.DucalAiForecast(title, hero)) continue;
                        opportunities++;
                        if (!mature)
                        {
                            // Reserve the slot until the actual claim matures; this is not a free claim.
                            var fabrication = manager.GetModel().GetClaimAction(title, hero);
                            if (!fabrication.Possible || !DisputeAi.DucalAiResources(hero.Gold, clan.Influence,
                                fabrication.Gold, fabrication.Influence, true)) continue;
                            fabrication.TakeAction(null);
                            if (!title.OngoingClaims.ContainsKey(hero)) continue;
                            Later(RealmQuiet(realm), 42);
                            outcome = $"fabricação: {clan.Name} / {title.FullName}";
                        }
                        else if (territorial)
                        {
                            var usurp = manager.GetModel().GetUsurpAction(title, hero);
                            if (!usurp.Possible || !DisputeAi.DucalAiResources(hero.Gold, clan.Influence,
                                usurp.Gold, usurp.Influence, false)) continue;
                            var oldClan = title.deJure.Clan;
                            usurp.TakeAction(null);
                            if (title.deJure != hero) continue;
                            MarkDucalCooldown(realm, title, clan, oldClan);
                            outcome = $"usurpação territorial: {clan.Name} / {title.FullName}";
                        }
                        else
                        {
                            if (!DisputeAi.DucalAiResources(hero.Gold, clan.Influence, 0, PersonalTitleWar.DemandCost, false)) continue;
                            if (OpenDucal(title, hero) == null) continue;
                            outcome = $"disputa política: {clan.Name} / {title.FullName}";
                        }
                        politics.Write("ducal_scan_result", $"Dia {Day}: {stable} reinos estáveis; {checkedRealms} avaliados; {opportunities} oportunidades; {outcome}.");
                        politics.Write("ducal_ai_actions", (politics.Number("ducal_ai_actions", 0) + 1).ToString());
                        return;
                    }
                }
            }
            politics.Write("ducal_scan_result", $"Dia {Day}: {stable} reinos estáveis; {checkedRealms} avaliados; {opportunities} oportunidades; {outcome}.");
        }

        internal string DucalAudit()
        {
            var politics = RealmPolitics.Current;
            return (politics?.Read("ducal_scan_result") ?? "Nenhuma avaliação semanal registrada.") +
                $"\nAvaliações semanais: {politics?.Number("ducal_ai_scans", 0) ?? 0}. Iniciativas: {politics?.Number("ducal_ai_actions", 0) ?? 0}." +
                "\nDisputas ducais ativas: " + Active.Count(d => d.IsDucal) +
                "\nEm fabricação: " + TitleManager.Instance.AllTitles.Where(t => t.TitleType == TitleType.Dukedom).Sum(t => t.OngoingClaims.Count);
        }

        private static void Notify(string text) => InformationManager.DisplayMessage(new InformationMessage(text));

        private void DailyTick()
        {
            if (!TitleManager.Instance.IsInitialized) return;
            ClearEndedDisputes();
            foreach (var title in TitleManager.Instance.AllTitles.Where(t => t.Fief?.IsFortification == true))
                foreach (var hero in title.OngoingClaims.Keys.Concat(title.Claims.Where(p => p.Value == ClaimType.Fabricated)
                    .Select(p => p.Key)).Distinct()) Track(title, hero);

            // AI vassals can start the same paid fabrication as the player.
            if ((int)CampaignTime.Now.ToDays % 7 == 0)
            {
                TryDucalInitiatives();
                foreach (var clan in Clan.All)
                    if (clan != Clan.PlayerClan && !clan.IsEliminated && !clan.IsUnderMercenaryService &&
                        clan.Kingdom != null && clan.Leader != null && MBRandom.RandomFloat < 0.02f)
                        TryStartClaim(clan);
            }

            foreach (var dispute in Active.ToList())
            {
                if (dispute.Demanded)
                {
                    PersonalTitleWar.Tick(dispute);
                    continue;
                }

                if (dispute.IsDucal && dispute.Claimant.Clan != Clan.PlayerClan &&
                    (!DisputeAi.DucalAiRealmStable(dispute.Realm) || dispute.Claimant.IsPrisoner || dispute.Holder.IsPrisoner)) continue;

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
                        .Where(c => !dispute.IsDucal || DisputeAi.Support(dispute, c, side, advantage) > 0)
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

        private void TryStartClaim(Clan clan)
        {
            if (Active.Any(d => d.IsDucal && (d.Claimant.Clan == clan || d.Holder.Clan == clan))) return;
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