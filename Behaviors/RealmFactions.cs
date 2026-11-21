using System;
using System.Collections.Generic;
using System.Linq;
using CasusBelli.Disputes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli
{
    public sealed class RealmFactions : CampaignBehaviorBase
    {
        private List<PoliticalMovement> _movements = new List<PoliticalMovement>();
        private readonly HashSet<PoliticalMovement> _prompts = new HashSet<PoliticalMovement>();
        internal static RealmFactions Current => Campaign.Current?.CampaignBehaviorManager == null ? null : Campaign.Current.GetCampaignBehavior<RealmFactions>();
        internal PoliticalMovement Find(Kingdom realm) => realm == null ? null : _movements.FirstOrDefault(m => m.Realm == realm || m.Rebels == realm);
        public override void SyncData(IDataStore store)
        {
            store.SyncData("CasusBelli_Movements_v1",ref _movements);
            _movements ??= new List<PoliticalMovement>();
        }
        public override void RegisterEvents() => CampaignEvents.DailyTickEvent.AddNonSerializedListener(this,Tick);
        internal void Start(Kingdom realm,Hero hero,string demand)
        {
            if (realm == null || hero == null || Find(realm) != null || RealmPolitics.Current.Number("quiet|" + realm.StringId,0) > CampaignTime.Now.ToDays) return;
            var movement = new PoliticalMovement(realm,hero,demand);
            if (movement.Valid) _movements.Add(movement);
        }
        internal void End(PoliticalMovement m,string reason)
        {
            _movements.Remove(m);
            _prompts.Remove(m);
            RealmPolitics.Current.Write("crisis|" + m.Realm.StringId,reason);
            RealmPolitics.Current.Write("quiet|" + m.Realm.StringId,((int)CampaignTime.Now.ToDays + 42).ToString());
            if (Clan.PlayerClan?.Kingdom == m.Realm || Clan.PlayerClan?.Kingdom == m.Rebels)
                InformationManager.DisplayMessage(new InformationMessage(reason));
        }
        private void Tick()
        {
            foreach (var m in _movements.ToArray())
            {
                if (m.Rebels != null) { CivilWar.Review(this,m); continue; }
                if (!m.Valid || !m.Deadline.IsFuture) { End(m,"A coalizão se dissolveu."); continue; }
                if (m.Ultimatum) { Press(m); continue; }
                m.Offers.RemoveAll(o => !m.Eligible(o.Recipient) || (o.Answered && o.Recipient != Clan.PlayerClan &&
                    (o.Accepted ? m.Sympathy(o.Recipient) < -15 : o.Arrival.ElapsedDaysUntilNow > 28)));
                foreach (var offer in m.Offers.Where(o => !o.Answered && !o.Arrival.IsFuture).ToList())
                {
                    if (offer.Recipient != Clan.PlayerClan) offer.Answer(m.Sympathy(offer.Recipient) >= 10);
                    else if (_prompts.Add(m))
                    {
                        void Answer(bool yes) { _prompts.Remove(m); if (m.Valid && m.Eligible(offer.Recipient)) offer.Answer(yes); }
                        InformationManager.ShowInquiry(new InquiryData("Emissário da nobreza",$"{m.Pretender.Name} pede apoio para {m.Demand}. O compromisso pode levar a uma guerra civil.",
                            true,true,"Apoiar","Recusar",() => Answer(true),() => Answer(false)),true);
                    }
                }
                if (m.Pretender.Clan == Clan.PlayerClan) continue;
                var next = m.Realm.Clans.Where(m.CanInvite).OrderByDescending(m.Sympathy).FirstOrDefault();
                if (next != null) m.Invite(next);
                if (m.CanPress()) Press(m);
            }
            if ((int)CampaignTime.Now.ToDays % 7 != 0) return;
            foreach (var realm in Kingdom.All.Where(k => !k.IsEliminated && k.Leader != null && Find(k) == null).ToList())
            {
                var unhappy = realm.Clans.Where(c => c != Clan.PlayerClan && c != realm.RulingClan && c.Leader != null &&
                    !c.IsUnderMercenaryService && !c.IsEliminated && c.Fiefs.Count > 0 &&
                    c.Leader.GetRelation(realm.Leader) + RealmPolitics.Loyalty(c.Leader,realm) - RealmPolitics.Controversy(realm.Leader) < 15)
                    .OrderByDescending(c => c.Influence).FirstOrDefault();
                if (unhappy != null)
                {
                    var law = Managers.Titles.TitleManager.Instance.GetSovereignTitle(realm)?.Contract?.DemesneLaws
                        ?.FirstOrDefault(l => l.LawType == Managers.Titles.Governments.DemesneLaw.DemesneLawType.Taxation);
                    Start(realm,unhappy.Leader,law?.Level >= 3 ? CivilWar.Relief : unhappy.Culture != realm.Culture ? CivilWar.Independence : CivilWar.Autonomy);
                }
                else if (RealmPolitics.Authority(realm) < 45)
                {
                    var ally = realm.Clans.Where(c => c.Leader != null && c != Clan.PlayerClan && c != realm.RulingClan &&
                        RealmPeers.Rank(c) == 2 && c.Leader.GetRelation(realm.Leader) >= 35 && RealmPolitics.Loyalty(c.Leader,realm) >= 65)
                        .OrderByDescending(c => c.Influence).FirstOrDefault();
                    if (ally != null) Start(realm,ally.Leader,CivilWar.Crown);
                }
            }
        }
        internal void Press(PoliticalMovement m)
        {
            if (!m.CanPress()) { m.Ultimatum = false; return; }
            m.Ultimatum = true;
            if (m.Realm.Leader != Hero.MainHero)
            {
                Decide(m,m.Demand == CivilWar.Crown || (RealmPolitics.Authority(m.Realm) < 35 && m.Demand != CivilWar.Throne));
                return;
            }
            if (_prompts.Add(m))
                InformationManager.ShowInquiry(new InquiryData("Ultimato à Coroa",$"{m.Pretender.Name} exige {m.Demand}. A recusa leva os clãs comprometidos à guerra.",
                    true,true,"Ceder","Recusar",() => Decide(m,true),() => Decide(m,false)),true);
        }
        private void Decide(PoliticalMovement m,bool yield)
        {
            _prompts.Remove(m);
            if (!_movements.Contains(m) || !m.CanPress()) { m.Ultimatum = false; return; }
            if (!yield && m.Demand == CivilWar.Crown) { End(m,"A Coroa recusou os poderes oferecidos por seus partidários."); return; }
            if (yield && m.Demand != CivilWar.Independence)
            {
                CivilWar.Concede(m); End(m,"A Coroa aceitou o ultimato por " + m.Demand + "."); return;
            }
            CivilWar.Secede(m,!yield);
            if (m.Rebels == null || m.Rebels == m.Realm) { End(m,"A liga perdeu as condições para se separar."); return; }
            if (yield) End(m,"A Coroa reconheceu a independência da coalizão.");
        }
    }
}
