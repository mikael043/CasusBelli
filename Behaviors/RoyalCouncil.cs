using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace CasusBelli
{
    public sealed class RoyalCouncil : CampaignBehaviorBase
    {
        private List<string> _discoveries = new List<string>();
        private Dictionary<string,string> _members = new Dictionary<string,string>();
        private static string Key(Kingdom realm, CouncilOffice office) => realm.StringId + "|" + (int)office;
        internal string Member(Kingdom realm, CouncilOffice office) =>
            _members.TryGetValue(Key(realm, office), out var id) ? id : null;

        public override void SyncData(IDataStore store)
        {
            store.SyncData("CasusBelli_CouncilDiscoveries_v1", ref _discoveries);
            _discoveries ??= new List<string>();
            var rows = store.IsSaving ? _members.Select(p => p.Key + "|" + p.Value).ToList() : new List<string>();
            store.SyncData("CasusBelli_RoyalCouncil_v1", ref rows);
            if (!store.IsLoading) return;
            _members.Clear();
            foreach (var row in rows ?? new List<string>())
            {
                int split = row.LastIndexOf('|');
                if (split > 0) _members[row.Substring(0,split)] = row.Substring(split + 1);
            }
        }
        public override void RegisterEvents() => CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, Review);
        internal IEnumerable<Hero> Candidates(Kingdom realm, CouncilOffice office, List<Hero> court) =>
            court.Where(h => CouncilSelection.Eligible(realm,h,office) &&
                !CouncilSelection.Offices.Any(o => o != office && Member(realm,o) == h.StringId))
                .OrderByDescending(h => CouncilSelection.Score(realm,h,office)).ThenBy(h => h.StringId,StringComparer.Ordinal);

        internal void Appoint(Kingdom realm, CouncilOffice office, Hero hero)
        {
            if (realm.Leader != Hero.MainHero) return;
            if (hero == null) _members.Remove(Key(realm,office));
            else if (Candidates(realm,office,CouncilSelection.Court(realm)).Contains(hero))
                _members[Key(realm,office)] = hero.StringId;
        }
        private void Review()
        {
            bool weekly = (int)CampaignTime.Now.ToDays % 7 == 0;
            var live = new HashSet<string>();
            foreach (var realm in Kingdom.All.Where(k => !k.IsEliminated && k.Leader != null))
            {
                var court = CouncilSelection.Court(realm);
                foreach (var office in CouncilSelection.Offices)
                    if (!court.Any(h => h.StringId == Member(realm,office) && CouncilSelection.Eligible(realm,h,office)))
                        _members.Remove(Key(realm,office));
                if (weekly)
                {
                    CouncilDuties.Accounts(realm);
                    RealmDiplomacy.Weekly(realm);
                    Managers.Titles.TitlePetitions.Weekly(realm);
                    CouncilDuties.Intelligence(realm,_discoveries,live);
                    CourtInvestigation.Weekly(realm);
                }
                if (realm.Leader == Hero.MainHero) continue;
                foreach (var office in CouncilSelection.Offices)
                {
                    var incumbent = court.FirstOrDefault(h => h.StringId == Member(realm,office));
                    if (incumbent != null && (int)CampaignTime.Now.ToDays % 7 != 0) continue;
                    var best = Candidates(realm,office,court).FirstOrDefault();
                    if (best != null && (incumbent == null ||
                        CouncilSelection.Score(realm,best,office) >= CouncilSelection.Score(realm,incumbent,office) + 10))
                        _members[Key(realm,office)] = best.StringId;
                }
            }
            if (weekly) _discoveries.RemoveAll(k => !live.Contains(k));
        }
    }
}
