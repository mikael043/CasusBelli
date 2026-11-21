using System;
using System.Linq;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;

namespace CasusBelli
{
    internal static class CourtInvestigation
    {
        internal static string Block(Kingdom realm,Hero suspect)
        {
            if (suspect == null || suspect == realm.Leader || suspect.Clan?.Kingdom != realm || !suspect.IsAlive) return "Escolha um nobre do reino.";
            if (CouncilDuties.Officer(realm,CouncilOffice.Spymaster) == null) return "O cargo de mestre dos espiões está vago.";
            if (realm.RulingClan.Influence < 15) return "A investigação custa 15 de influência.";
            if (RealmPolitics.Current.Number("inquiry|" + realm.StringId,0) > CampaignTime.Now.ToDays) return "A rede de informantes ainda está ocupada.";
            return null;
        }
        internal static void Investigate(Kingdom realm,Hero suspect)
        {
            if (Block(realm,suspect) != null) return;
            var state = RealmPolitics.Current;
            var spy = CouncilDuties.Officer(realm,CouncilOffice.Spymaster);
            ChangeClanInfluenceAction.Apply(realm.RulingClan,-15);
            state.Write("inquiry|" + realm.StringId,((int)CampaignTime.Now.ToDays+21).ToString());
            string report = $"A investigação sobre {suspect.Name} terminou sem provas.";
            var faction = RealmFactions.Current?.Find(realm);
            bool conceals = spy == suspect || (RealmPolitics.Loyalty(spy,realm) < 45 && spy.GetRelation(suspect) > spy.GetRelation(realm.Leader));
            float skill = spy.GetSkillValue(DefaultSkills.Roguery) + spy.GetSkillValue(DefaultSkills.Scouting);
            if (!conceals && MBRandom.RandomFloat < Math.Min(.95f,.25f + skill/650f))
            {
                if (faction?.Valid == true && faction.Rebels == null && !faction.Exposed && faction.Members().Contains(suspect.Clan))
                {
                    faction.Exposed = true;
                    RealmPolitics.Conduct(suspect,realm,-6,10);
                    report = $"{spy.Name} comprovou que {suspect.Name} participa da coalizão por {faction.Demand}.";
                }
                else
                {
                    var claim = TitleManager.Instance.AllTitles.FirstOrDefault(t => t.OngoingClaims.ContainsKey(suspect));
                    if (claim != null) report = $"{suspect.Name} fabrica uma reivindicação sobre {claim.FullName}.";
                }
            }
            state.Write("intelligence|" + realm.StringId,report);
        }
        internal static void Weekly(Kingdom realm)
        {
            if (realm.Leader == Hero.MainHero) return;
            var suspect = realm.Clans.Where(c => c != realm.RulingClan && c.Leader != null && !c.IsEliminated && !c.IsUnderMercenaryService)
                .Select(c => c.Leader).OrderByDescending(h => RealmPolitics.Controversy(h) - h.GetRelation(realm.Leader)).FirstOrDefault();
            if (suspect != null && RealmPolitics.Controversy(suspect)-suspect.GetRelation(realm.Leader) >= 25) Investigate(realm,suspect);
        }
    }
}
