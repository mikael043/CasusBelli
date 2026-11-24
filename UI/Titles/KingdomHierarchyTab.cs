using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Encyclopedia;
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


        // Feudal diagram: crowns above their vassals, portraits and coats of arms,
        // gold connectors, independent branch expansion and two-axis navigation.
        private sealed class EmbeddedHierarchy
        {
            private readonly Widget _body;
            private readonly ButtonWidget _template;
            private readonly TextWidget _caption;
            private readonly Dictionary<Hero, TaleWorlds.Core.ViewModelCollection.ImageIdentifiers.CharacterImageIdentifierVM> _portraits =
                new Dictionary<Hero, TaleWorlds.Core.ViewModelCollection.ImageIdentifiers.CharacterImageIdentifierVM>();
            private readonly Dictionary<Hero, TaleWorlds.Core.ViewModelCollection.ImageIdentifiers.BannerImageIdentifierVM> _crests =
                new Dictionary<Hero, TaleWorlds.Core.ViewModelCollection.ImageIdentifiers.BannerImageIdentifierVM>();
            private readonly Dictionary<FeudalTitle, Widget> _nodeWidgets = new Dictionary<FeudalTitle, Widget>();
            private FeudalHierarchyTree _tree;
            private EncyclopediaTroopScrollablePanel _scroll;
            private ListPanel _treeHost;
            private TextWidget _selectionInfo;
            private TextWidget _countInfo;
            private Kingdom _realm;
            private FeudalTitle _title;
            private bool _deFacto;

            internal EmbeddedHierarchy(Widget panel, ButtonWidget template, TextWidget caption)
            {
                _template = template;
                _caption = caption;
                _body = new Widget(panel.Context)
                {
                    Id = "CbDiagramBody",
                    WidthSizePolicy = SizePolicy.StretchToParent,
                    HeightSizePolicy = SizePolicy.StretchToParent,
                    MarginLeft = 12,
                    MarginRight = 12,
                    MarginTop = 75,
                    MarginBottom = 10,
                    IsVisible = true
                };
                panel.AddChild(_body);
            }

            internal void Open(Kingdom kingdom)
            {
                if (kingdom == null || TitleManager.Instance == null) return;
                _body.IsVisible = true;
                _realm = kingdom;
                _deFacto = false;
                RebuildModel(true);
                Draw();
            }

            internal void Hide()
            {
                _body.IsVisible = false;
                _body.RemoveAllChildren();
                _scroll = null;
                _treeHost = null;
                _selectionInfo = null;
                _countInfo = null;
                _nodeWidgets.Clear();
                _portraits.Clear();
                _crests.Clear();
            }

            private void RebuildModel(bool resetSelection)
            {
                var manager = TitleManager.Instance;
                _tree = FeudalHierarchyTree.Build(
                    _realm, _deFacto, manager.GetSovereignTitleList(_realm), manager.AllTitles);
                if (resetSelection || _title == null || !_tree.Titles.Contains(_title))
                    _title = _tree.Roots.FirstOrDefault();
            }

            private void ChangeMode(bool deFacto)
            {
                if (_deFacto == deFacto) return;
                _deFacto = deFacto;
                RebuildModel(true);
                Draw();
            }

            private Hero Owner(FeudalTitle t) =>
                t == null ? null : _deFacto ? t.DeFactoHolder : t.deJure;

            private ButtonWidget Button(Widget parent, string label, System.Action click,
                float width, float height = 39)
            {
                var b = CreateButton(_template, _caption, "", label, click);
                b.WidthSizePolicy = SizePolicy.Fixed;
                b.HeightSizePolicy = SizePolicy.Fixed;
                b.SuggestedWidth = width;
                b.SuggestedHeight = height;
                b.PositionYOffset = 0;
                b.VerticalAlignment = VerticalAlignment.Center;
                parent.AddChild(b);
                return b;
            }

            private ListPanel Row(Widget parent, float top)
            {
                var row = new ListPanel(parent.Context)
                {
                    WidthSizePolicy = SizePolicy.CoverChildren,
                    HeightSizePolicy = SizePolicy.CoverChildren,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    MarginTop = top,
                    StackLayout = { LayoutMethod = TaleWorlds.GauntletUI.Layout.LayoutMethod.HorizontalLeftToRight }
                };
                parent.AddChild(row);
                return row;
            }

            private TextWidget Label(Widget parent, string value, float top,
                float height, float width = 0)
            {
                var label = new TextWidget(parent.Context)
                {
                    WidthSizePolicy = width > 0 ? SizePolicy.Fixed : SizePolicy.StretchToParent,
                    SuggestedWidth = width,
                    HeightSizePolicy = SizePolicy.Fixed,
                    SuggestedHeight = height,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    MarginTop = top,
                    Brush = _caption.ReadOnlyBrush,
                    Text = value,
                    DoNotAcceptEvents = true
                };
                parent.AddChild(label);
                return label;
            }

            private void SelectRealm()
            {
                var options = Kingdom.All.Where(k => k != null && !k.IsEliminated)
                    .OrderBy(k => k.Name.ToString())
                    .Select(k => new InquiryElement(k, k.Name.ToString(), null)).ToList();
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                    "Escolher reino", "Selecione o reino para visualizar a hierarquia feudal.",
                    options, true, 1, 1, "Abrir", "Voltar",
                    choices => {
                        if (choices.Count == 1 && choices[0].Identifier is Kingdom selected)
                            Open(selected);
                    }, null, isSeachAvailable: true));
            }

            private void SelectTitle(FeudalTitle title)
            {
                _title = title;
                UpdateSelection();
            }

            private void UpdateSelection()
            {
                if (_selectionInfo != null)
                    _selectionInfo.Text = _title == null ? "Selecione um título" :
                        $"{_title.FullName}  |  Titular {(_deFacto ? "atual" : "de jure")}: {Owner(_title)?.Name?.ToString() ?? "Vago"}";
            }

            private void FocusSelection()
            {
                if (_title == null || _scroll == null ||
                    !_nodeWidgets.TryGetValue(_title, out Widget widget)) return;
                try
                {
                    _scroll.ScrollToChild(widget,
                        new ScrollablePanel.AutoScrollParameters(0f, 0f, 0f, 0f, 0f, 0f, .2f));
                }
                catch (System.Exception ex)
                {
                    DebugLog.Log("[Hierarchy] Focar título: " + ex.Message);
                }
            }

            private void Draw()
            {
                if (_realm == null || _tree == null) return;
                _body.RemoveAllChildren();
                _scroll = null;
                _treeHost = null;
                _nodeWidgets.Clear();

                var header = new Widget(_body.Context)
                {
                    Id = "CbHierarchyHeader",
                    WidthSizePolicy = SizePolicy.StretchToParent,
                    HeightSizePolicy = SizePolicy.Fixed,
                    SuggestedHeight = 74,
                    VerticalAlignment = VerticalAlignment.Top
                };
                _body.AddChild(header);

                DrawRealmBanner(header);

                var titleBrush = new Brush
                {
                    Font = _caption.ReadOnlyBrush.Font,
                    FontSize = 26,
                    FontColor = Color.FromUint(0xFFE8D8B4),
                    TextHorizontalAlignment = TaleWorlds.TwoDimension.TextHorizontalAlignment.Left,
                    TextVerticalAlignment = TaleWorlds.TwoDimension.TextVerticalAlignment.Center
                };
                var titleText = new TextWidget(header.Context)
                {
                    WidthSizePolicy = SizePolicy.StretchToParent,
                    HeightSizePolicy = SizePolicy.Fixed,
                    SuggestedHeight = 35,
                    MarginLeft = 58,
                    MarginRight = 450,
                    MarginTop = 2,
                    Brush = titleBrush,
                    Text = _realm.Name.ToString(),
                    DoNotAcceptEvents = true
                };
                header.AddChild(titleText);

                var titles = _tree.Titles.ToList();
                string counts =
                    $"{titles.Count(t => t.TitleType == TitleType.Dukedom)} ducados  " +
                    $"{titles.Count(t => t.TitleType == TitleType.County)} condados  " +
                    $"{titles.Count(t => t.TitleType == TitleType.Barony)} baronias  " +
                    $"{titles.Count(t => t.TitleType == TitleType.Lordship)} senhorios";
                var subBrush = new Brush
                {
                    Font = _caption.ReadOnlyBrush.Font,
                    FontSize = 15,
                    FontColor = Color.FromUint(0xFFC9C2B6),
                    TextHorizontalAlignment = TaleWorlds.TwoDimension.TextHorizontalAlignment.Left,
                    TextVerticalAlignment = TaleWorlds.TwoDimension.TextVerticalAlignment.Center
                };
                _countInfo = new TextWidget(header.Context)
                {
                    Id = "CbDiagramCount",
                    WidthSizePolicy = SizePolicy.StretchToParent,
                    HeightSizePolicy = SizePolicy.Fixed,
                    SuggestedHeight = 24,
                    MarginLeft = 58,
                    MarginRight = 450,
                    MarginTop = 35,
                    Brush = subBrush,
                    Text = counts,
                    DoNotAcceptEvents = true
                };
                header.AddChild(_countInfo);

                var modes = new ListPanel(header.Context)
                {
                    WidthSizePolicy = SizePolicy.CoverChildren,
                    HeightSizePolicy = SizePolicy.CoverChildren,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    MarginTop = 10,
                    MarginRight = 8,
                    StackLayout = { LayoutMethod = TaleWorlds.GauntletUI.Layout.LayoutMethod.HorizontalLeftToRight }
                };
                header.AddChild(modes);
                Button(modes, "Trocar reino", SelectRealm, 130, 36);
                Button(modes, "De facto", () => ChangeMode(true), 104, 36).IsEnabled = !_deFacto;
                Button(modes, "De jure", () => ChangeMode(false), 104, 36).IsEnabled = _deFacto;

                var divider = new Widget(_body.Context)
                {
                    WidthSizePolicy = SizePolicy.StretchToParent,
                    HeightSizePolicy = SizePolicy.Fixed,
                    SuggestedHeight = 2,
                    MarginTop = 72,
                    MarginLeft = 6,
                    MarginRight = 6,
                    Color = Color.FromUint(0xFFA8752F),
                    DoNotAcceptEvents = true
                };
                if (_body.Context.SpriteData != null)
                    divider.Sprite = _body.Context.SpriteData.GetSprite("BlankWhiteSquare_9");
                _body.AddChild(divider);

                var viewport = new Widget(_body.Context)
                {
                    Id = "CbDiagramViewport",
                    WidthSizePolicy = SizePolicy.StretchToParent,
                    HeightSizePolicy = SizePolicy.StretchToParent,
                    MarginTop = 82,
                    MarginBottom = 58,
                    MarginLeft = 6,
                    MarginRight = 6
                };
                _body.AddChild(viewport);

                _scroll = new EncyclopediaTroopScrollablePanel(viewport.Context)
                {
                    Id = "CbHierarchyScroll",
                    WidthSizePolicy = SizePolicy.StretchToParent,
                    HeightSizePolicy = SizePolicy.StretchToParent,
                    AutoHideScrollBars = true,
                    AutoAdjustScrollbarHandleSize = true,
                    PanWithMouseEnabled = true,
                    OnlyAcceptScrollEventIfCanScroll = true,
                    MarginRight = 24,
                    MarginBottom = 22
                };
                viewport.AddChild(_scroll);

                var clip = new Widget(viewport.Context)
                {
                    Id = "CbDiagramClip",
                    WidthSizePolicy = SizePolicy.StretchToParent,
                    HeightSizePolicy = SizePolicy.StretchToParent,
                    ClipContents = true
                };
                _scroll.AddChild(clip);

                _treeHost = new ListPanel(viewport.Context)
                {
                    Id = "CbHierarchyTree",
                    WidthSizePolicy = SizePolicy.CoverChildren,
                    HeightSizePolicy = SizePolicy.CoverChildren,
                    MinWidth = 1050,
                    MinHeight = 480,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    MarginTop = 12,
                    MarginBottom = 50,
                    MarginLeft = 45,
                    MarginRight = 45,
                    StackLayout = { LayoutMethod = TaleWorlds.GauntletUI.Layout.LayoutMethod.HorizontalLeftToRight }
                };
                clip.AddChild(_treeHost);
                _scroll.ClipRect = clip;
                _scroll.InnerPanel = _treeHost;

                foreach (var root in _tree.Roots)
                    _treeHost.AddChild(BuildVisualNode(root));

                var vertical = NativeScrollbar(viewport, false);
                vertical.Id = "CbVerticalScroll";
                viewport.AddChild(vertical);
                _scroll.VerticalScrollbar = vertical;

                var horizontal = NativeScrollbar(viewport, true);
                horizontal.Id = "CbHorizontalScroll";
                viewport.AddChild(horizontal);
                _scroll.HorizontalScrollbar = horizontal;

                _selectionInfo = Label(_body, "", 0, 32);
                _selectionInfo.Id = "CbSelectedTitle";
                _selectionInfo.VerticalAlignment = VerticalAlignment.Bottom;
                _selectionInfo.MarginTop = 0;
                _selectionInfo.MarginBottom = 23;
                UpdateSelection();

                var footer = new ListPanel(_body.Context)
                {
                    WidthSizePolicy = SizePolicy.CoverChildren,
                    HeightSizePolicy = SizePolicy.CoverChildren,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    MarginRight = 8,
                    MarginBottom = 1,
                    StackLayout = { LayoutMethod = TaleWorlds.GauntletUI.Layout.LayoutMethod.HorizontalLeftToRight }
                };
                _body.AddChild(footer);
                Button(footer, "Centralizar", FocusSelection, 130, 34);
                Button(footer, "Atos e detalhes", Details, 165, 34);
            }

            private ScrollbarWidget NativeScrollbar(Widget parent, bool horizontal)
            {
                var scrollbar = new ScrollbarWidget(parent.Context)
                {
                    WidthSizePolicy = horizontal ? SizePolicy.StretchToParent : SizePolicy.Fixed,
                    HeightSizePolicy = horizontal ? SizePolicy.Fixed : SizePolicy.StretchToParent,
                    SuggestedWidth = horizontal ? 0 : 26,
                    SuggestedHeight = horizontal ? 9 : 0,
                    HorizontalAlignment = horizontal ? HorizontalAlignment.Center : HorizontalAlignment.Right,
                    VerticalAlignment = horizontal ? VerticalAlignment.Bottom : VerticalAlignment.Center,
                    MarginLeft = horizontal ? 35 : 0,
                    MarginRight = horizontal ? 35 : 1,
                    MarginBottom = horizontal ? 2 : 18,
                    AlignmentAxis = horizontal ? AlignmentAxis.Horizontal : AlignmentAxis.Vertical,
                    MinValue = 0,
                    MaxValue = 100
                };

                var bed = new Widget(parent.Context)
                {
                    WidthSizePolicy = horizontal ? SizePolicy.StretchToParent : SizePolicy.Fixed,
                    HeightSizePolicy = horizontal ? SizePolicy.Fixed : SizePolicy.StretchToParent,
                    SuggestedWidth = horizontal ? 0 : 3,
                    SuggestedHeight = horizontal ? 3 : 0,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    DoNotAcceptEvents = true
                };
                if (parent.Context.SpriteData != null)
                    bed.Sprite = parent.Context.SpriteData.GetSprite(horizontal
                        ? "SPGeneral\\SPRecruitment\\slider_thin_bed_horizontal"
                        : "SPGeneral\\SPRecruitment\\slider_thin_bed_vertical");
                scrollbar.AddChild(bed);

                var handle = new ImageWidget(parent.Context)
                {
                    WidthSizePolicy = SizePolicy.Fixed,
                    HeightSizePolicy = SizePolicy.Fixed,
                    SuggestedWidth = horizontal ? 145 : 25,
                    SuggestedHeight = horizontal ? 25 : 145,
                    MinWidth = horizontal ? 90 : 0,
                    MinHeight = horizontal ? 0 : 90,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Brush = parent.Context.GetBrush(horizontal
                        ? "Encyclopedia.Scroll.Handle.Horizontal"
                        : "Encyclopedia.Scroll.Handle.Vertical")
                };
                scrollbar.AddChild(handle);
                scrollbar.Handle = handle;
                return scrollbar;
            }

            private EncyclopediaUnitTreeNodeItemBrushWidget BuildVisualNode(FeudalTitle title)
            {
                var lineBrush = _body.Context.GetBrush("Encyclopedia.CharacterTree.Line");
                var alternateLineBrush = _body.Context.GetBrush("Encyclopedia.CharacterTree.Line.Alternative");
                bool nativeConnectors = _body.Context.SpriteData != null && lineBrush != null;
                var node = new EncyclopediaUnitTreeNodeItemBrushWidget(_body.Context)
                {
                    Id = "CbTreeNode_" + (title.StringId ?? ""),
                    WidthSizePolicy = SizePolicy.CoverChildren,
                    HeightSizePolicy = SizePolicy.CoverChildren,
                    MarginLeft = 10,
                    MarginRight = 10
                };
                _nodeWidgets[title] = node;

                var cardStack = new ListPanel(node.Context)
                {
                    WidthSizePolicy = SizePolicy.CoverChildren,
                    HeightSizePolicy = SizePolicy.CoverChildren,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    StackLayout = { LayoutMethod = TaleWorlds.GauntletUI.Layout.LayoutMethod.VerticalTopToBottom }
                };
                node.AddChild(cardStack);

                var card = new ButtonWidget(node.Context)
                {
                    Id = "CbSelectTitle_" + (title.StringId ?? ""),
                    WidthSizePolicy = SizePolicy.Fixed,
                    HeightSizePolicy = SizePolicy.Fixed,
                    SuggestedWidth = 120,
                    SuggestedHeight = 110,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Brush = node.Context.GetBrush("Encyclopedia.SubPage.Element"),
                    UpdateChildrenStates = true,
                    DoNotPassEventsToChildren = true
                };
                cardStack.AddChild(card);
                card.ClickEventHandlers.Add(w => SelectTitle(title));

                Hero owner = Owner(title) ?? (title.TitleType <= TitleType.Kingdom ? _realm.Leader : null);
                DrawPortrait(card, owner);
                DrawCrest(card, owner);

                var nameBrush = new Brush
                {
                    Font = _caption.ReadOnlyBrush.Font,
                    FontSize = 16,
                    FontColor = Color.FromUint(0xFFE9E1D3),
                    TextHorizontalAlignment = TaleWorlds.TwoDimension.TextHorizontalAlignment.Center,
                    TextVerticalAlignment = TaleWorlds.TwoDimension.TextVerticalAlignment.Center
                };
                var name = new TextWidget(node.Context)
                {
                    WidthSizePolicy = SizePolicy.Fixed,
                    HeightSizePolicy = SizePolicy.Fixed,
                    SuggestedWidth = 150,
                    SuggestedHeight = 52,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Brush = nameBrush,
                    Text = title.FullName?.ToString() ?? title.StringId ?? "Título",
                    CanBreakWords = true,
                    DoNotAcceptEvents = true
                };
                cardStack.AddChild(name);

                var ownerBrush = new Brush
                {
                    Font = _caption.ReadOnlyBrush.Font,
                    FontSize = 12,
                    FontColor = Color.FromUint(0xFFBFB7AA),
                    TextHorizontalAlignment = TaleWorlds.TwoDimension.TextHorizontalAlignment.Center,
                    TextVerticalAlignment = TaleWorlds.TwoDimension.TextVerticalAlignment.Center
                };
                var holder = new TextWidget(node.Context)
                {
                    WidthSizePolicy = SizePolicy.Fixed,
                    HeightSizePolicy = SizePolicy.Fixed,
                    SuggestedWidth = 150,
                    SuggestedHeight = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Brush = ownerBrush,
                    Text = Owner(title)?.Name?.ToString() ?? "Vago",
                    CanBreakWords = false,
                    DoNotAcceptEvents = true
                };
                cardStack.AddChild(holder);

                var lineContainer = new Widget(node.Context)
                {
                    Id = "LineContainer",
                    WidthSizePolicy = SizePolicy.Fixed,
                    HeightSizePolicy = SizePolicy.Fixed,
                    SuggestedWidth = 0,
                    SuggestedHeight = 20,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MarginTop = 195,
                    DoNotAcceptEvents = true,
                    AlphaFactor = .72f
                };
                if (node.Context.SpriteData != null)
                    lineContainer.Sprite = node.Context.SpriteData.GetSprite("BlankWhiteSquare_9");
                node.AddChild(lineContainer);

                var children = new ListPanel(node.Context)
                {
                    Id = "ChildContainer",
                    WidthSizePolicy = SizePolicy.CoverChildren,
                    HeightSizePolicy = SizePolicy.CoverChildren,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MarginTop = 226,
                    StackLayout = { LayoutMethod = TaleWorlds.GauntletUI.Layout.LayoutMethod.HorizontalLeftToRight }
                };
                node.AddChild(children);

                if (nativeConnectors)
                {
                    node.LineBrush = lineBrush;
                    node.AlternateLineBrush = alternateLineBrush ?? lineBrush;
                    node.LineContainer = lineContainer;
                    node.ChildContainer = children;
                }

                foreach (var child in _tree.ChildrenOf(title))
                    children.AddChild(BuildVisualNode(child));

                return node;
            }

            private void DrawRealmBanner(Widget parent)
            {
                if (_realm?.Banner == null) return;
                try
                {
                    var id = new TaleWorlds.Core.ViewModelCollection.ImageIdentifiers.BannerImageIdentifierVM(
                        _realm.Banner, true);
                    var banner = new MaskedTextureWidget(parent.Context)
                    {
                        WidthSizePolicy = SizePolicy.Fixed,
                        HeightSizePolicy = SizePolicy.Fixed,
                        SuggestedWidth = 44,
                        SuggestedHeight = 44,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        MarginLeft = 7,
                        MarginTop = 5,
                        DoNotAcceptEvents = true,
                        Brush = parent.Context.GetBrush("Flat.Tuple.Banner.Small.Hero"),
                        ImageId = id.Id,
                        AdditionalArgs = id.AdditionalArgs,
                        TextureProviderName = id.TextureProviderName
                    };
                    parent.AddChild(banner);
                }
                catch (System.Exception ex)
                {
                    DebugLog.Log("[Hierarchy] Realm banner: " + ex.Message);
                }
            }

            private void DrawPortrait(Widget card, Hero hero)
            {
                if (hero?.CharacterObject == null) return;
                try
                {
                    if (!_portraits.TryGetValue(hero, out var id))
                        _portraits[hero] = id =
                            new TaleWorlds.Core.ViewModelCollection.ImageIdentifiers.CharacterImageIdentifierVM(
                                CharacterCode.CreateFrom(hero.CharacterObject));
                    var portrait = new ImageIdentifierWidget(card.Context)
                    {
                        WidthSizePolicy = SizePolicy.Fixed,
                        HeightSizePolicy = SizePolicy.Fixed,
                        SuggestedWidth = 78,
                        SuggestedHeight = 86,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        PositionXOffset = 7,
                        PositionYOffset = 7,
                        DoNotAcceptEvents = true,
                        ImageId = id.Id,
                        AdditionalArgs = id.AdditionalArgs,
                        TextureProviderName = id.TextureProviderName
                    };
                    card.AddChild(portrait);
                }
                catch (System.Exception ex)
                {
                    DebugLog.Log("[Hierarchy] Portrait: " + ex.Message);
                }
            }

            private void DrawCrest(Widget card, Hero owner)
            {
                if (owner?.Clan?.Banner == null) return;
                try
                {
                    if (!_crests.TryGetValue(owner, out var id))
                        _crests[owner] = id =
                            new TaleWorlds.Core.ViewModelCollection.ImageIdentifiers.BannerImageIdentifierVM(
                                owner.Clan.Banner, true);
                    var crest = new MaskedTextureWidget(card.Context)
                    {
                        WidthSizePolicy = SizePolicy.Fixed,
                        HeightSizePolicy = SizePolicy.Fixed,
                        SuggestedWidth = 35,
                        SuggestedHeight = 45,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        PositionXOffset = 74,
                        PositionYOffset = 7,
                        DoNotAcceptEvents = true,
                        Brush = card.Context.GetBrush("Flat.Tuple.Banner.Small.Hero"),
                        ImageId = id.Id,
                        AdditionalArgs = id.AdditionalArgs,
                        TextureProviderName = id.TextureProviderName
                    };
                    card.AddChild(crest);
                }
                catch (System.Exception ex)
                {
                    DebugLog.Log("[Hierarchy] Crest: " + ex.Message);
                }
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