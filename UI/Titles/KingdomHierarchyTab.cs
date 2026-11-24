using System.Collections.Generic;
using System.Linq;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli.UI.Titles
{
    // Shared title actions used by the prefab-backed kingdom hierarchy and
    // by the campaign menu. Visual construction lives in GUI/Prefabs.
    internal static class KingdomHierarchyTab
    {
        internal static void OpenDetails(FeudalTitle title, System.Action refresh)
        {
            if (title == null) return;
            refresh = refresh ?? (() => { });

            var model = TitleManager.Instance.GetModel();
            var claim = model.GetClaimAction(title, Hero.MainHero);
            var options = new List<InquiryElement>
            {
                new InquiryElement("actions", "Atos feudais e reconhecimento", null),
                new InquiryElement("claim", "Fabricar reivindicação", null,
                    claim.Possible, claim.Reason?.ToString() ?? "")
            };

            string info =
                $"Titular de jure: {Holder(title)}. Posse de facto: {title.DeFactoHolder?.Name}.\n" +
                $"Herança: {RealmLaw.Translate(title.Contract?.Inheritance?.StringId)}. " +
                $"Sucessão: {RealmLaw.Translate(title.Contract?.Succession?.StringId)}. " +
                $"Lei de gênero: {RealmLaw.Translate(title.Contract?.GenderLaw?.StringId)}.";

            if (title.OngoingClaims.TryGetValue(Hero.MainHero, out var deadline))
                info += $"\nSua reivindicação amadurece em {System.Math.Ceiling(deadline.RemainingDaysFromNow):0} dias.";
            else if (title.HeroHasValidClaim(Hero.MainHero))
                info += "\nSua reivindicação está madura.";

            if (title.TitleType == TitleType.Dukedom)
            {
                info += "\nLegitimidade (diagnóstico experimental):";
                info += "\nSeu clã: " + TitleModel.DescribeDucalLegitimacy(title, Hero.MainHero);
                info += "\nTitular: " + TitleModel.DescribeDucalLegitimacy(title, title.deJure);
                if (title.deJure != Hero.MainHero)
                {
                    options.Add(new InquiryElement("support", "Consultar apoio político", null));
                    options.Add(new InquiryElement("ducal_dispute", "Abrir disputa ducal", null));
                }
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                title.FullName.ToString(), info, options, true, 1, 1, "Abrir", "Voltar",
                selected =>
                {
                    if (selected.Count == 0) return;
                    switch ((string)selected[0].Identifier)
                    {
                        case "actions":
                            FeudalActionsMenu.Open(title, refresh);
                            break;
                        case "support":
                            DisputeMenu.PreviewDucalSupport(title, Hero.MainHero, refresh);
                            break;
                        case "ducal_dispute":
                            DisputeMenu.OpenDucal(title, refresh);
                            break;
                        default:
                            ConfirmClaim(title, refresh);
                            break;
                    }
                }, selected => refresh()));
        }

        internal static void ChooseClaimTitle(System.Action back)
        {
            back = back ?? (() => { });
            var manager = TitleManager.Instance;
            var options = new List<InquiryElement>();
            foreach (var title in manager.AllTitles.OrderBy(t => t.ShortName?.ToString()))
            {
                var action = manager.GetModel().GetClaimAction(title, Hero.MainHero);
                options.Add(new InquiryElement(title, $"{title.FullName} — {Holder(title)}", null,
                    action.Possible, action.Reason.ToString()));
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Fabricar reivindicação",
                "Escolha o título pelo nome. Cidades e castelos podem ser objetivos de guerra.",
                options, true, 1, 1, "Ver custos", "Voltar",
                selection =>
                {
                    if (selection.Count == 1)
                        ConfirmClaim((FeudalTitle)selection[0].Identifier, back);
                }, selection => back(), isSeachAvailable: true));
        }

        internal static void ConfirmClaim(FeudalTitle title, System.Action back)
        {
            back = back ?? (() => { });
            if (title == null)
            {
                back();
                return;
            }

            var manager = TitleManager.Instance;
            var action = manager.GetModel().GetClaimAction(title, Hero.MainHero);
            if (!action.Possible)
            {
                InformationManager.DisplayMessage(new InformationMessage(action.Reason.ToString()));
                back();
                return;
            }

            string details =
                $"{title.FullName} ({title.StringId})\nTitular: {Holder(title)}\n" +
                $"Custo: {action.Gold:0} de ouro, {action.Influence:0} de influência e {action.Renown:0} de renome.\n" +
                $"Prazo: {System.Math.Ceiling(CampaignTime.YearsFromNow(CouncilDuties.ClaimYears(Hero.MainHero)).RemainingDaysFromNow)} dias. " +
                "A fabricação prejudica a relação com o titular.";

            InformationManager.ShowInquiry(new InquiryData(
                "Fabricar reivindicação", details, true, true,
                "Iniciar fabricação", "Voltar",
                () =>
                {
                    var confirmed = manager.GetModel().GetClaimAction(title, Hero.MainHero);
                    if (confirmed.Possible)
                    {
                        confirmed.TakeAction(null);
                        InformationManager.DisplayMessage(
                            new InformationMessage($"Fabricação iniciada: {title.FullName}."));
                    }
                    else
                        InformationManager.DisplayMessage(
                            new InformationMessage(confirmed.Reason.ToString()));
                    back();
                }, back));
        }

        internal static string Holder(FeudalTitle title) =>
            title?.deJure?.Name?.ToString() ?? "Vago";
    }
}