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