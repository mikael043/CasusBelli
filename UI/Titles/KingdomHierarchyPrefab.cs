#if CB_UIEX
using System.Collections.Generic;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.ViewModels;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement;
using TaleWorlds.Library;

namespace CasusBelli.UI.Titles
{
    // Add the CasusBelli tab next to Fiefs. The visual body itself is a custom
    // prefab, so changing fonts/spacing/colors no longer requires recompiling C#.
    [PrefabExtension(
        "KingdomManagement",
        "descendant::ButtonWidget[@Id='FiefsTabButton']")]
    internal sealed class KingdomHierarchyTabPrefabPatch : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        [PrefabExtensionText]
        public string Content => @"
<ButtonWidget Id=""CbKingdomTab"" DataSource=""{CbHierarchy}""
              DoNotPassEventsToChildren=""true""
              WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
              SuggestedWidth=""!Header.Tab.Center.Width.Scaled""
              SuggestedHeight=""!Header.Tab.Center.Height.Scaled""
              VerticalAlignment=""Center"" PositionYOffset=""2""
              Brush=""Header.Tab.Center""
              Command.Click=""ExecuteOpen"" UpdateChildrenStates=""true"">
  <Children>
    <TextWidget WidthSizePolicy=""CoverChildren"" HeightSizePolicy=""CoverChildren""
                HorizontalAlignment=""Center"" VerticalAlignment=""Center""
                Brush=""Clan.TabControl.Text"" Text=""CasusBelli"" />
  </Children>
</ButtonWidget>";
    }

    // Render after the vanilla panels so it naturally overlays the currently
    // selected vanilla page while IsOpen=true.
    [PrefabExtension(
        "KingdomManagement",
        "descendant::DiplomacyPanel[@Id='DiplomacyPanel']")]
    internal sealed class KingdomHierarchyPanelPrefabPatch : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        [PrefabExtensionText]
        public string Content =>
            @"<CBKingdomHierarchy DataSource=""{CbHierarchy}"" />";
    }

    [ViewModelMixin]
    internal sealed class KingdomHierarchyMixin :
        BaseViewModelMixin<KingdomManagementVM>
    {
        private static readonly Dictionary<KingdomManagementVM, KingdomHierarchyMixin>
            Instances = new Dictionary<KingdomManagementVM, KingdomHierarchyMixin>();

        private readonly KingdomManagementVM _vm;
        private readonly KingdomHierarchyVM _hierarchy;

        public KingdomHierarchyMixin(KingdomManagementVM vm) : base(vm)
        {
            _vm = vm;
            _hierarchy = new KingdomHierarchyVM(() => _vm.Kingdom);
            Instances[vm] = this;
            _hierarchy.SetDefaultKingdom(vm.Kingdom);
        }

        [DataSourceProperty]
        public KingdomHierarchyVM CbHierarchy => _hierarchy;

        public override void OnRefresh()
        {
            base.OnRefresh();
            _hierarchy.SetDefaultKingdom(_vm.Kingdom);
        }

        public override void OnFinalize()
        {
            Instances.Remove(_vm);
            _hierarchy.Close();
            base.OnFinalize();
        }

        internal static void CloseFor(KingdomManagementVM vm)
        {
            if (vm != null && Instances.TryGetValue(vm, out var mixin))
                mixin._hierarchy.Close();
        }
    }

    // Clicking any vanilla tab closes the overlay. The native tab keeps its own
    // command untouched; this patch only changes our ViewModel state afterwards.
    [HarmonyPatch]
    internal static class KingdomHierarchyNativeTabClosePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            string[] names =
            {
                "ExecuteShowClan",
                "ExecuteShowFiefs",
                "ExecuteShowPolicies",
                "ExecuteShowArmy",
                "ExecuteShowDiplomacy"
            };

            foreach (string name in names)
            {
                MethodInfo method = AccessTools.Method(typeof(KingdomManagementVM), name);
                if (method != null) yield return method;
            }
        }

        private static void Postfix(KingdomManagementVM __instance)
        {
            KingdomHierarchyMixin.CloseFor(__instance);
        }
    }
}
#endif