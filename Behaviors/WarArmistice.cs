using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace CasusBelli
{
    public partial class CasusBelliCampaignBehavior
    {
        private void SyncArmistices(IDataStore store)
        {
            var rows = new List<string>();
            if (store.IsSaving)
                foreach (var war in _warRegistry.AllWars.Where(w => w.IsActive && (w.ArmisticeEnds >= 0 || w.LastResumed >= 0)))
                    rows.Add(FormattableString.Invariant($"{war.WarId}|{war.ArmisticeStarted:R}|{war.ArmisticeEnds:R}|{war.LastResumed:R}"));
            store.SyncData("CasusBelli_Armistices_v1", ref rows);
            if (!store.IsLoading || rows == null) return;
            foreach (var row in rows)
            {
                var parts = row.Split('|');
                var war = _warRegistry.AllWars.FirstOrDefault(w => w.WarId == parts[0]);
                if (war == null || parts.Length != 4) continue;
                var culture = CultureInfo.InvariantCulture;
                if (double.TryParse(parts[1], NumberStyles.Float, culture, out var start) &&
                    double.TryParse(parts[2], NumberStyles.Float, culture, out var end) &&
                    double.TryParse(parts[3], NumberStyles.Float, culture, out var resumed))
                {
                    war.ArmisticeStarted = start;
                    war.ArmisticeEnds = end;
                    war.LastResumed = resumed;
                }
            }
        }
    }

    public partial class CasusBelliWarScoreBehavior
    {
        private bool TryStartArmistice(WarRecord war, Kingdom a, Kingdom d, double now)
        {
            double fightingDays = war.LastResumed >= 0 ? now - war.LastResumed : GetWarAgeDays(war);
            if (fightingDays < WhitePeaceMinDays) return false;
            var score = GetBreakdown(war);
            if (score.AttackerHoldsEnemyKing || score.DefenderHoldsEnemyKing || Math.Abs(score.Total) > WhitePeaceBand) return false;
            war.ArmisticeStarted = now;
            war.ArmisticeEnds = CampaignTime.YearsFromNow(1).ToDays;
            MakePeaceAction.Apply(a, d);
            AnnounceIfPlayerKingdom(a, d, $"{a.Name} e {d.Name} assinaram uma trégua de um ano. A guerra permanece registrada.");
            return true;
        }

        private void TickArmistice(WarRecord war, Kingdom a, Kingdom d, double now)
        {
            if (a == null || d == null || a.IsEliminated || d.IsEliminated)
            {
                war.IsActive = false;
                war.Outcome = "PEACE";
                war.EndedOnDay = now;
                return;
            }
            if (now < war.ArmisticeEnds) return;
            if (!a.IsAtWarWith(d)) DeclareWarAction.ApplyByDefault(a, d);
            if (!a.IsAtWarWith(d)) return;
            if (war.ObjectiveAchievedOnDay >= 0)
                war.ObjectiveAchievedOnDay += now - war.ArmisticeStarted;
            war.ArmisticeStarted = war.ArmisticeEnds = -1;
            war.LastResumed = now;
            AnnounceIfPlayerKingdom(a, d, $"Terminou a trégua. {a.Name} e {d.Name} retomaram a guerra.");
        }
    }
}
