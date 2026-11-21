using HarmonyLib;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace CasusBelli.Harmony
{
    [HarmonyPatch(typeof(Hero), nameof(Hero.Name), MethodType.Getter)]
    internal static class TitleDisplayNamePatch
    {
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void Postfix(Hero __instance, TextObject ____name, ref TextObject __result)
        {
            // Preserve a display name already supplied by another naming mod.
            if (Campaign.Current == null || __result == null || !ReferenceEquals(__result, ____name))
                return;

            var manager = TitleManager.Instance;
            if (!manager.IsInitialized) return;
            var title = manager.GetHighestTitle(__instance);
            var clan = __instance.Clan;
            var realm = clan?.Kingdom;
            var ruler = realm?.RulingClan?.Leader;
            var culture = realm?.Culture ?? clan?.Culture;
            string rank;
            if (__instance == ruler)
                rank = TextHelper.GetTitlePrefix(title?.TitleType == TitleType.Empire ? TitleType.Empire : TitleType.Kingdom,
                    culture, __instance.IsFemale);
            else if (ruler != null && culture?.StringId == "vlandia" &&
                (__instance.Father == ruler || __instance.Mother == ruler))
                rank = __instance.IsFemale ? "Princesa" : "Príncipe";
            else if (title != null)
                rank = TextHelper.GetTitlePrefix(title.TitleType, culture, __instance.IsFemale);
            else if (realm != null && !clan.IsUnderMercenaryService && __instance.IsLord)
                rank = __instance.IsFemale ? "Lady" : "Lorde";
            else return;

            __result = new TextObject(rank == "Khan" ? "{=CBHeroTitleSuffix}{NAME} {RANK}" : "{=CBHeroTitleName}{RANK} {NAME}")
                .SetTextVariable("RANK", rank)
                .SetTextVariable("NAME", __result);
        }
    }
}
