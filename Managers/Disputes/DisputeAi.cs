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
            if (dispute.IsDucal && side)
                return EvaluateDucalSupport(dispute.Title, dispute.Claimant, voter, out _) ? 1 : -1;
            var ally = dispute.Sponsor(side).Clan;
            var rival = dispute.Sponsor(!side).Clan;
            return .3f * (RealmPolitics.Controversy(rival.Leader) - RealmPolitics.Controversy(ally.Leader))
                + (ally == dispute.Realm.RulingClan ? .25f * (RealmPolitics.Loyalty(voter.Leader,dispute.Realm) - 50) : 0)
                + 0.55f * dispute.Affinity(voter, side)
                + (SameCulture(voter, ally) ? 8 : 0) - (SameCulture(voter, rival) ? 8 : 0)
                + 2 * (Standing(ally) - Standing(rival))
                + (side ? 14 * attackerAdvantage - 4 : 4 - 14 * attackerAdvantage);
        }

        // A high relation cannot compensate for an ineligible sponsor or a proposal with no political benefit.
        internal static bool HasDucalSponsorStanding(int peerRank, int tier, bool landed, bool formerOwner) =>
            peerRank == 2 && tier >= 3 && (landed || formerOwner);

        internal static bool DucalSupportThreshold(float structural, float affinity) =>
            structural >= 10f && structural + Math.Max(-15f, Math.Min(15f, affinity)) >= 20f;

        internal static string DucalSponsorBlock(FeudalTitle title, Hero claimant) => DucalSponsorBlockCore(title, claimant, false);

        internal static string ProjectedDucalSponsorBlock(FeudalTitle title, Hero claimant) => DucalSponsorBlockCore(title, claimant, true);

        private static string DucalSponsorBlockCore(FeudalTitle title, Hero claimant, bool projected)
        {
            var holder = title?.deJure;
            var clan = claimant?.Clan;
            var realm = clan?.Kingdom;
            if (title?.TitleType != TitleType.Dukedom || holder?.Clan == null || realm == null ||
                holder.Clan.Kingdom != realm || holder.Clan == clan)
                return "Uma disputa ducal interna exige dois clãs distintos do mesmo reino.";
            if (claimant.IsDead || holder.IsDead || clan.Leader != claimant || holder.Clan.Leader != holder ||
                clan.IsEliminated || holder.Clan.IsEliminated || clan.IsUnderMercenaryService || holder.Clan.IsUnderMercenaryService)
                return "Somente o líder de um clã vassalo pode iniciar a disputa.";
            if (!projected && !title.HeroHasValidClaim(claimant))
                return "A reivindicação precisa estar madura.";
            if (RealmPolitics.Authority(realm) >= 75 && claimant != realm.Leader)
                return "A autoridade da Coroa proíbe disputas ducais privadas.";
            if (!HasDucalSponsorStanding(RealmPeers.Rank(clan), clan.Tier, clan.Fiefs.Count > 0,
                title.GetHeroClaim(claimant) == ClaimType.PreviousOwner))
                return "O pretendente precisa de paridade plena, clã de nível 3 e uma base fundiária (ou ser ex-titular).";
            return null;
        }

        internal static bool EvaluateDucalSupport(FeudalTitle title, Hero claimant, Clan voter, out string details) =>
            EvaluateDucalSupportCore(title, claimant, voter, false, out details);

        internal static bool EvaluateProjectedDucalSupport(FeudalTitle title, Hero claimant, Clan voter) =>
            EvaluateDucalSupportCore(title, claimant, voter, true, out _);

        private static bool EvaluateDucalSupportCore(FeudalTitle title, Hero claimant, Clan voter, bool projected, out string details)
        {
            var block = DucalSponsorBlockCore(title, claimant, projected);
            if (block != null) { details = block; return false; }
            var holder = title.deJure;
            var realm = claimant.Clan.Kingdom;
            if (voter?.Leader == null || voter.Kingdom != realm || voter == claimant.Clan ||
                voter == holder.Clan || RealmPeers.Rank(voter) != 2)
            { details = "Somente outros pares plenos do reino podem apoiar."; return false; }
            if (title.HeroHasValidClaim(voter.Leader))
            { details = "Este clã possui uma reivindicação própria e não quer fortalecer um rival."; return false; }

            int claimantLegitimacy = projected ? TitleModel.ProjectedDucalLegitimacyScore(title, claimant) : TitleModel.DucalLegitimacyScore(title, claimant);
            float legitimacy = (claimantLegitimacy - TitleModel.DucalLegitimacyScore(title, holder)) * .5f;
            float military = 20 * Advantage(FieldTroops(claimant.Clan), FieldTroops(holder.Clan));
            float misconduct = .30f * (RealmPolitics.Controversy(holder) - RealmPolitics.Controversy(claimant));
            float precedent = -10f;
            float war = HasExternalKingdomWar(realm) ? -15f : 0f;
            float allegiance = claimant == realm.Leader ? (RealmPolitics.Loyalty(voter.Leader, realm) - 50) * .25f :
                holder == realm.Leader ? -(RealmPolitics.Loyalty(voter.Leader, realm) - 50) * .25f : 0f;
            TitleModel.GetDucalCountyControl(title, voter, out int controlled, out _);
            float estate = controlled > 0 ? -10f : 0f;
            float structural = legitimacy + military + misconduct + precedent + war + allegiance + estate;
            float affinity = Math.Max(-15f, Math.Min(15f, .15f *
                (voter.Leader.GetRelation(claimant) - voter.Leader.GetRelation(holder))));
            bool accepts = DucalSupportThreshold(structural, affinity);
            details = $"Mérito político: {structural:+0.0;-0.0;0} (mín. 10). Relações: {affinity:+0.0;-0.0;0}. Total: {structural + affinity:0.0} (mín. 20).\n" +
                $"Legitimidade {legitimacy:+0.0;-0.0;0}; força militar {military:+0.0;-0.0;0}; controvérsia {misconduct:+0.0;-0.0;0}; " +
                $"precedente {precedent:+0;-0;0}; guerra externa {war:+0;-0;0}; lealdade {allegiance:+0.0;-0.0;0}; patrimônio no ducado {estate:+0;-0;0}.";
            return accepts;
        }

        internal const int DucalAuthorityLimit = 65;
        internal const int DucalRealmCooldown = 84;
        internal const int DucalTitleCooldown = 168;
        internal const int DucalRecruitmentDays = 84;
        internal const float DucalWeeklyChance = .15f;

        internal static bool DucalAiStable(bool externalWar, bool civilCrisis, int authority, int quietUntil, int day) =>
            !externalWar && !civilCrisis && authority < DucalAuthorityLimit && day >= quietUntil;

        // Permanent hostility to bandits and minor factions is not a war between kingdoms.
        internal static bool HasExternalKingdomWar(Kingdom realm) => realm != null &&
            realm.FactionsAtWarWith.Any(f => f is Kingdom enemy && enemy != realm && !enemy.IsEliminated);

        internal static bool DucalAiRealmStable(Kingdom realm) => realm != null && !realm.IsEliminated && realm.Leader != null &&
            DucalAiStable(HasExternalKingdomWar(realm), RealmFactions.Current?.Find(realm) != null,
                RealmPolitics.Authority(realm), RealmPolitics.Current?.Number("quiet|" + realm.StringId, 0) ?? 0,
                (int)CampaignTime.Now.ToDays);

        internal static bool DucalAiResources(float gold, float influence, float goldCost, float influenceCost, bool fabrication) =>
            gold >= goldCost + 5000f && influence >= influenceCost + (fabrication ? PersonalTitleWar.DemandCost + 30f : 30f);

        internal static bool DucalAiSafeForce(int attack, int defense) =>
            attack > 0 && defense >= 0 && (long)attack * 2 >= (long)defense * 3;

        internal static bool DucalAiLocalInterest(int controlled, bool formerOwner) => controlled > 0 || formerOwner;

        internal static bool DucalAiForecast(FeudalTitle title, Hero claimant)
        {
            var holder = title.deJure;
            var voters = claimant.Clan.Kingdom.Clans.Where(c => c != claimant.Clan && c != holder.Clan &&
                c.Leader != null && !c.Leader.IsDead && RealmPeers.Rank(c) == 2).ToList();
            // Player support is never assumed; everyone else is conservatively counted on defense.
            var supporters = voters.Where(c => c != Clan.PlayerClan && !TitleDisputeBehavior.DucalCommitmentBlocked(c, null) && EvaluateProjectedDucalSupport(title, claimant, c)).ToList();
            if (!PersonalTitleWar.HasPoliticalMajority(supporters.Count, voters.Count)) return false;
            return DucalAiSafeForce(FieldTroops(claimant.Clan) + supporters.Sum(FieldTroops),
                FieldTroops(holder.Clan) + voters.Except(supporters).Sum(FieldTroops));
        }

        internal static bool Press(TitleDispute dispute, float advantage)
        {
            var claimant = dispute.Claimant;
            var holder = dispute.Holder;
            if (dispute.IsDucal)
                return DucalAiRealmStable(dispute.Realm) && !claimant.IsPrisoner &&
                    !holder.IsPrisoner && advantage >= .20f && claimant.Clan.Influence >= PersonalTitleWar.DemandCost + 30f;
            return advantage * 60 - claimant.GetRelation(holder) * 0.25f
                + 2 * (Standing(claimant.Clan) - Standing(holder.Clan))
                + (claimant.Culture == dispute.Title.Fief.Culture ? 5 : 0) >= 3;
        }

        internal static bool Yield(TitleDispute dispute, float attackerAdvantage)
        {
            var holder = dispute.Holder;
            if (dispute.IsDucal)
                return attackerAdvantage >= .25f && TitleModel.DucalLegitimacyScore(dispute.Title, dispute.Claimant) >=
                    TitleModel.DucalLegitimacyScore(dispute.Title, holder);
            return 65 * attackerAdvantage + 0.18f * holder.GetRelation(dispute.Claimant)
                + 2 * (Standing(dispute.Claimant.Clan) - Standing(holder.Clan))
                - (holder.Clan.Fiefs.Count <= 1 ? 12 : 0)
                - (holder.Culture == dispute.Title.Fief.Culture ? 5 : 0) > 20;
        }
    }
}