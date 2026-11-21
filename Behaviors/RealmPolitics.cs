using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace CasusBelli
{
    public sealed class RealmPolitics : CampaignBehaviorBase
    {
        private readonly Dictionary<string,string> _records = new Dictionary<string,string>();
        internal static RealmPolitics Current => Campaign.Current?.CampaignBehaviorManager == null ? null :
            Campaign.Current.GetCampaignBehavior<RealmPolitics>();

        public override void SyncData(IDataStore store)
        {
            var rows = store.IsSaving ? _records.Select(e => e.Key + "\t" + e.Value).ToList() : new List<string>();
            store.SyncData("CasusBelli_PoliticalRegister_v1", ref rows);
            if (!store.IsLoading) return;
            _records.Clear();
            foreach (string row in rows ?? new List<string>())
            {
                int split = row?.IndexOf('\t') ?? -1;
                if (split > 0) _records[row.Substring(0,split)] = row.Substring(split + 1);
            }
        }

        internal string Read(string key) => _records.TryGetValue(key,out var value) ? value : null;
        internal void Write(string key,string value)
        {
            if (value == null) _records.Remove(key); else _records[key] = value;
        }
        internal int Number(string key,int fallback) => int.TryParse(Read(key),out var n) ? n : fallback;
        private static int Bound(int n) => Math.Max(0,Math.Min(100,n));
        internal static int Authority(Kingdom realm) => realm == null ? 50 :
            Current?.Number("authority|" + realm.StringId,RealmLaw.BaseAuthority(realm.StringId)) ?? RealmLaw.BaseAuthority(realm.StringId);
        internal static int Loyalty(Hero noble, Kingdom realm) => noble == null || realm?.Leader == null ? 50 :
            Current?.Number("oath|" + realm.Leader.StringId + "|" + noble.StringId,50) ?? 50;
        internal static int Controversy(Hero noble) => noble == null ? 0 : Current?.Number("scandal|" + noble.StringId,0) ?? 0;
        internal static void Shift(Kingdom realm,int change)
        {
            if (realm != null) Current?.Write("authority|" + realm.StringId,Bound(Authority(realm)+change).ToString());
        }
        internal static void Conduct(Hero noble,Kingdom realm,int loyalty,int scandal)
        {
            if (noble == null) return;
            if (realm?.Leader != null && loyalty != 0)
                Current?.Write("oath|" + realm.Leader.StringId + "|" + noble.StringId, Bound(Loyalty(noble,realm)+loyalty).ToString());
            if (scandal != 0) Current?.Write("scandal|" + noble.StringId,Bound(Controversy(noble)+scandal).ToString());
        }
        public override void RegisterEvents()
        {
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this,OnTransfer);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this,OnDefection);
            CampaignEvents.RulingClanChanged.AddNonSerializedListener(this,(realm,clan) => { Shift(realm,-4); RoyalSeat.Move(realm); });
        }
        private void OnTransfer(Settlement fief,bool open,Hero owner,Hero previous,Hero capturer,ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege || previous?.Clan?.Kingdom == owner?.Clan?.Kingdom) return;
            Shift(previous?.Clan?.Kingdom,-3);
            Shift(owner?.Clan?.Kingdom,3);
        }
        private void OnDefection(Clan clan,Kingdom oldRealm,Kingdom newRealm,ChangeKingdomAction.ChangeKingdomActionDetail detail,bool initial)
        {
            var crisis = oldRealm == null ? null : RealmFactions.Current?.Find(oldRealm);
            if (crisis?.Rebels != null && (newRealm == crisis.Rebels || newRealm == crisis.Realm)) return;
            if (!initial && detail == ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdomByDefection)
                Conduct(clan.Leader,oldRealm,-25,15);
        }
    }
}
