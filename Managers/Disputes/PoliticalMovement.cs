using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.SaveSystem;

namespace CasusBelli.Disputes
{
    public sealed class PoliticalMovement
    {
        [SaveableProperty(1)] public Kingdom Realm { get; private set; }
        [SaveableProperty(2)] public Hero Pretender { get; private set; }
        [SaveableProperty(3)] public string Demand { get; private set; }
        [SaveableProperty(4)] public List<SupportOffer> Offers { get; private set; }
        [SaveableProperty(5)] public Kingdom Rebels { get; internal set; }
        [SaveableProperty(6)] public CampaignTime Deadline { get; private set; }
        [SaveableProperty(7)] public bool Ultimatum { get; internal set; }
        [SaveableProperty(8)] public int InitialFiefs { get; internal set; }
        [SaveableProperty(9)] public CampaignTime LastDispatch { get; private set; }
        [SaveableProperty(10)] public Hero Challenged { get; private set; }
        [SaveableProperty(11)] public bool Exposed { get; internal set; }
        internal PoliticalMovement(Kingdom realm,Hero hero,string demand)
        {
            Realm = realm; Pretender = hero; Demand = demand; Challenged = realm?.Leader;
            Deadline = CampaignTime.YearsFromNow(1);
            Offers = new List<SupportOffer>();
        }
        internal bool Valid => Realm?.IsEliminated == false && Realm.Leader == Challenged && Pretender?.IsAlive == true &&
            Pretender.Clan?.Leader == Pretender && Pretender.Clan.Kingdom == Realm && Pretender != Realm.Leader && !Pretender.Clan.IsUnderMercenaryService;
        internal bool Eligible(Clan c) => c?.Kingdom == Realm && c.Leader != null && !c.IsEliminated &&
            !c.IsUnderMercenaryService && c != Realm.RulingClan && c != Pretender.Clan;
        internal bool CanInvite(Clan c) => Valid && Rebels == null && !Ultimatum && Eligible(c) &&
            (LastDispatch.Equals(default(CampaignTime)) || LastDispatch.ElapsedDaysUntilNow >= 7) &&
            !Offers.Any(o => o.Recipient == c);
        internal void Invite(Clan c)
        {
            if (!CanInvite(c)) return;
            Offers.Add(new SupportOffer(c,true));
            LastDispatch = CampaignTime.Now;
        }
        internal float Sympathy(Clan c) => (Demand == CivilWar.Crown ? 1.5f * (RealmPolitics.Loyalty(c.Leader,Realm)-50) + .9f * c.Leader.GetRelation(Realm.Leader) : 0) + .6f * (c.Leader.GetRelation(Pretender) - c.Leader.GetRelation(Realm.Leader))
            - (Exposed ? 12 : 0) + 50 - RealmPolitics.Loyalty(c.Leader,Realm) + .4f * (RealmPolitics.Controversy(Realm.Leader)-RealmPolitics.Controversy(Pretender))
            + (c.Culture == Pretender.Culture ? 5 : 0) + c.Leader.GetTraitLevel(DefaultTraits.Calculating) * 3;
        internal List<Clan> Members() => Offers.Where(o => o.Accepted && Eligible(o.Recipient)).Select(o => o.Recipient)
            .Concat(new[]{Pretender.Clan}).Distinct().ToList();
        internal bool CanPress()
        {
            if (!Valid || Rebels != null) return false;
            var members = Members();
            var peers = Realm.Clans.Where(c => c.Leader != null && !c.IsEliminated && !c.IsUnderMercenaryService).ToList();
            return members.Count >= 2 && Pretender.Clan.Fiefs.Count > 0 &&
                members.Sum(c => 1 + DisputeAi.FieldTroops(c)) >= .4f * peers.Sum(c => 1 + DisputeAi.FieldTroops(c));
        }
    }
}
