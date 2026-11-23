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
            var hierarchy = new EmbeddedHierarchy(panel, fiefs, caption);
            var main = CreateButton(fiefs, caption, "CbKingdomTab", "CasusBelli", () =>
            {
                if (screen.Kingdom == null) return;
                panel.IsVisible = true;
                hierarchy.Open(screen.Kingdom);
            });
            tabs.AddChildAtIndex(main, tabs.GetChildIndex(fiefs) + 1);
            foreach (var native in nativeButtons)
                native.ClickEventHandlers.Add(w => { hierarchy.Hide(); panel.IsVisible = false; });
            void Section(string id, string name, System.Action<Kingdom> open)
            {
                var button = CreateButton(fiefs, caption, id, name, () => open(screen.Kingdom));
                button.WidthSizePolicy = SizePolicy.Fixed;
                button.SuggestedWidth = 182;
                row.AddChild(button);
            }
            Section("CbHierarchy", "Hierarquia", hierarchy.Open);
            Section("CbPeers", "Pares do reino", k => RealmPeers.Show(k, () => { }));
            Section("CbCouncil", "Conselho", k => RoyalCouncilMenu.Open(k, () => { }));
            Section("CbPolitics", "Política", PoliticsMenu.Open);
            Section("CbDisputes", "Disputas internas", k => DisputeMenu.Open(k, () => { }));
        }


        // The hierarchy is embedded in the existing kingdom tab. Only one branch page is rendered.
        private sealed class EmbeddedHierarchy
        {
            private const int PageSize = 5;
            private readonly Widget _body;
            private readonly Widget _left;
            private readonly Widget _right;
            private readonly ButtonWidget _template;
            private readonly TextWidget _caption;
            private readonly Dictionary<Hero, TaleWorlds.Core.ViewModelCollection.ImageIdentifiers.CharacterImageIdentifierVM> _portraits =
                new Dictionary<Hero, TaleWorlds.Core.ViewModelCollection.ImageIdentifiers.CharacterImageIdentifierVM>();
            private readonly Dictionary<Kingdom, TaleWorlds.Core.ViewModelCollection.ImageIdentifiers.BannerImageIdentifierVM> _banners =
                new Dictionary<Kingdom, TaleWorlds.Core.ViewModelCollection.ImageIdentifiers.BannerImageIdentifierVM>();
            private readonly Stack<FeudalTitle> _parents = new Stack<FeudalTitle>();
            private Kingdom _realm;
            private FeudalTitle _title;
            private bool _deFacto;
            private int _page;
            private int _realmPage;
            private readonly Dictionary<FeudalTitle, FeudalTitle> _legalParents = new Dictionary<FeudalTitle, FeudalTitle>();

            internal EmbeddedHierarchy(Widget panel, ButtonWidget template, TextWidget caption)
            {
                _template = template; _caption = caption;
                _body = new Widget(panel.Context) { WidthSizePolicy = SizePolicy.StretchToParent, HeightSizePolicy = SizePolicy.StretchToParent,
                    MarginTop = 75, MarginBottom = 12, MarginLeft = 20, MarginRight = 20 };
                panel.AddChild(_body);
                _left = new BrushWidget(panel.Context) { WidthSizePolicy = SizePolicy.Fixed, SuggestedWidth = 280,
                    HeightSizePolicy = SizePolicy.StretchToParent, Brush = panel.Context.GetBrush("Standard.Background.Brush") };
                _right = new Widget(panel.Context) { WidthSizePolicy = SizePolicy.StretchToParent, HeightSizePolicy = SizePolicy.StretchToParent,
                    MarginLeft = 300, MarginRight = 15 };
                _body.AddChild(_left); _body.AddChild(_right);
            }

            internal void Open(Kingdom kingdom)
            {
                if (kingdom == null) return;
                _body.IsVisible = true;
                _deFacto = false;
                _realmPage = 0;
                SelectRealm(kingdom);
            }

            internal void Hide()
            {
                _body.IsVisible = false;
                _right.RemoveAllChildren(); _left.RemoveAllChildren();
                _portraits.Clear(); _banners.Clear();
            }

            private void SelectRealm(Kingdom kingdom)
            {
                _realm = kingdom; _parents.Clear(); _page = 0;
                _legalParents.Clear();
                foreach (var t in TitleManager.Instance.AllTitles)
                    if (t?.Vassals != null)
                        foreach (var child in t.Vassals)
                            if (child != null && !_legalParents.ContainsKey(child)) _legalParents.Add(child, t);
                var roots = TitleManager.Instance.GetSovereignTitleList(kingdom).Where(t => t != null).ToList();
                _title = roots.Count == 1 ? roots[0] : null;
                Draw();
            }

            private void ChangeMode(bool deFacto)
            {
                _deFacto = deFacto;
                if (_realm != null) SelectRealm(_realm);
            }

            private Hero Owner(FeudalTitle t) => t == null ? null : _deFacto ? t.DeFactoHolder : t.deJure;
            private Kingdom EffectiveRealm(FeudalTitle t) => t?.DeFactoHolder?.Clan?.Kingdom;
            private List<FeudalTitle> Children()
            {
                var manager = TitleManager.Instance;
                var roots = manager.GetSovereignTitleList(_realm);
                if (_title == null) return roots.Where(t => t != null).ToList();
                if (!_deFacto) return (_title.Vassals ?? new List<FeudalTitle>()).Where(t => t != null && t != _title && !_parents.Contains(t)).Distinct().ToList();
                // Preserve immediate legal links if both ends remain in the same effective realm.
                // An occupied title whose parent remains abroad falls directly under the effective crown.
                return manager.AllTitles.Where(t => {
                    if (t == null || t == _title || _parents.Contains(t) || roots.Contains(t) || EffectiveRealm(t) != _realm) return false;
                    _legalParents.TryGetValue(t, out var parent);
                    if (parent != null && (EffectiveRealm(parent) == _realm || roots.Contains(parent))) return parent == _title;
                    return _title == manager.GetSovereignTitle(_realm);
                }).Distinct().ToList();
            }

            private void Descend(FeudalTitle t)
            {
                _parents.Push(_title); _title = t; _page = 0; Draw();
            }
            private void Ascend()
            {
                if (_parents.Count == 0) return;
                _title = _parents.Pop(); _page = 0; Draw();
            }
            private TextWidget Label(Widget parent, string value, float width, float height, float top = 0)
            {
                var label = new TextWidget(parent.Context) { WidthSizePolicy = width > 0 ? SizePolicy.Fixed : SizePolicy.StretchToParent,
                    SuggestedWidth = width, HeightSizePolicy = SizePolicy.Fixed, SuggestedHeight = height,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
                    MarginTop = top, Brush = _caption.ReadOnlyBrush, Text = value, DoNotAcceptEvents = true };
                parent.AddChild(label); return label;
            }
            private ButtonWidget Button(Widget parent, string text, System.Action action, float width, float height = 46)
            {
                var b = CreateButton(_template, _caption, "", text, action);
                b.WidthSizePolicy = SizePolicy.Fixed; b.SuggestedWidth = width;
                b.HeightSizePolicy = SizePolicy.Fixed; b.SuggestedHeight = height;
                b.PositionYOffset = 0; b.VerticalAlignment = VerticalAlignment.Top;
                parent.AddChild(b); return b;
            }
            private ListPanel Row(Widget parent, float top)
            {
                var r = new ListPanel(parent.Context) { WidthSizePolicy = SizePolicy.CoverChildren, HeightSizePolicy = SizePolicy.CoverChildren,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, MarginTop = top,
                    StackLayout = { LayoutMethod = TaleWorlds.GauntletUI.Layout.LayoutMethod.HorizontalLeftToRight } };
                parent.AddChild(r); return r;
            }
            private Widget Line(Widget parent, float width, float height, float top)
            {
                var line = new Widget(parent.Context) { WidthSizePolicy = SizePolicy.Fixed, HeightSizePolicy = SizePolicy.Fixed,
                    SuggestedWidth = width, SuggestedHeight = height, HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top, MarginTop = top, DoNotAcceptEvents = true,
                    Color = Color.FromUint(0xC3A05EFF) };
                // SpriteData is not guaranteed while Gauntlet is still constructing or in the
                // lightweight integration harness. Keep hierarchy navigation usable even then;
                // the connector simply remains invisible until a fully initialized UI context.
                if (parent.Context.SpriteData != null)
                    line.Sprite = parent.Context.SpriteData.GetSprite("BlankWhiteSquare_9");
                parent.AddChild(line); return line;
            }
            private void Banner(Widget parent, Kingdom kingdom, float size)
            {
                if (kingdom?.Banner == null) return;
                if (!_banners.TryGetValue(kingdom, out var id))
                    _banners[kingdom] = id = new TaleWorlds.Core.ViewModelCollection.ImageIdentifiers.BannerImageIdentifierVM(kingdom.Banner, true);
                var img = new MaskedTextureWidget(parent.Context) {
                    WidthSizePolicy = SizePolicy.Fixed, HeightSizePolicy = SizePolicy.Fixed, SuggestedWidth = size, SuggestedHeight = size,
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, MarginLeft = 8,
                    DoNotAcceptEvents = true, Brush = parent.Context.GetBrush("Flat.Tuple.Banner.Small.Hero"),
                    ImageId = id.Id, AdditionalArgs = id.AdditionalArgs, TextureProviderName = id.TextureProviderName };
                parent.AddChild(img);
            }
            private void Portrait(Widget parent, Hero hero)
            {
                if (hero?.CharacterObject == null) return;
                try
                {
                    if (!_portraits.TryGetValue(hero, out var id))
                        _portraits[hero] = id = new TaleWorlds.Core.ViewModelCollection.ImageIdentifiers.CharacterImageIdentifierVM(CharacterCode.CreateFrom(hero.CharacterObject));
                    var img = new TaleWorlds.MountAndBlade.GauntletUI.Widgets.ImageIdentifierWidget(parent.Context) {
                        WidthSizePolicy = SizePolicy.Fixed, HeightSizePolicy = SizePolicy.Fixed, SuggestedWidth = 105, SuggestedHeight = 82,
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, MarginTop = 5,
                        DoNotAcceptEvents = true, ImageId = id.Id, AdditionalArgs = id.AdditionalArgs, TextureProviderName = id.TextureProviderName };
                    parent.AddChild(img);
                }
                catch (System.Exception ex) { DebugLog.Log("[Hierarchy] Portrait: " + ex.Message); }
            }
            private void Card(Widget parent, FeudalTitle title, string name, Hero owner, System.Action click, float width, bool connector)
            {
                var slot = new Widget(parent.Context) {
                    WidthSizePolicy = SizePolicy.Fixed, HeightSizePolicy = SizePolicy.Fixed, SuggestedWidth = width, SuggestedHeight = connector ? 178 : 154,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, MarginLeft = 6, MarginRight = 6 };
                parent.AddChild(slot);
                if (connector) Line(slot, 2, 24, 0);
                var card = new ButtonWidget(parent.Context) {
                    WidthSizePolicy = SizePolicy.Fixed, HeightSizePolicy = SizePolicy.Fixed, SuggestedWidth = width, SuggestedHeight = 154,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, MarginTop = connector ? 24 : 0,
                    Brush = parent.Context.GetBrush("Encyclopedia.SubPage.Element"), UpdateChildrenStates = true, DoNotPassEventsToChildren = true };
                slot.AddChild(card);
                Portrait(card, owner);
                Label(card, name, width - 6, 34, 84);
                Label(card, owner?.Name?.ToString() ?? "Vago", width - 6, 30, 117);
                card.ClickEventHandlers.Add(w => click());
            }
            private void Draw()
            {
                if (_realm == null) return;
                _left.RemoveAllChildren(); _right.RemoveAllChildren();
                var kingdoms = Kingdom.All.Where(k => !k.IsEliminated).OrderBy(k => k.Name.ToString()).ToList();
                Label(_left, "REINOS", 0, 44);
                const int realmPageSize = 9;
                for (int i = 0; i < realmPageSize && _realmPage * realmPageSize + i < kingdoms.Count; i++)
                {
                    var kingdom = kingdoms[_realmPage * realmPageSize + i];
                    var b = Button(_left, kingdom.Name.ToString(), () => SelectRealm(kingdom), 270, 54);
                    b.HorizontalAlignment = HorizontalAlignment.Center;
                    b.MarginTop = 50 + i * 56;
                    Banner(b, kingdom, 36);
                    b.IsEnabled = kingdom != _realm;
                }
                var nav = Row(_left, 565);
                var prev = Button(nav, "<", () => { _realmPage--; Draw(); }, 65); prev.IsEnabled = _realmPage > 0;
                var next = Button(nav, ">", () => { _realmPage++; Draw(); }, 65); next.IsEnabled = (_realmPage + 1) * realmPageSize < kingdoms.Count;

                Label(_right, "Hierarquia — " + _realm.Name, 0, 50);
                var mode = Row(_right, 55);
                var legal = Button(mode, "De jure", () => ChangeMode(false), 160); legal.IsEnabled = _deFacto;
                var actual = Button(mode, "De facto", () => ChangeMode(true), 160); actual.IsEnabled = !_deFacto;
                var top = Row(_right, 118);
                Card(top, _title, _title?.FullName?.ToString() ?? _realm.Name.ToString(), _title != null ? Owner(_title) : _realm.Leader, Details, 280, false);
                var children = Children().OrderBy(t => t.TitleType).ThenBy(t => t.ShortName?.ToString()).ToList();
                _page = System.Math.Min(_page, System.Math.Max(0, (children.Count - 1) / PageSize));
                var visible = children.Skip(_page * PageSize).Take(PageSize).ToList();
                if (visible.Count > 0)
                {
                    Line(_right, 2, 42, 272);
                    if (visible.Count > 1) Line(_right, (visible.Count - 1) * 210, 2, 314);
                    var row = Row(_right, 314);
                    foreach (var child in visible)
                        Card(row, child, child.FullName.ToString(), Owner(child), () => Descend(child), 198, true);
                }
                Label(_right, children.Count == 0 ? "Sem títulos subordinados" : $"{_page * PageSize + 1}–{System.Math.Min((_page + 1) * PageSize, children.Count)} de {children.Count} títulos", 0, 40, 500);
                var controls = Row(_right, 550);
                Button(controls, "Subir nível", Ascend, 170).IsEnabled = _parents.Count > 0;
                Button(controls, "Anterior", () => { _page--; Draw(); }, 145).IsEnabled = _page > 0;
                Button(controls, "Próxima", () => { _page++; Draw(); }, 145).IsEnabled = (_page + 1) * PageSize < children.Count;
                Button(controls, "Atos e detalhes", Details, 210).IsEnabled = _title != null;
            }
            private void Details()
            {
                if (_title == null) return;
                var t = _title;
                var model = TitleManager.Instance.GetModel();
                var claim = model.GetClaimAction(t, Hero.MainHero);
                var options = new List<InquiryElement> {
                    new InquiryElement("actions", "Atos feudais e reconhecimento", null),
                    new InquiryElement("claim", "Fabricar reivindicação", null, claim.Possible, claim.Reason?.ToString() ?? "")
                };
                string info = $"Titular de jure: {Holder(t)}. Posse de facto: {t.DeFactoHolder?.Name}.\nHerança: {RealmLaw.Translate(t.Contract?.Inheritance?.StringId)}. Sucessão: {RealmLaw.Translate(t.Contract?.Succession?.StringId)}. Lei de gênero: {RealmLaw.Translate(t.Contract?.GenderLaw?.StringId)}.";
                if (t.OngoingClaims.TryGetValue(Hero.MainHero, out var deadline))
                    info += $"\nSua reivindicação amadurece em {System.Math.Ceiling(deadline.RemainingDaysFromNow):0} dias.";
                else if (t.HeroHasValidClaim(Hero.MainHero))
                    info += "\nSua reivindicação está madura.";
                if (t.TitleType == TitleType.Dukedom)
                {
                    info += "\nLegitimidade (diagnóstico experimental):";
                    info += "\nSeu clã: " + TitleModel.DescribeDucalLegitimacy(t, Hero.MainHero);
                    info += "\nTitular: " + TitleModel.DescribeDucalLegitimacy(t, t.deJure);
                    if (t.deJure != Hero.MainHero)
                    {
                        options.Add(new InquiryElement("support", "Consultar apoio político", null));
                        options.Add(new InquiryElement("ducal_dispute", "Abrir disputa ducal", null));
                    }
                }
                void Refresh() { if (_body.IsVisible) Draw(); }
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(t.FullName.ToString(), info, options, true, 1, 1, "Abrir", "Voltar", selected =>
                {
                    if (selected.Count == 0) return;
                    if ((string)selected[0].Identifier == "actions") FeudalActionsMenu.Open(t, Refresh);
                    else if ((string)selected[0].Identifier == "support") DisputeMenu.PreviewDucalSupport(t, Hero.MainHero, Refresh);
                    else if ((string)selected[0].Identifier == "ducal_dispute") DisputeMenu.OpenDucal(t, Refresh);
                    else ConfirmClaim(t, Refresh);
                }, selected => Refresh()));
            }
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
            string description = current == null ? "Títulos soberanos" : $"Titular de jure: {Holder(current)}. Posse de facto: {current.DeFactoHolder?.Name}.\n" +
                $"Herança: {RealmLaw.Translate(current.Contract?.Inheritance?.StringId)}. Sucessão: {RealmLaw.Translate(current.Contract?.Succession?.StringId)}. Lei de gênero: {RealmLaw.Translate(current.Contract?.GenderLaw?.StringId)}.";
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
                entries.Add(new InquiryElement("actions","Atos feudais e reconhecimento",null));
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
                        else if (command == "actions") FeudalActionsMenu.Open(current,() => ShowBranch(kingdom,path));
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