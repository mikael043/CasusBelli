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
            private readonly Widget _body;
            private readonly Widget _left;
            private readonly Widget _right;
            private readonly ButtonWidget _template;
            private readonly TextWidget _caption;
            private readonly Dictionary<Kingdom, TaleWorlds.Core.ViewModelCollection.ImageIdentifiers.BannerImageIdentifierVM> _banners =
                new Dictionary<Kingdom, TaleWorlds.Core.ViewModelCollection.ImageIdentifiers.BannerImageIdentifierVM>();
            private readonly HashSet<FeudalTitle> _expanded = new HashSet<FeudalTitle>();
            private FeudalHierarchyTree _tree;
            private ScrollablePanel _scroll;
            private ListPanel _items;
            private TextWidget _selectionInfo;
            private TextWidget _countInfo;
            private Kingdom _realm;
            private FeudalTitle _title;
            private bool _deFacto;
            private int _realmPage;

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
                _right.RemoveAllChildren();
                _left.RemoveAllChildren();
                _scroll = null;
                _items = null;
                _selectionInfo = null;
                _countInfo = null;
                _banners.Clear();
            }

            private void SelectRealm(Kingdom kingdom)
            {
                if (kingdom == null || TitleManager.Instance == null) return;
                _realm = kingdom;
                RebuildModel(true);
                Draw();
            }

            private void RebuildModel(bool expandAll)
            {
                var manager = TitleManager.Instance;
                _tree = FeudalHierarchyTree.Build(
                    _realm, _deFacto, manager.GetSovereignTitleList(_realm),
                    manager.AllTitles);
                if (expandAll) _expanded.Clear();
                _expanded.RemoveWhere(t => !_tree.Titles.Contains(t));
                // Open the crown and duchies by default. Counties, baronies
                // and lordships remain one click away, keeping large kingdoms fast.
                if (expandAll)
                    foreach (var t in _tree.Titles)
                        if (t.TitleType <= TitleType.Dukedom) _expanded.Add(t);
                if (_title == null || !_tree.Titles.Contains(_title))
                    _title = _tree.Roots.FirstOrDefault();
            }

            private void ChangeMode(bool deFacto)
            {
                if (_deFacto == deFacto || _realm == null) return;
                _deFacto = deFacto;
                RebuildModel(true);
                Draw();
            }

            private Hero Owner(FeudalTitle t) =>
                t == null ? null : _deFacto ? t.DeFactoHolder : t.deJure;

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
            private ButtonWidget TreeButton(Widget parent, string text, System.Action action,
                float width, bool stretch = false)
            {
                var b = CreateButton(_template, _caption, "", text, action);
                b.WidthSizePolicy = stretch ? SizePolicy.StretchToParent : SizePolicy.Fixed;
                if (!stretch) b.SuggestedWidth = width;
                b.HeightSizePolicy = SizePolicy.Fixed;
                b.SuggestedHeight = 39;
                b.VerticalAlignment = VerticalAlignment.Center;
                b.PositionYOffset = 0;
                var label = b.FindChild(w => w is TextWidget) as TextWidget;
                if (label != null)
                {
                    label.HorizontalAlignment = HorizontalAlignment.Left;
                    label.WidthSizePolicy = SizePolicy.StretchToParent;
                    label.MarginLeft = 10;
                    label.MarginRight = 6;
                    label.CanBreakWords = false;
                }
                parent.AddChild(b);
                return b;
            }

            private void SelectTitle(FeudalTitle title)
            {
                _title = title;
                UpdateSelection();
            }

            private void UpdateSelection()
            {
                if (_selectionInfo == null) return;
                _selectionInfo.Text = _title == null ? "Nenhum título selecionado" :
                    $"{_title.FullName}  |  Titular {(_deFacto ? "atual" : "de jure")}: {Owner(_title)?.Name?.ToString() ?? "Vago"}";
            }

            private void Toggle(FeudalTitle title)
            {
                if (!_expanded.Remove(title)) _expanded.Add(title);
                DrawTree();
            }

            private void ExpandAll(bool expand)
            {
                _expanded.Clear();
                if (expand && _tree != null)
                    foreach (var title in _tree.Titles) _expanded.Add(title);
                DrawTree();
            }

            // Renders the complete expanded outline into one native scrollable panel.
            // The scrollbar and the panel survive expansion, preserving scroll position.
            private void DrawTree()
            {
                if (_items == null || _tree == null) return;
                _items.RemoveAllChildren();
                var visible = _tree.Visible(_expanded);
                if (_countInfo != null)
                    _countInfo.Text = $"{visible.Count} de {_tree.Count} títulos visíveis  •  {(_deFacto ? "Controle de facto" : "Hierarquia de jure")}";

                foreach (var entry in visible)
                {
                    var title = entry.Title;
                    var line = new ListPanel(_items.Context)
                    {
                        Id = "CbTitleRow_" + (title.StringId ?? ""),
                        WidthSizePolicy = SizePolicy.StretchToParent,
                        HeightSizePolicy = SizePolicy.Fixed,
                        SuggestedHeight = 42,
                        StackLayout = { LayoutMethod = TaleWorlds.GauntletUI.Layout.LayoutMethod.HorizontalLeftToRight }
                    };
                    _items.AddChild(line);

                    if (entry.Depth > 0)
                        line.AddChild(new Widget(line.Context)
                        {
                            WidthSizePolicy = SizePolicy.Fixed,
                            HeightSizePolicy = SizePolicy.Fixed,
                            SuggestedWidth = System.Math.Min(180, entry.Depth * 26),
                            SuggestedHeight = 39,
                            DoNotAcceptEvents = true
                        });

                    var toggle = TreeButton(line,
                        entry.ChildCount == 0 ? "·" : (_expanded.Contains(title) ? "−" : "+"),
                        () => Toggle(title), 38);
                    toggle.Id = "CbToggleTitle_" + (title.StringId ?? "");
                    toggle.IsEnabled = entry.ChildCount > 0;

                    string person = Owner(title)?.Name?.ToString() ?? "Vago";
                    string name = $"{title.FullName}  •  {person}";
                    var button = TreeButton(line, name, () => SelectTitle(title), 0, true);
                    button.Id = "CbSelectTitle_" + (title.StringId ?? "");
                    TreeButton(line, "Detalhes", () =>
                    {
                        SelectTitle(title);
                        Details();
                    }, 100);
                }
            }

            private void Draw()
            {
                if (_realm == null || _tree == null) return;
                _left.RemoveAllChildren();
                _right.RemoveAllChildren();
                _scroll = null;
                _items = null;

                var kingdoms = Kingdom.All.Where(k => !k.IsEliminated)
                    .OrderBy(k => k.Name.ToString()).ToList();
                Label(_left, "REINOS", 0, 44);
                const int realmPageSize = 9;
                _realmPage = System.Math.Max(0, System.Math.Min(_realmPage,
                    System.Math.Max(0, (kingdoms.Count - 1) / realmPageSize)));
                for (int i = 0; i < realmPageSize &&
                    _realmPage * realmPageSize + i < kingdoms.Count; i++)
                {
                    var kingdom = kingdoms[_realmPage * realmPageSize + i];
                    var button = Button(_left, kingdom.Name.ToString(),
                        () => SelectRealm(kingdom), 270, 54);
                    button.HorizontalAlignment = HorizontalAlignment.Center;
                    button.MarginTop = 50 + i * 56;
                    Banner(button, kingdom, 36);
                    button.IsEnabled = kingdom != _realm;
                }
                var navigation = Row(_left, 565);
                Button(navigation, "<", () => { _realmPage--; Draw(); }, 65)
                    .IsEnabled = _realmPage > 0;
                Button(navigation, ">", () => { _realmPage++; Draw(); }, 65)
                    .IsEnabled = (_realmPage + 1) * realmPageSize < kingdoms.Count;

                Label(_right, "ÁRVORE FEUDAL  •  " + _realm.Name, 0, 48);
                var mode = Row(_right, 49);
                Button(mode, "De jure", () => ChangeMode(false), 154).IsEnabled = _deFacto;
                Button(mode, "De facto", () => ChangeMode(true), 154).IsEnabled = !_deFacto;
                var toolbar = Row(_right, 100);
                Button(toolbar, "Expandir tudo", () => ExpandAll(true), 166).Id = "CbExpandAll";
                Button(toolbar, "Recolher tudo", () => ExpandAll(false), 166).Id = "CbCollapseAll";
                Button(toolbar, "Atualizar", () => { RebuildModel(false); Draw(); }, 146);
                _countInfo = Label(_right, "", 0, 30, 149);

                // Same native ScrollablePanel / ClipRect / InnerPanel / scrollbar
                // arrangement used by Bannerlord's own initial menu.
                var viewport = new Widget(_right.Context)
                {
                    Id = "CbHierarchyViewport",
                    WidthSizePolicy = SizePolicy.StretchToParent,
                    HeightSizePolicy = SizePolicy.StretchToParent,
                    VerticalAlignment = VerticalAlignment.Top,
                    MarginTop = 185,
                    MarginBottom = 112,
                    MarginLeft = 10,
                    MarginRight = 12
                };
                _right.AddChild(viewport);
                _scroll = new ScrollablePanel(viewport.Context)
                {
                    Id = "CbHierarchyScroll",
                    WidthSizePolicy = SizePolicy.StretchToParent,
                    HeightSizePolicy = SizePolicy.StretchToParent,
                    AutoHideScrollBars = true,
                    AutoAdjustScrollbarHandleSize = true,
                    OnlyAcceptScrollEventIfCanScroll = true
                };
                viewport.AddChild(_scroll);
                var clip = new Widget(viewport.Context)
                {
                    Id = "CbHierarchyClip",
                    WidthSizePolicy = SizePolicy.StretchToParent,
                    HeightSizePolicy = SizePolicy.StretchToParent,
                    MarginRight = 17,
                    ClipContents = true
                };
                _scroll.AddChild(clip);
                _items = new ListPanel(viewport.Context)
                {
                    Id = "CbHierarchyTree",
                    WidthSizePolicy = SizePolicy.StretchToParent,
                    HeightSizePolicy = SizePolicy.CoverChildren,
                    VerticalAlignment = VerticalAlignment.Top,
                    StackLayout = { LayoutMethod = TaleWorlds.GauntletUI.Layout.LayoutMethod.VerticalTopToBottom }
                };
                clip.AddChild(_items);
                _scroll.ClipRect = clip;
                _scroll.InnerPanel = _items;

                var scrollbar = new ScrollbarWidget(viewport.Context)
                {
                    WidthSizePolicy = SizePolicy.Fixed,
                    SuggestedWidth = 12,
                    HeightSizePolicy = SizePolicy.StretchToParent,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    AlignmentAxis = AlignmentAxis.Vertical,
                    MinValue = 0,
                    MaxValue = 100
                };
                var handle = new Widget(viewport.Context)
                {
                    WidthSizePolicy = SizePolicy.Fixed,
                    HeightSizePolicy = SizePolicy.Fixed,
                    SuggestedWidth = 7,
                    SuggestedHeight = 28,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Color = Color.FromUint(0xC3A05EFF)
                };
                if (viewport.Context.SpriteData != null)
                    handle.Sprite = viewport.Context.SpriteData.GetSprite("BlankWhiteSquare_9");
                scrollbar.AddChild(handle);
                scrollbar.Handle = handle;
                viewport.AddChild(scrollbar);
                _scroll.VerticalScrollbar = scrollbar;

                _selectionInfo = Label(_right, "", 0, 34);
                _selectionInfo.Id = "CbSelectedTitle";
                _selectionInfo.VerticalAlignment = VerticalAlignment.Bottom;
                _selectionInfo.MarginTop = 0;
                _selectionInfo.MarginBottom = 63;
                UpdateSelection();

                var footer = Row(_right, 0);
                footer.VerticalAlignment = VerticalAlignment.Bottom;
                footer.MarginTop = 0;
                footer.MarginBottom = 6;
                Button(footer, "Atos e detalhes", Details, 202).IsEnabled = _title != null;
                Button(footer, "Fabricar reivindicação",
                    () => ChooseClaimTitle(() => { if (_body.IsVisible) Draw(); }), 240);
                DrawTree();
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