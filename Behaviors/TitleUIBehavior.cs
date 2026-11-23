using System;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;
using TaleWorlds.Engine;
using CasusBelli.UI.Titles;

namespace CasusBelli
{
    // Comportamento para a interface de títulos (BannerKings equivalent)
    public class TitleUIBehavior : CampaignBehaviorBase
    {
        private class TitleGauntletLayer : GlobalLayer
        {
            private GauntletLayer _gauntletLayer;
            private GauntletMovieIdentifier _gauntletMovie;
            private TitleTreeVM _viewModel;

            public TitleGauntletLayer()
            {
                _gauntletLayer = new GauntletLayer("TitlesLayer", 100, false);
                Layer = (ScreenLayer)_gauntletLayer;
            }

            public void LoadMovie(string uiFile, TitleTreeVM viewModel)
            {
                _viewModel = viewModel;
                _gauntletMovie = _gauntletLayer?.LoadMovie(uiFile, viewModel);
            }

            public void ReleaseMovie()
            {
                if (_gauntletMovie != null)
                    _gauntletLayer?.ReleaseMovie(_gauntletMovie);
            }
        }

        private TitleGauntletLayer _titlesLayer;
        private GauntletMovieIdentifier _titlesMovie;
        public TitleTreeVM _viewModel;
        private bool _isWindowOpen = false;

        private static readonly string UiFile = "TitlesWindow";

        public override void RegisterEvents()
        {
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        public static void OpenTitlesWindow()
        {
            var behavior = Campaign.Current?.GetCampaignBehavior<TitleUIBehavior>();
            if (behavior != null)
            {
                behavior.Open();
            }
        }

        public void Open()
        {
            if (_isWindowOpen) return;

            try
            {
                // Create the Gauntlet layer
                _titlesLayer = new TitleGauntletLayer();

                // Create the view model
                _viewModel = new TitleTreeVM();

                // Load the movie from the prefab file
                // UI file path is relative to the module's GUI folder
                _titlesLayer.LoadMovie(UiFile, _viewModel);

                // Adicionar a camada ao ScreenManager usando o método correto para GlobalLayer
                ScreenManager.AddGlobalLayer(_titlesLayer, true);

                _isWindowOpen = true;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[TitleUI] Erro ao abrir janela de títulos: " + ex.Message));
            }
        }

        public void Close()
        {
            if (!_isWindowOpen) return;

            try
            {
                if (_titlesLayer != null)
                {
                    _titlesLayer.ReleaseMovie();
                    ScreenManager.RemoveGlobalLayer(_titlesLayer);
                }

                _isWindowOpen = false;
                _titlesLayer = null;
                _titlesMovie = null;
                _viewModel = null;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[TitleUI] Erro ao fechar janela de títulos: " + ex.Message));
            }
        }
    }
}

namespace CasusBelli.UI.Titles
{
    public sealed class CBFeudalTreeVM : ViewModel
    {
        private const int PageSize = 5;
        private readonly Action _close;
        private Kingdom _realm;
        private FeudalTitle _current;
        private int _page;
        private readonly System.Collections.Generic.Stack<FeudalTitle> _path = new System.Collections.Generic.Stack<FeudalTitle>();
        [DataSourceProperty] public MBBindingList<CBFeudalRealmVM> Realms { get; } = new MBBindingList<CBFeudalRealmVM>();
        [DataSourceProperty] public MBBindingList<CBFeudalNodeVM> Children { get; } = new MBBindingList<CBFeudalNodeVM>();
        [DataSourceProperty] public string Heading => "Hierarquia Feudal — " + (_realm?.Name?.ToString() ?? "");
        [DataSourceProperty] public string RootName => _current?.FullName?.ToString() ?? _realm?.Name?.ToString() ?? "Sem título";
        [DataSourceProperty] public string RootHolder => _current?.deJure?.Name?.ToString() ?? (_current == null ? "Selecione um título" : "Vago");
        [DataSourceProperty] public bool HasRoot => _current != null;
        [DataSourceProperty] public bool CanBack => _path.Count > 0;
        [DataSourceProperty] public bool HasChildren => Children.Count > 0;
        [DataSourceProperty] public float LineWidth => System.Math.Max(0, (Children.Count - 1) * 272);
        [DataSourceProperty] public bool CanNext => (_page + 1) * PageSize < _count;
        [DataSourceProperty] public bool CanPrevious => _page > 0;
        [DataSourceProperty] public string PageText => _count == 0 ? "Sem títulos subordinados" : $"{_page * PageSize + 1}–{System.Math.Min((_page + 1) * PageSize, _count)} de {_count}";
        [DataSourceProperty] public string BranchText => _current == null ? "Títulos soberanos" : "Vassalos diretos";
        private int _count;

        public CBFeudalTreeVM(Kingdom realm, Action close)
        {
            _close = close;
            foreach (var k in Kingdom.All)
                if (!k.IsEliminated) Realms.Add(new CBFeudalRealmVM(k, SelectRealm));
            SelectRealm(realm ?? (Realms.Count > 0 ? Realms[0].Realm : null));
        }

        private void SelectRealm(Kingdom realm)
        {
            _realm = realm;
            _path.Clear();
            _current = null;
            if (realm != null)
            {
                var roots = System.Linq.Enumerable.ToList(TitleManager.Instance.GetSovereignTitleList(realm));
                if (roots.Count == 1) _current = roots[0];
            }
            _page = 0;
            RefreshTree();
        }

        private void RefreshTree()
        {
            Children.Clear();
            var source = _current == null
                ? (_realm == null ? new System.Collections.Generic.List<FeudalTitle>() : System.Linq.Enumerable.ToList(TitleManager.Instance.GetSovereignTitleList(_realm)))
                : (_current.Vassals == null ? new System.Collections.Generic.List<FeudalTitle>() : System.Linq.Enumerable.ToList(_current.Vassals));
            var valid = source.FindAll(t => t != null && t != _current && !_path.Contains(t));
            _count = valid.Count;
            foreach (var child in System.Linq.Enumerable.Take(System.Linq.Enumerable.Skip(valid, _page * PageSize), PageSize))
                Children.Add(new CBFeudalNodeVM(child, Navigate));
            foreach (string prop in new[] { nameof(Heading), nameof(RootName), nameof(RootHolder), nameof(HasRoot), nameof(CanBack), nameof(HasChildren), nameof(LineWidth), nameof(CanNext), nameof(CanPrevious), nameof(PageText), nameof(BranchText) })
                OnPropertyChanged(prop);
        }

        private void Navigate(FeudalTitle title)
        {
            _path.Push(_current);
            _current = title;
            _page = 0;
            RefreshTree();
        }

        public void ExecuteBack()
        {
            if (_path.Count == 0) return;
            _current = _path.Pop(); _page = 0; RefreshTree();
        }
        public void ExecuteNext() { if (CanNext) { _page++; RefreshTree(); } }
        public void ExecutePrevious() { if (CanPrevious) { _page--; RefreshTree(); } }
        public void ExecuteDetails()
        {
            var title = _current;
            if (title == null) return;
            var realm = _realm;
            _close();
            Action back = () => CBFeudalTreeWindow.Open(realm);
            InformationManager.ShowInquiry(new InquiryData(title.FullName.ToString(),
                $"Titular de jure: {title.deJure?.Name?.ToString() ?? "Vago"}\nDetentor de facto: {title.DeFactoHolder?.Name}\nHerança: {RealmLaw.Translate(title.Contract?.Inheritance?.StringId)}\nSucessão: {RealmLaw.Translate(title.Contract?.Succession?.StringId)}",
                true, true, "Atos feudais", "Voltar", () => FeudalActionsMenu.Open(title, back), back));
        }
        public void ExecuteClose() => _close();
    }
    public sealed class CBFeudalRealmVM : ViewModel
    {
        internal Kingdom Realm { get; }
        private readonly Action<Kingdom> _select;
        [DataSourceProperty] public string Name => Realm.Name.ToString();
        public CBFeudalRealmVM(Kingdom realm, Action<Kingdom> select) { Realm = realm; _select = select; }
        public void ExecuteSelect() => _select(Realm);
    }
    public sealed class CBFeudalNodeVM : ViewModel
    {
        private readonly FeudalTitle _title;
        private readonly Action<FeudalTitle> _select;
        [DataSourceProperty] public string Name => _title.FullName.ToString();
        [DataSourceProperty] public string Holder => _title.deJure?.Name?.ToString() ?? "Vago";
        public CBFeudalNodeVM(FeudalTitle title, Action<FeudalTitle> select) { _title = title; _select = select; }
        public void ExecuteSelect() => _select(_title);
    }
    internal sealed class CBFeudalTreeWindow : GlobalLayer
    {
        private static CBFeudalTreeWindow _active;
        private GauntletLayer _layer;
        private GauntletMovieIdentifier _movie;
        internal static void Open(Kingdom realm)
        {
            if (_active != null || Campaign.Current == null) return;
            var window = new CBFeudalTreeWindow();
            try
            {
                window._layer = new GauntletLayer("CBFeudalTree", 1000, false);
                window.Layer = window._layer;
                window._movie = window._layer.LoadMovie("CBFeudalTreeWindow", new CBFeudalTreeVM(realm, window.Close));
                ScreenManager.AddGlobalLayer(window, true);
                _active = window;
                window._layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
                window._layer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(window._layer);
                Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
            }
            catch (Exception ex)
            {
                window.Close();
                InformationManager.DisplayMessage(new InformationMessage("CB: " + ex.Message));
            }
        }
        private void Close()
        {
            if (_layer == null) return;
            _layer.InputRestrictions.ResetInputRestrictions();
            _layer.IsFocusLayer = false;
            ScreenManager.TryLoseFocus(_layer);
            if (_movie != null) _layer.ReleaseMovie(_movie);
            if (_active == this) { ScreenManager.RemoveGlobalLayer(this); _active = null; }
            _layer = null;
        }
        [CommandLineFunctionality.CommandLineArgumentFunction("tree_preview", "casus_belli")]
        public static string Preview(System.Collections.Generic.List<string> args)
        {
            if (Campaign.Current == null) return "Abra uma campanha.";
            Open(Hero.MainHero?.Clan?.Kingdom);
            return "CB: árvore experimental solicitada.";
        }
    }
}