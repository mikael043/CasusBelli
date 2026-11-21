using System.Linq;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli.Disputes
{
    internal static class PersonalTitleWar
    {
        internal const int DemandCost = 40;
        internal const int ReplyDays = 7;

        internal static string DemandBlock(TitleDispute dispute)
        {
            if (!dispute.CanDemand) return "A reivindicação precisa estar madura.";
            if (dispute.TruceEnds.IsFuture) return $"Trégua por mais {System.Math.Ceiling(dispute.TruceEnds.RemainingDaysFromNow)} dias.";
            if (!dispute.Offers.Any(o => o.Accepted && o.ForClaimant && dispute.Eligible(o.Recipient)))
                return "É preciso o apoio confirmado de outro clã do reino.";
            if (dispute.Claimant.Clan.Influence < DemandCost)
                return $"O ultimato custa {DemandCost} de influência.";
            var active = Campaign.Current.GetCampaignBehavior<TitleDisputeBehavior>().Active;
            if (active.Any(other => other.Demanded &&
                (other.Claimant.Clan == dispute.Claimant.Clan || other.Holder.Clan == dispute.Claimant.Clan ||
                 other.Claimant.Clan == dispute.Holder.Clan || other.Holder.Clan == dispute.Holder.Clan)))
                return "Um dos dois clãs já é parte principal em outro ultimato ou guerra pessoal.";
            return null;
        }

        internal static void Demand(TitleDispute dispute)
        {
            if (DemandBlock(dispute) != null) return;
            ChangeClanInfluenceAction.Apply(dispute.Claimant.Clan, -DemandCost);
            dispute.ReplyDeadline = CampaignTime.DaysFromNow(ReplyDays);
            dispute.Demanded = true;
            Report(dispute, "O pretendente apresentou um ultimato ao titular.");
        }

        internal static void Reply(TitleDispute dispute, bool yieldTitle)
        {
            if (!dispute.IsActive || !dispute.Demanded || dispute.Contested) return;
            if (yieldTitle) Resolve(dispute, true);
            else
            {
                dispute.Contested = true;
                dispute.NextPeaceOffer = CampaignTime.DaysFromNow(7);
                Report(dispute, "O titular recusou. Começou a guerra pessoal.");
            }
        }

        internal static int Troops(TitleDispute dispute, bool attacker)
        {
            var clans = dispute.Offers.Where(o => o.Accepted && o.ForClaimant == attacker &&
                dispute.Eligible(o.Recipient)).Select(o => o.Recipient)
                .Concat(new[] { dispute.Sponsor(attacker).Clan }).Distinct();
            return clans.Sum(DisputeAi.FieldTroops);
        }

        internal static void Tick(TitleDispute dispute)
        {
            if (!dispute.Contested)
            {
                // Give pending ultimatums from older saves a full response period.
                if (dispute.ReplyDeadline.Equals(default(CampaignTime)))
                    dispute.ReplyDeadline = CampaignTime.DaysFromNow(ReplyDays);
                if (!dispute.ReplyDeadline.IsFuture)
                {
                    Reply(dispute, false);
                    return;
                }
                if (dispute.Holder.Clan == Clan.PlayerClan) return;
            }
            int attack = Troops(dispute, true);
            int defense = Troops(dispute, false);
            if (!dispute.Contested)
            {
                Reply(dispute, attack > 0 && DisputeAi.Yield(dispute, DisputeAi.Advantage(attack, defense)));
                return;
            }
            PersonalPeace.Tick(dispute);
            if (!dispute.Contested || !dispute.IsActive) return;
            // One abstract engagement per day, weighted by the pledged clans' field troops.
            float odds = (attack + 1f) / (attack + defense + 2f);
            dispute.Balance += MBRandom.RandomFloat < odds ? 20 : -20;
            if (System.Math.Abs(dispute.Balance) >= 100) Resolve(dispute, dispute.Balance > 0);
        }

        internal static void Resolve(TitleDispute dispute, bool won)
        {
            if (won)
            {
                ChangeOwnerOfSettlementAction.ApplyByDefault(dispute.Claimant, dispute.Title.Fief);
                TitleManager.Instance.InheritTitle(dispute.Holder, dispute.Claimant, dispute.Title);
            }
            dispute.Title.RemoveClaim(dispute.Claimant);
            Report(dispute, won ? "O pretendente recebeu o título e o feudo." : "O titular derrotou a reivindicação.");
        }

        internal static void Report(TitleDispute dispute, string message)
        {
            if (dispute.Claimant.Clan == Clan.PlayerClan || dispute.Holder.Clan == Clan.PlayerClan ||
                dispute.Offers.Any(o => o.Accepted && o.Recipient == Clan.PlayerClan))
                InformationManager.DisplayMessage(new InformationMessage($"{dispute.Title.FullName}: {message}"));
        }
    }
}

