using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CasusBelli.UI.Titles
{
    // Helper para UI de títulos
    public static class UIHelper
    {
        // Obter tooltip para um título
        public static TextObject GetTitleTooltip(FeudalTitle title, List<TitleAction> actions)
        {
            if (title == null) return new TextObject("");

            var sb = new StringBuilder();

            sb.AppendLine(title.FullName.ToString());
            sb.AppendLine();

            // Tipo e feudo
            sb.AppendLine(new TextObject("{=BKTitleType}Type: {TYPE}")
                .SetTextVariable("TYPE", TextHelper.GetRankName(title.TitleType)).ToString());

            if (title.Fief != null)
            {
                sb.AppendLine(new TextObject("{=BKTitleFief}Fief: {FIEF}")
                    .SetTextVariable("FIEF", title.Fief.Name).ToString());
            }

            // Detentor legal
            if (title.deJure != null)
            {
                sb.AppendLine(new TextObject("{=BKDeJureHolder}De Jure Holder: {HOLDER}")
                    .SetTextVariable("HOLDER", title.deJure.Name).ToString());
            }
            else
            {
                sb.AppendLine(new TextObject("{=BKNoDeJure}De Jure Holder: None").ToString());
            }

            // Detentor de facto
            var deFacto = title.DeFactoHolder;
            if (deFacto != null && deFacto != title.deJure)
            {
                sb.AppendLine(new TextObject("{=BKDeFactoHolder}De Facto Holder: {HOLDER}")
                    .SetTextVariable("HOLDER", deFacto.Name).ToString());
            }

            // Soberano
            if (title.Sovereign != null)
            {
                sb.AppendLine(new TextObject("{=BKSovereign}Sovereign: {SOVEREIGN}")
                    .SetTextVariable("SOVEREIGN", title.Sovereign.FullName).ToString());
            }

            // Vassalos
            if (title.Vassals != null && title.Vassals.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(new TextObject("{=BKVassals}Vassals ({COUNT}):")
                    .SetTextVariable("COUNT", title.Vassals.Count).ToString());
                foreach (var vassal in title.Vassals.Take(10))
                {
                    string vassalName = vassal.deJure?.Name?.ToString() ?? "Vacant";
                    sb.AppendLine($"  - {TextHelper.GetRankName(vassal.TitleType)} {vassalName}");
                }
                if (title.Vassals.Count > 10)
                    sb.AppendLine($"  ... and {title.Vassals.Count - 10} more");
            }

            // Claims
            if (title.Claims != null && title.Claims.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(new TextObject("{=BKClaims}Claims:").ToString());
                foreach (var claim in title.Claims.Take(5))
                {
                    string claimType = GetClaimTypeName(claim.Value);
                    sb.AppendLine($"  - {claim.Key.Name}: {claimType}");
                }
            }

            // Claims em andamento
            if (title.OngoingClaims != null && title.OngoingClaims.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(new TextObject("{=BKOngoingClaims}Ongoing Claims:").ToString());
                foreach (var claim in title.OngoingClaims.Take(5))
                {
                    int daysLeft = (int)claim.Value.RemainingDaysFromNow;
                    sb.AppendLine($"  - {claim.Key.Name}: {daysLeft} days remaining");
                }
            }

            // Contrato
            if (title.Contract != null)
            {
                sb.AppendLine();
                sb.AppendLine(new TextObject("{=BKContract}Contract:").ToString());
                sb.AppendLine($"  Government: {title.Contract.Government.Name}");
                sb.AppendLine($"  Succession: {title.Contract.Succession.Name}");
                sb.AppendLine($"  Inheritance: {title.Contract.Inheritance.Name}");
                sb.AppendLine($"  Gender Law: {title.Contract.GenderLaw.Name}");

                if (title.Contract.DemesneLaws != null && title.Contract.DemesneLaws.Count > 0)
                {
                    sb.AppendLine("  Demesne Laws:");
                    foreach (var law in title.Contract.DemesneLaws)
                    {
                        sb.AppendLine($"    - {law.Name} (Level {law.Level})");
                    }
                }
            }

            // Ações disponíveis
            if (actions != null && actions.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(new TextObject("{=BKAvailableActions}Available Actions:").ToString());
                foreach (var action in actions)
                {
                    if (action.Possible)
                    {
                        sb.AppendLine($"  - {action.Reason}");
                        if (action.Gold > 0 || action.Influence > 0 || action.Renown > 0)
                        {
                            var costs = new List<string>();
                            if (action.Gold > 0) costs.Add($"{action.Gold:0} Gold");
                            if (action.Influence > 0) costs.Add($"{action.Influence:0} Influence");
                            if (action.Renown > 0) costs.Add($"{action.Renown:0} Renown");
                            sb.AppendLine($"    Cost: {string.Join(", ", costs)}");
                        }
                    }
                }
            }

            return new TextObject(sb.ToString());
        }

        // Nome do tipo de claim
        private static string GetClaimTypeName(ClaimType type)
        {
            return type switch
            {
                ClaimType.Fabricated => "Fabricated",
                ClaimType.PreviousOwner => "Previous Owner",
                ClaimType.DeFacto => "De Facto",
                ClaimType.ClanSplit => "Clan Split",
                ClaimType.Ongoing => "Ongoing",
                _ => "None"
            };
        }

        // Mostrar popup de confirmação de ação
        public static void ShowActionPopup(TitleAction action, TitleVM titleVM)
        {
            if (action == null || !action.Possible) return;

            var title = action.Title;
            if (title == null) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(action.Reason.ToString());
            sb.AppendLine();

            var costs = new List<string>();
            if (action.Gold > 0) costs.Add($"{action.Gold:0} Gold");
            if (action.Influence > 0) costs.Add($"{action.Influence:0} Influence");
            if (action.Renown > 0) costs.Add($"{action.Renown:0} Renown");

            if (costs.Count > 0)
            {
                sb.AppendLine(new TextObject("{=BKActionCost}Cost: {COSTS}")
                    .SetTextVariable("COSTS", string.Join(", ", costs)).ToString());
            }

            if (!action.Reason.ToString().Contains("Usurp") && !action.Reason.ToString().Contains("Revoke"))
            {
                sb.AppendLine();
                sb.AppendLine(new TextObject("{=BKConfirmAction}Are you sure you want to proceed?").ToString());
            }

            InformationManager.ShowInquiry(new InquiryData(
                action.Type.ToString(),
                sb.ToString(),
                true,
                true,
                GameTexts.FindText("str_yes").ToString(),
                GameTexts.FindText("str_no").ToString(),
                () => action.TakeAction(null),
                null));
        }
    }
}