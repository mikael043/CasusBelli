using System;
using TaleWorlds.CampaignSystem;

namespace CasusBelli
{
    public partial class CasusBelliWarScoreBehavior
    {
        private static bool AttackerClaim(WarRecord war, Kingdom winner) =>
            war.AttackerKingdomId == winner.StringId && war.IsTerritorial;

        public bool CanSurrender(WarRecord war, Kingdom kingdom, out string reason)
        {
            reason = "A guerra já terminou ou o reino não participa.";
            if (war == null || !war.IsActive || kingdom == null ||
                (kingdom.StringId != war.AttackerKingdomId && kingdom.StringId != war.DefenderKingdomId)) return false;
            var enemy = OtherSide(war, kingdom);
            if (enemy == null || !kingdom.IsAtWarWith(enemy)) return false;
            reason = "";
            return true;
        }

        private double AcceptanceThreshold(WarRecord war, Kingdom recipient)
        {
            double threshold = 90 - Math.Min(25, GetWarAgeDays(war) / 4) - 5 * (Eagerness(recipient) - 1);
            if (war.IsTerritorial &&
                !string.IsNullOrEmpty(war.TargetSettlementId))
            {
                var target = TaleWorlds.CampaignSystem.Settlements.Settlement.Find(war.TargetSettlementId);
                if (target != null)
                {
                    var owner = OwnerKingdomId(target);
                    if (owner == recipient.StringId) threshold += 10;
                    else if (owner == OtherSide(war, recipient)?.StringId) threshold -= 10;
                }
            }
            var envoy = CouncilDuties.Officer(OtherSide(war,recipient),CouncilOffice.Chancellor);
            if (envoy != null) threshold -= Math.Min(5,envoy.GetSkillValue(TaleWorlds.Core.DefaultSkills.Charm) / 50f);
            return Math.Min(100, Math.Max(55, threshold));
        }

        public bool CanEnforce(WarRecord war, Kingdom proposer, out string reason)
        {
            if (!CanSurrender(war, proposer, out reason)) return false;
            var recipient = OtherSide(war, proposer);
            double threshold = AcceptanceThreshold(war, recipient);
            if (ScoreFor(war, proposer.StringId) >= threshold) return true;
            reason = $"{recipient.Name} ainda resistirá. Vantagem necessária para aceitar: {Math.Ceiling(threshold)}%.";
            return false;
        }

        private bool WantsWhite(WarRecord war, Kingdom realm)
        {
            double age = war.LastResumed >= 0 ? CampaignTime.Now.ToDays - war.LastResumed : GetWarAgeDays(war);
            double tolerance = WhitePeaceBand + Math.Min(25, Math.Max(0, age - WhitePeaceMinDays) / 5);
            return age >= WhitePeaceMinDays && ScoreFor(war, realm.StringId) <= tolerance;
        }

        public bool CanWhitePeace(WarRecord war, Kingdom proposer, out string reason)
        {
            if (!CanSurrender(war, proposer, out reason)) return false;
            if (WantsWhite(war, OtherSide(war, proposer))) return true;
            reason = GetWarAgeDays(war) < WhitePeaceMinDays
                ? $"O adversário ainda pretende lutar. Reavalia a paz branca após {WhitePeaceMinDays} dias."
                : "O adversário considera sua vantagem suficiente para continuar a guerra.";
            return false;
        }
    }
}
