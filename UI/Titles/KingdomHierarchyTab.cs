using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Kingdom;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli.UI.Titles
{
    [HarmonyPatch(typeof(GauntletLayer), "LoadMovie", new[] { typeof(GauntletMovieIdentifier) })]
    internal static class KingdomHierarchyTab
    {
        [HarmonyPostfix]
        private static void AfterLoad(GauntletMovieIdentifier identifier)
        {
            if (identifier.MovieName != "KingdomManagement" || !(identifier.DataSource is KingdomManagementVM screen))
                return;

            var root = identifier.Movie.RootWidget;
            var tabs = root.GetAllChildrenRecursive().OfType<KingdomTabControlListPanel>().FirstOrDefault();
            var fiefs = tabs?.FiefsButton;
            if (fiefs == null || !(fiefs.FindChild(w => w is TextWidget) is TextWidget caption))
            {
                DebugLog.Log("[TitleSystem] Aba Feudos não encontrada no menu do reino.");
                return;
            }

            if (tabs.FindChild("CbKingdomTab") != null) return;
            var nativeButtons = tabs.Children.OfType<ButtonWidget>().ToList();
            var panel = new BrushWidget(root.Context)
            {
                Brush = root.Context.GetBrush("Standard.Background.Brush"),
                Id = "CbKingdomPanel", WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.StretchToParent, MarginTop = 161, MarginBottom = 80,
                IsVisible = false
            };
            root.AddChild(panel);
            var row = new ListPanel(root.Context)
            {
                WidthSizePolicy = SizePolicy.CoverChildren, HeightSizePolicy = SizePolicy.CoverChildren,
                HorizontalAlignment = HorizontalAlignment.Center,
                StackLayout = { LayoutMethod = TaleWorlds.GauntletUI.Layout.LayoutMethod.HorizontalLeftToRight }
            };
            panel.AddChild(row);
            var summary = new TextWidget(root.Context)
            {
                WidthSizePolicy = SizePolicy.CoverChildren, HeightSizePolicy = SizePolicy.CoverChildren,
                HorizontalAlignment = HorizontalAlignment.Center, MarginTop = 100, Brush = caption.ReadOnlyBrush
            };
            panel.AddChild(summary);
            var main = CreateButton(fiefs, caption, "CbKingdomTab", "CasusBelli", () =>
            {
                if (screen.Kingdom == null) return;
                summary.Text = $"{screen.Kingdom.Name}\nSoberano: {screen.Kingdom.Leader?.Name}";
                panel.IsVisible = true;
            });
            tabs.AddChildAtIndex(main, tabs.GetChildIndex(fiefs) + 1);
            foreach (var native in nativeButtons)
                native.ClickEventHandlers.Add(w => { panel.IsVisible = false; });
            void Section(string id, string name, System.Action<Kingdom> open)
            {
                var button = CreateButton(fiefs, caption, id, name, () => open(screen.Kingdom));
                button.WidthSizePolicy = SizePolicy.Fixed;
                button.SuggestedWidth = 182;
                row.AddChild(button);
            }
            Section("CbHierarchy", "Hierarquia", OpenHierarchy);
            Section("CbPeers", "Pares do reino", k => RealmPeers.Show(k, () => { }));
            Section("CbCouncil", "Conselho", k => RoyalCouncilMenu.Open(k, () => { }));
            Section("CbDisputes", "Disputas internas", k => DisputeMenu.Open(k, () => { }));
        }

        private static ButtonWidget CreateButton(ButtonWidget fiefs, TextWidget caption, string id, string title, System.Action open)
        {
            var button = new ButtonWidget(fiefs.Context)
            {
                Id = id,
                WidthSizePolicy = fiefs.WidthSizePolicy,
                HeightSizePolicy = fiefs.HeightSizePolicy,
                SuggestedWidth = fiefs.SuggestedWidth,
                SuggestedHeight = fiefs.SuggestedHeight,
                VerticalAlignment = fiefs.VerticalAlignment,
                PositionYOffset = fiefs.PositionYOffset,
                Brush = fiefs.ReadOnlyBrush,
                UpdateChildrenStates = true,
                DoNotPassEventsToChildren = true
            };
            var label = new TextWidget(fiefs.Context)
            {
                WidthSizePolicy = SizePolicy.CoverChildren,
                HeightSizePolicy = SizePolicy.CoverChildren,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Brush = caption.ReadOnlyBrush,
                Text = title
            };
            button.AddChild(label);
            button.ClickEventHandlers.Add(widget => open());
            return button;
        }

        private static void OpenHierarchy(Kingdom kingdom)
        {
            if (kingdom == null)
            {
                InformationManager.ShowInquiry(new InquiryData("Hierarquia", "Nenhum reino selecionado.",
                    true, false, "Fechar", "", null, null));
                return;
            }
            ShowBranch(kingdom, new Stack<FeudalTitle>());
        }

        private static void ShowBranch(Kingdom kingdom, Stack<FeudalTitle> path)
        {
            var current = path.Count > 0 ? path.Peek() : null;
            var children = current == null ? TitleManager.Instance.GetSovereignTitleList(kingdom) : current.Vassals;
            var entries = children.Where(t => t != null && !path.Contains(t))
                .OrderBy(t => t.TitleType).ThenBy(t => t.ShortName?.ToString())
                .Select(t => new InquiryElement(t, $"{t.FullName} — {Holder(t)}", null))
                .ToList();

            string heading = current?.FullName?.ToString() ?? $"Hierarquia de {kingdom.Name}";
            string description = current == null ? "Títulos soberanos" : $"Titular de jure: {Holder(current)}";
            void GoBack()
            {
                if (path.Count == 0) return;
                path.Pop();
                ShowBranch(kingdom, path);
            }

            string back = current == null ? "Fechar" : "Voltar";
            if (current == null)
            {
                entries.Add(new InquiryElement("search", "Fabricar reivindicação em outro título…", null));
            }
            else
            {
                var claim = TitleManager.Instance.GetModel().GetClaimAction(current, Hero.MainHero);
                entries.Add(new InquiryElement("claim", "Fabricar reivindicação", null, claim.Possible, claim.Reason.ToString()));
                if (current.OngoingClaims.TryGetValue(Hero.MainHero, out var deadline))
                    description += $"\nSua reivindicação amadurece em {System.Math.Ceiling(deadline.RemainingDaysFromNow):0} dias.";
            }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                heading, description, entries, true, 1, 1, "Abrir", back,
                selected =>
                {
                    if (selected.Count == 0) return;
                    var choice = selected[0].Identifier;
                    if (choice is string command)
                    {
                        if (command == "search") ChooseClaimTitle(() => ShowBranch(kingdom, path));
                        else ConfirmClaim(current, () => ShowBranch(kingdom, path));
                        return;
                    }
                    path.Push((FeudalTitle)choice);
                    ShowBranch(kingdom, path);
                }, selected => GoBack()));
        }

        internal static void ChooseClaimTitle(System.Action back)
        {
            var manager = TitleManager.Instance;
            var options = new List<InquiryElement>();
            foreach (var title in manager.AllTitles.OrderBy(t => t.ShortName?.ToString()))
            {
                var action = manager.GetModel().GetClaimAction(title, Hero.MainHero);
                options.Add(new InquiryElement(title, $"{title.FullName} — {Holder(title)}", null,
                    action.Possible, action.Reason.ToString()));
            }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Fabricar reivindicação", "Escolha o título pelo nome. Cidades e castelos podem ser objetivos de guerra.",
                options, true, 1, 1, "Ver custos", "Voltar",
                selection =>
                {
                    if (selection.Count == 1) ConfirmClaim((FeudalTitle)selection[0].Identifier, back);
                }, selection => back(), isSeachAvailable: true));
        }

        internal static void ConfirmClaim(FeudalTitle title, System.Action back)
        {
            var manager = TitleManager.Instance;
            var action = manager.GetModel().GetClaimAction(title, Hero.MainHero);
            if (!action.Possible)
            {
                InformationManager.DisplayMessage(new InformationMessage(action.Reason.ToString()));
                back();
                return;
            }
            string details = $"{title.FullName} ({title.StringId})\nTitular: {Holder(title)}\n" +
                $"Custo: {action.Gold:0} de ouro, {action.Influence:0} de influência e {action.Renown:0} de renome.\n" +
                 $"Prazo: {System.Math.Ceiling(CampaignTime.YearsFromNow(CouncilDuties.ClaimYears(Hero.MainHero)).RemainingDaysFromNow)} dias. A fabricação prejudica a relação com o titular.";
            InformationManager.ShowInquiry(new InquiryData("Fabricar reivindicação", details, true, true,
                "Iniciar fabricação", "Voltar", () =>
                {
                    var confirmed = manager.GetModel().GetClaimAction(title, Hero.MainHero);
                    if (confirmed.Possible)
                    {
                        confirmed.TakeAction(null);
                        InformationManager.DisplayMessage(new InformationMessage($"Fabricação iniciada: {title.FullName}."));
                    }
                    else InformationManager.DisplayMessage(new InformationMessage(confirmed.Reason.ToString()));
                    back();
                }, back));
        }

        private static string Holder(FeudalTitle title) => title.deJure?.Name?.ToString() ?? "Vago";
    }
}
