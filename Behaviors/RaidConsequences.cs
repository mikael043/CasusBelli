using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli
{
    public partial class CasusBelliWarScoreBehavior
    {
        private readonly Dictionary<string, double> _raidBans = new Dictionary<string, double>();
        private static Settlement RecruitRegion(Settlement settlement) => settlement?.Village?.Bound ?? settlement;
        internal double BanDays(Hero leader, Settlement settlement)
        {
            var region = RecruitRegion(settlement);
            if (leader == null || region == null) return 0;
            return _raidBans.TryGetValue(leader.StringId + "|" + region.StringId, out var until)
                ? Math.Max(0, until - CampaignTime.Now.ToDays) : 0;
        }

        private void OnRaidCompleted(BattleSideEnum winner, RaidEventComponent raid)
        {
            var battle = raid?.MapEvent;
            var village = battle?.MapEventSettlement;
            if (winner != BattleSideEnum.Attacker || village?.IsVillage != true) return;
            var region = RecruitRegion(village);
            var until = CampaignTime.YearsFromNow(1).ToDays;
            foreach (var party in battle.AttackerSide.Parties)
            {
                var leader = party.Party.MobileParty?.LeaderHero;
                if (leader == null) continue;
                _raidBans[leader.StringId + "|" + region.StringId] = until;
                if (leader == Hero.MainHero)
                    InformationManager.DisplayMessage(new InformationMessage($"Após o saque de {village.Name}, você não poderá recrutar em {region.Name} nem nas aldeias vinculadas por um ano."));
            }
            var attacker = battle.AttackerSide.LeaderParty?.MapFaction as Kingdom;
            var defender = village.OwnerClan?.Kingdom;
            var main = GetMain();
            if (attacker == null || defender == null || main == null ||
                !main.TryGetActiveWarBetween(attacker.StringId, defender.StringId, out var war) || war.ArmisticeEnds >= 0) return;
            if (RaidCount(war, attacker.StringId) < 5 &&
                _contributions.Add(Entry(war, attacker.StringId, "raid", village.StringId))) RaiseChanged();
        }

        private int RaidCount(WarRecord war, string side)
        {
            var prefix = Entry(war, side, "raid", "");
            return _contributions.Count(key => key.StartsWith(prefix, StringComparison.Ordinal));
        }

        private void SyncRaidBans(IDataStore store)
        {
            var rows = new List<string>();
            if (store.IsSaving)
                foreach (var pair in _raidBans.Where(p => p.Value > CampaignTime.Now.ToDays))
                    rows.Add(pair.Key + "|" + pair.Value.ToString("R", CultureInfo.InvariantCulture));
            store.SyncData("CasusBelli_RecruitmentBans_v1", ref rows);
            if (!store.IsLoading) return;
            _raidBans.Clear();
            foreach (var row in rows ?? new List<string>())
            {
                int cut = row.LastIndexOf('|');
                if (cut > 0 && double.TryParse(row.Substring(cut + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var days))
                    _raidBans[row.Substring(0, cut)] = days;
            }
        }
    }

    [HarmonyPatch(typeof(DefaultVolunteerModel), nameof(DefaultVolunteerModel.MaximumIndexHeroCanRecruitFromHero))]
    internal static class RaidVolunteerPatch
    {
        private static void Postfix(Hero __0, Hero __1, ref int __result)
        {
            if (Campaign.Current?.GetCampaignBehavior<CasusBelliWarScoreBehavior>()?.BanDays(__0, __1?.CurrentSettlement ?? __1?.HomeSettlement) > 0)
                __result = 0;
        }
    }

    [HarmonyPatch(typeof(RecruitmentCampaignBehavior), "ApplyInternal")]
    internal static class RaidRecruitmentPatch
    {
        internal static bool Prefix(MobileParty __0, Settlement __1)
        {
            var days = Campaign.Current?.GetCampaignBehavior<CasusBelliWarScoreBehavior>()?.BanDays(__0?.LeaderHero, __1 ?? __0?.CurrentSettlement) ?? 0;
            if (days <= 0) return true;
            if (__0 == MobileParty.MainParty)
                InformationManager.DisplayMessage(new InformationMessage($"O saque impede o recrutamento nesta região por mais {Math.Ceiling(days)} dias."));
            return false;
        }
    }

    [HarmonyPatch]
    internal static class RaidTavernPatch
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(RecruitmentCampaignBehavior), "BuyMercenaries");
            yield return AccessTools.Method(typeof(RecruitmentCampaignBehavior), "buy_mercenaries_on_consequence");
        }

        private static bool Prefix() => RaidRecruitmentPatch.Prefix(MobileParty.MainParty, Settlement.CurrentSettlement);
    }

}
