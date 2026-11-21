using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using CasusBelli.Managers.Titles;

namespace CasusBelli.UI
{
#if CB_HARMONY
    // PATCH DESABILITADO - causava crash "Patching exception in method null"
    // Use Harmony\TitleDisplayNamePatch.cs em vez deste
    /*
    [HarmonyPatch(typeof(Hero), "Name")]
    public static class HeroNameTitlePatch
    {
        [HarmonyPostfix]
        public static void PatchHeroName(ref string __result, Hero __instance)
        {
            if (__instance == null || __instance.Clan == null)
                return;

            var titleManager = TitleManager.Instance;
            if (titleManager == null)
                return;

            FeudalTitle title = titleManager.GetTitleByStringId(__instance.StringId);
            if (title == null)
                return;

            // Verifica se o herói é o dono do título (de fato)
            if (!title.IsHolder(__instance.Clan))
                return;

            string titlePrefix = TitleDisplayName.GetCharacterTitlePrefix(title);
            if (!string.IsNullOrEmpty(titlePrefix) && !__result.Contains(titlePrefix))
            {
                __result = titlePrefix + " " + __result;
            }
        }
    }

    [HarmonyPatch(typeof(Settlement), "Name")]
    public static class SettlementNameTitlePatch
    {
        [HarmonyPostfix]
        public static void PatchSettlementName(ref string __result, Settlement __instance)
        {
            if (__instance == null)
                return;

            var titleManager = TitleManager.Instance;
            FeudalTitle title = titleManager?.GetTitle(__instance);
            if (title?.IsUsurped == true)
            {
                __result = __result + " [Usurpado]";
            }
        }
    }
    */
#endif
}