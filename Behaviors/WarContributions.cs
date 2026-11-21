using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CasusBelli
{
    public partial class CasusBelliWarScoreBehavior
    {
        private HashSet<string> _contributions = new HashSet<string>();
        private static string Entry(WarRecord war, string realm, string kind, string id) =>
            string.Join("|", war.WarId, realm, kind, id);

        private readonly Dictionary<Settlement, Settlement[]> _neighbors = new Dictionary<Settlement, Settlement[]>();
        internal Settlement[] Neighbors(Settlement fort)
        {
            if (!_neighbors.TryGetValue(fort, out var neighbors))
            {
                neighbors = Settlement.All.Where(s => s != fort && s.IsFortification)
                    .OrderBy(s => fort.Position.DistanceSquared(s.Position))
                    .ThenBy(s => s.StringId, System.StringComparer.Ordinal).Take(3).ToArray();
                _neighbors.Add(fort, neighbors);
            }
            return neighbors;
        }

        internal bool Fought(WarRecord war, Kingdom realm, Hero hero) =>
            _contributions.Contains(Entry(war, realm.StringId, "lord", hero.StringId));
        internal bool WasTaken(WarRecord war, Kingdom realm, Settlement fort) =>
            _contributions.Contains(Entry(war, realm.StringId, "fort", fort.StringId));

        private void SyncWarContributions(IDataStore store)
        {
            var entries = store.IsSaving ? _contributions.ToList() : new List<string>();
            store.SyncData("CasusBelli_WarContributions_v1", ref entries);
            if (store.IsLoading) _contributions = new HashSet<string>(entries ?? new List<string>());
        }

        private void TrackCommanders(MapEvent battle)
        {
            if (battle == null) return;
            var attacker = battle.GetMapEventSide(BattleSideEnum.Attacker)?.LeaderParty?.MapFaction as Kingdom;
            var defender = battle.GetMapEventSide(BattleSideEnum.Defender)?.LeaderParty?.MapFaction as Kingdom;
            var main = GetMain();
            if (attacker == null || defender == null || main == null ||
                !main.TryGetActiveWarBetween(attacker.StringId, defender.StringId, out var war)) return;
            foreach (var party in battle.InvolvedParties)
            {
                var leader = party.MobileParty?.LeaderHero;
                var realm = leader?.Clan?.Kingdom;
                if (realm == attacker || realm == defender)
                    _contributions.Add(Entry(war, realm.StringId, "lord", leader.StringId));
            }
        }
    }
}
