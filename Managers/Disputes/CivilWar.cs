using System.Linq;
using CasusBelli.Managers.Titles;
using CasusBelli.Managers.Titles.Governments;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;

namespace CasusBelli.Disputes
{
    internal static class CivilWar
    {
        internal const string Throne = "a sucessão ao trono", Autonomy = "a autonomia dos vassalos", Independence = "a independência", Crown = "o fortalecimento da Coroa", Relief = "a redução dos tributos feudais";
        internal static void Concede(PoliticalMovement m)
        {
            if (m.Demand == Throne)
            {
                var house = m.Pretender.Clan;
                if (house?.Kingdom == m.Realm && house.Leader?.IsAlive == true) ChangeRulingClanAction.Apply(m.Realm,house);
            }
            else if (m.Demand == Relief)
            {
                var crown = TitleManager.Instance.GetSovereignTitle(m.Realm);
                crown?.EnactLaw(DefaultDemesneLaws.Instance.LowTaxes);
                RealmPolitics.Shift(m.Realm,-3);
            }
            else RealmPolitics.Shift(m.Realm,m.Demand == Crown ? 12 : -15);
        }
        internal static void Secede(PoliticalMovement m,bool war)
        {
            var members = m.Members();
            var founder = m.Pretender.Clan;
            var name = new TextObject($"Liga de {founder.Name}");
            Campaign.Current.KingdomManager.CreateKingdom(name,name,founder.Culture,founder);
            m.Rebels = founder.Kingdom;
            if (m.Rebels == null || m.Rebels == m.Realm) return;
            foreach (var c in members.Where(c => c != founder && c.Kingdom == m.Realm))
                ChangeKingdomAction.ApplyByJoinToKingdomByDefection(c,m.Realm,m.Rebels,CampaignTime.Zero);
            m.InitialFiefs = m.Rebels.Fiefs.Count;
            if (war && !m.Rebels.IsAtWarWith(m.Realm))
            {
                if (m.Demand == Throne) DeclareWarAction.ApplyByClaimOnThrone(m.Rebels,m.Realm);
                else DeclareWarAction.ApplyByRebellion(m.Rebels,m.Realm);
            }
            if (war && m.Rebels.IsAtWarWith(m.Realm)) Campaign.Current.GetCampaignBehavior<CasusBelliCampaignBehavior>().RegisterCivilConflict(m);
            if (!war && m.Rebels.IsAtWarWith(m.Realm)) MakePeaceAction.Apply(m.Rebels,m.Realm);
        }
        internal static void Review(RealmFactions factions,PoliticalMovement m)
        {
            var rebels = m.Rebels;
            if (m.Realm.IsEliminated) { factions.End(m,"A Coroa caiu. A liga conservou seu reino."); return; }
            var main = Campaign.Current.GetCampaignBehavior<CasusBelliCampaignBehavior>();
            var record = main.GetWarRegistry().AllWars.LastOrDefault(w => (w.AttackerKingdomId == rebels.StringId && w.DefenderKingdomId == m.Realm.StringId) || (w.DefenderKingdomId == rebels.StringId && w.AttackerKingdomId == m.Realm.StringId));
            if (record?.IsActive == true && record.ArmisticeEnds >= 0 && !rebels.IsEliminated) return;
            double score = record == null ? 0 : Campaign.Current.GetCampaignBehavior<CasusBelliWarScoreBehavior>().ScoreFor(record,rebels.StringId);
            if (!rebels.IsEliminated && rebels.IsAtWarWith(m.Realm) && System.Math.Abs(score) < 85) return;
            bool dictated = record?.Outcome == "VICTORY" || record?.Outcome == "DEFENDER_VICTORY";
            bool rebelsWon = dictated ? (record.Outcome == "VICTORY") == (record.AttackerKingdomId == rebels.StringId) :
                (score >= 85 || (score > -85 && rebels.Fiefs.Count > m.InitialFiefs));
            bool won = !rebels.IsEliminated && rebelsWon;
            if (rebels.IsAtWarWith(m.Realm)) MakePeaceAction.Apply(rebels,m.Realm);
            if (won && m.Demand == Independence) { factions.End(m,"A liga conquistou sua independência."); return; }
            if (!rebels.IsEliminated)
            {
                foreach (var mercenary in rebels.Clans.Where(c => c.IsUnderMercenaryService).ToList())
                    ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(mercenary);
                var clans = rebels.Clans.Where(c => !c.IsUnderMercenaryService && !c.IsEliminated).OrderBy(c => c == rebels.RulingClan).ToList();
                foreach (var c in clans) ChangeKingdomAction.ApplyByJoinToKingdomByDefection(c,rebels,m.Realm,CampaignTime.Zero);
                if (!rebels.IsEliminated && rebels.Clans.Count == 0) DestroyKingdomAction.Apply(rebels);
            }
            if (won) Concede(m); else RealmPolitics.Shift(m.Realm,6);
            factions.End(m,won ? "A liga obteve " + m.Demand + "." : "A Coroa prevaleceu na guerra civil.");
        }
    }
}
