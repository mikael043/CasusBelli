using System;
using System.Linq;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace CasusBelli
{
    internal static class RealmLaw
    {
        internal static string Name(string id) => id switch
        {
            "vlandia" => "Monarquia feudal eletiva", "sturgia" => "Principado territorial",
            "battania" => "Confederação de clãs", "empire" => "Eleição senatorial",
            "empire_s" => "Sucessão imperial dinástica", "empire_w" => "Aclamação militar",
            "aserai" => "Sultanato aristocrático", "khuzait" => "Canato dos clãs",
            "nord" => "Assembleia dos nobres", _ => "Assembleia do reino"
        };
        internal static string Preference(string id) => id switch
        {
            "empire_s" => "O clã reinante tem preferência dinástica na eleição.", "empire_w" => "A liderança militar favorece os candidatos e os grupos de guerra dão peso aos votos.",
            "empire" => "A influência favorece candidatos ao trono e o grau de pariato determina o peso dos votos.",
            "aserai" => "O patrimônio favorece candidatos e dá peso aos votos.", "khuzait" => "Tática e alianças pessoais favorecem candidatos. Grupos de guerra dão peso aos votos.",
            "battania" or "nord" => "Cada clã tem um voto por grau de apoio. Prestígio e relações favorecem os candidatos.",
            _ => "Ducados e condados dão peso aos votos. Posse territorial favorece candidatos ao trono."
        };
        internal static string Translate(string law)
        {
            var parts = (law ?? "").Split(new[]{" · "},StringSplitOptions.None);
            string label = parts[0] switch {
                "agnatic" => "Agnática","agnatic_cognatic" => "Preferência masculina","absolute_cognatic" => "Preferência feminina","cognatic" => "Cognática",
                "primogeniture" => "Primogenitura","ultimogeniture" => "Ultimogenitura","gavelkind" => "Partilha","feudal_elective" => "Eletiva feudal","republic" => "Republicana","seniority" => "Senioridade","hereditary" => "Hereditária",
                "low_taxes" => "Tributos baixos","medium_taxes" => "Tributos médios","high_taxes" => "Tributos altos","oppressive_taxes" => "Tributos opressivos",
                "low_levies" => "Serviço militar leve","medium_levies" => "Serviço militar regular","high_levies" => "Serviço militar elevado","maximum_levies" => "Serviço militar máximo",
                "local_justice" => "Justiça local","royal_justice" => "Justiça real","harsh_justice" => "Justiça rigorosa",
                "basic_infrastructure" => "Infraestrutura básica","improved_infrastructure" => "Infraestrutura melhorada","advanced_infrastructure" => "Infraestrutura avançada",
                "free_trade" => "Comércio livre","regulated_trade" => "Comércio regulado","restricted_trade" => "Comércio restrito",
                "" => "Indefinida", _ => parts[0].Replace('_',' ')
            };
            return parts.Length > 1 ? label + " · " + parts[1] : label;
        }
        internal static int BaseAuthority(string id) => id switch
        {
            "empire_s" => 70, "empire_w" => 65, "aserai" => 60, "khuzait" => 55,
            "sturgia" => 40, "empire" => 40, "battania" => 35, "nord" => 35, _ => 50
        };
        internal static int Weight(Clan c)
        {
            if (RealmPeers.Rank(c) == 0) return 0;
            int title = TitleManager.Instance.GetAllDeJure(c).Select(t => (int)t.TitleType).DefaultIfEmpty(6).Min();
            switch (c.Kingdom.StringId)
            {
                case "battania": case "nord": return 1;
                case "empire": return RealmPeers.Rank(c);
                case "aserai": return 1 + Math.Min(2,c.Leader.Gold / 100000);
                case "empire_w": case "khuzait": return 1 + Math.Min(2,c.WarPartyComponents.Count);
                default: return title <= (int)TitleType.Dukedom ? 3 : title <= (int)TitleType.County ? 2 : 1;
            }
        }
        internal static float Candidacy(Clan voter, Hero candidate)
        {
            var clan = candidate.Clan;
            var realm = voter.Kingdom;
            float merit = .35f * voter.Leader.GetRelation(candidate) - RealmPolitics.Controversy(candidate)
                + (candidate.Culture == voter.Culture ? 8 : 0) + (clan == realm.RulingClan ? RealmPolitics.Loyalty(voter.Leader,realm)-50 : 0);
            var campaign = RealmFactions.Current?.Find(realm);
            if (campaign?.Demand == Disputes.CivilWar.Throne && campaign.Pretender == candidate && campaign.Members().Contains(voter)) merit += 50;
            switch (realm.StringId)
            {
                case "empire_s": return merit + (clan == realm.RulingClan ? 180 : 0);
                case "empire": return merit + Math.Min(70,clan.Influence / 10);
                case "empire_w": return merit + candidate.GetSkillValue(DefaultSkills.Leadership) / 3;
                case "khuzait": return merit + candidate.GetSkillValue(DefaultSkills.Tactics) / 4 + voter.Leader.GetRelation(candidate) / 3;
                case "aserai": return merit + Math.Min(80,candidate.Gold / 3000);
                case "battania": case "nord": return merit + voter.Leader.GetRelation(candidate) / 2 + clan.Tier * 4;
                default: return merit + Weight(clan) * 12 + clan.Fiefs.Count * 3;
            }
        }
    }
}
