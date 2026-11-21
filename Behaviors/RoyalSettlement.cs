using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace CasusBelli
{
    internal static class RoyalSettlement
    {
        internal static List<Settlement> EligibleFiefs(Kingdom captor, Kingdom captive, WarRecord war)
        {
            var home = captor.Settlements.Where(s => s.IsFortification).ToList();
            var score = Campaign.Current.GetCampaignBehavior<CasusBelliWarScoreBehavior>();
            var candidates = new HashSet<Settlement>();
            foreach (var fort in home)
            {
                foreach (var neighbor in score.Neighbors(fort))
                    if (neighbor.OwnerClan?.Kingdom == captive) candidates.Add(neighbor);
            }
            if (war != null)
                foreach (var fort in home.Where(s => score.WasTaken(war, captor, s))) candidates.Add(fort);
            return candidates.OrderBy(s => home.Min(b => b.Position.DistanceSquared(s.Position)))
                .ThenBy(s => s.StringId, StringComparer.Ordinal).ToList();
        }

        internal static void ConfirmPossession(Settlement target)
        {
            var holder = target.OwnerClan?.Leader;
            if (holder != null)
                Managers.Titles.TitleManager.Instance.ApplyOwnerChange(target, holder);
        }

        private static List<Hero> Lords(Kingdom realm) => realm.Clans
            .Where(c => !c.IsEliminated && !c.IsUnderMercenaryService)
            .SelectMany(c => c.Heroes).Where(h => h.IsAlive && h.IsLord && !h.IsChild)
            .OrderBy(h => h.StringId, StringComparer.Ordinal).ToList();

        internal static int Available(Kingdom realm) => (int)Math.Min(int.MaxValue,
            Lords(realm).Sum(h => (long)Math.Max(0, h.Gold)));

        internal static int Pay(Kingdom captive, Kingdom captor, int amount, Hero capturer, WarRecord war)
        {
            var score = Campaign.Current.GetCampaignBehavior<CasusBelliWarScoreBehavior>();
            var recipients = Lords(captor).Where(h => h != capturer && war != null &&
                score != null && score.Fought(war, captor, h)).ToList();
            var treasury = capturer ?? recipients.FirstOrDefault() ?? captor.Leader;
            if (treasury == null) return 0;
            // Smaller fortunes pay at most their available gold; the rest share the shortfall.
            var payers = Lords(captive).OrderBy(h => h.Gold).ToList();
            int remaining = amount;
            for (int i = 0; i < payers.Count && remaining > 0; i++)
            {
                int count = payers.Count - i;
                int share = Math.Min(Math.Max(0, payers[i].Gold), remaining / count + (remaining % count == 0 ? 0 : 1));
                if (share > 0) GiveGoldAction.ApplyBetweenCharacters(payers[i], treasury, share, true);
                remaining -= share;
            }
            int paid = amount - remaining;
            int pool = capturer == null ? paid : paid - paid / 2;
            for (int i = 0; i < recipients.Count; i++)
            {
                int share = pool / recipients.Count + (i < pool % recipients.Count ? 1 : 0);
                if (share > 0 && recipients[i] != treasury)
                    GiveGoldAction.ApplyBetweenCharacters(treasury, recipients[i], share, true);
            }
            return paid;
        }
    }
}
