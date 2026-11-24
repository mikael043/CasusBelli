using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

namespace CasusBelli.UI.Titles
{
    // Real modal layer for title details.
    // Keeping this outside KingdomManagement's widget tree is intentional:
    // Gauntlet event routing then cannot fall through to the hierarchy behind it.
    internal sealed class KingdomHierarchyDetailsWindow : GlobalLayer
    {
        internal const int LayerPriority = 2000;

        private static KingdomHierarchyDetailsWindow _active;

        private GauntletLayer _layer;
        private GauntletMovieIdentifier _movie;
        private KingdomHierarchyDetailsVM _vm;

        internal static bool Open(KingdomHierarchyDetailsVM vm)
        {
            if (vm == null) return false;

            // Integration tests do not create a real ScreenManager screen.
            // Keep the VM testable without pretending a modal layer exists.
            if (ScreenManager.TopScreen == null)
                return true;

            if (_active != null)
            {
                if (ReferenceEquals(_active._vm, vm))
                    return true;
                _active.CloseInternal();
            }

            var window = new KingdomHierarchyDetailsWindow();
            try
            {
                window._vm = vm;
                window._layer = new GauntletLayer(
                    "CBKingdomHierarchyDetailsLayer",
                    LayerPriority,
                    false);
                window.Layer = window._layer;

                window._movie = window._layer.LoadMovie(
                    "CBKingdomHierarchyDetails",
                    vm);

                // The layer must exist on ScreenManager before requesting focus.
                ScreenManager.AddGlobalLayer(window, true);
                _active = window;

                window._layer.InputRestrictions.SetInputRestrictions(
                    true,
                    InputUsageMask.All);
                window._layer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(window._layer);

                return true;
            }
            catch (Exception ex)
            {
                window.CloseInternal();
                DebugLog.Log(
                    "[Hierarchy] Falha ao abrir camada modal de detalhes: " +
                    ex.GetType().Name + ": " + ex.Message);
                InformationManager.DisplayMessage(new InformationMessage(
                    "CB: falha ao abrir detalhes do título: " + ex.Message));
                return false;
            }
        }

        internal static void Close(KingdomHierarchyDetailsVM vm)
        {
            if (_active == null) return;
            if (vm != null && !ReferenceEquals(_active._vm, vm)) return;
            _active.CloseInternal();
        }

        private void CloseInternal()
        {
            var vm = _vm;

            try
            {
                if (_layer != null)
                {
                    _layer.InputRestrictions.ResetInputRestrictions();
                    _layer.IsFocusLayer = false;
                    ScreenManager.TryLoseFocus(_layer);

                    if (_movie != null)
                        _layer.ReleaseMovie(_movie);
                }

                if (_active == this)
                    ScreenManager.RemoveGlobalLayer(this);
            }
            catch (Exception ex)
            {
                DebugLog.Log(
                    "[Hierarchy] Falha ao fechar camada modal: " +
                    ex.GetType().Name + ": " + ex.Message);
            }

            _movie = null;
            _layer = null;
            _vm = null;

            if (_active == this)
                _active = null;

            vm?.OnLayerClosed();
        }
    }
}