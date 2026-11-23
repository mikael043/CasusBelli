using System.Collections.Generic;
using System.Linq;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace CasusBelli
{
    public static class PoliticsCommands
    {
        [CommandLineFunctionality.CommandLineArgumentFunction("politica","casus_belli")]
        public static string Politics(List<string> args)
        {
            if (RealmPolitics.Current == null) return "Carregue uma campanha.";
            var realm = args == null || args.Count == 0 ? Clan.PlayerClan?.Kingdom : Kingdom.All.FirstOrDefault(k => k.StringId == args[0]);
            if (realm == null) return "Informe o identificador do reino, por exemplo casus_belli.politica vlandia.";
            var crisis = RealmFactions.Current?.Find(realm);
            var lines = new List<string> { realm.Name.ToString(),UI.Titles.PoliticsMenu.Summary(realm),
                $"Soberano: {realm.Leader?.Name}. Tesouro: {realm.KingdomBudgetWallet}.",
                RealmPolitics.Current.Read("accounts|" + realm.StringId) ?? "Sem contas fechadas.",
                crisis == null ? "Sem coalizão ativa." : $"Coalizão de {crisis.Pretender.Name} por {crisis.Demand}. Emissários: {crisis.Offers.Count}. Ultimato: {crisis.Ultimatum}.",
                RealmPolitics.Current.Read("ballot|" + realm.StringId) ?? "Sem votação registrada." };
            return string.Join("\n",lines);
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("auditar_ducados","casus_belli")]
        public static string AuditDuchies(List<string> args)
        {
            if (Campaign.Current == null || !TitleManager.Instance.IsInitialized) return "Carregue uma campanha.";
            return Campaign.Current.GetCampaignBehavior<TitleDisputeBehavior>()?.DucalAudit() ?? "Sistema de disputas indisponível.";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("auditar_guerras_pessoais","casus_belli")]
        public static string AuditPersonalWars(List<string> args)
        {
            if (RealmPolitics.Current == null) return "Carregue uma campanha.";
            var realm = args == null || args.Count == 0 ? Clan.PlayerClan?.Kingdom :
                Kingdom.All.FirstOrDefault(k => k.StringId == args[0]);
            if (realm == null) return "Informe o reino, por exemplo casus_belli.auditar_guerras_pessoais vlandia.";
            var behavior = Campaign.Current.GetCampaignBehavior<TitleDisputeBehavior>();
            var entries = behavior.Active.Where(d => d.Realm == realm).Select(d =>
            {
                string phase = d.Contested ? "GUERRA PESSOAL" : d.Demanded ? "ULTIMATO" :
                    d.TruceEnds.IsFuture ? "TRÉGUA" : "RECRUTAMENTO";
                string votes = d.IsDucal ? $" | pares: {CasusBelli.Disputes.PersonalTitleWar.DucalSupporters(d)}/{CasusBelli.Disputes.PersonalTitleWar.DucalPeers(d)}" : "";
                return $"{d.Title.FullName}: {phase}. {d.Claimant.Name} contra {d.Holder.Name}" +
                    $" | saldo: {d.Balance:0} | tropas: {CasusBelli.Disputes.PersonalTitleWar.Troops(d,true)}/{CasusBelli.Disputes.PersonalTitleWar.Troops(d,false)}{votes}.";
            }).ToList();
            string recent = RealmPolitics.Current.Read("personal_dispute_recent|" + realm.StringId) ??
                "Nenhuma ocorrência recente registrada.";
            int royalWait = CasusBelli.Disputes.PersonalTitleWar.RoyalInterventionNextDay(realm) -
                (int)CampaignTime.Now.ToDays;
            string royal = royalWait > 0 ? $"Intervenção real disponível em {royalWait} dias." :
                "Intervenção real sem intervalo pendente.";
            return (entries.Count == 0 ? "Nenhuma disputa ativa." : string.Join("\n",entries)) +
                "\n" + royal + "\nÚltima ocorrência: " + recent;
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("auditar_politica","casus_belli")]
        public static string Audit(List<string> args)
        {
            if (RealmPolitics.Current == null) return "Carregue uma campanha.";
            var manager = TitleManager.Instance;
            var issues = new List<string>();
            var titles = manager.AllTitles;
            if (!manager.IsInitialized || titles.Count == 0) issues.Add("A hierarquia feudal ainda não foi inicializada.");
            var parents = titles.SelectMany(t => t.Vassals.Select(v => new { Parent=t, Child=v })).ToLookup(p => p.Child);
            foreach (var t in titles)
            {
                if (t.Contract == null || t.deJure?.IsDead == true) issues.Add($"{t.StringId}: contrato ausente ou titular falecido.");
                if (!t.IsSovereignLevel && (parents[t].Count() != 1 || parents[t].Any(p => p.Parent.TitleType >= t.TitleType))) issues.Add($"{t.StringId}: vínculo hierárquico inválido.");
            }
            var wars = Campaign.Current.GetCampaignBehavior<CasusBelliCampaignBehavior>().GetWarRegistry().AllWars.Where(w => w.IsActive).ToList();
            foreach (var group in wars.GroupBy(w => string.Join("|", new[]{w.AttackerKingdomId,w.DefenderKingdomId}.OrderBy(s => s))))
                if (group.Count() > 1) issues.Add(group.Key + ": mais de uma guerra ativa para o par.");
            return $"Títulos: {titles.Count}. Guerras ativas: {wars.Count}. Ocorrências: {issues.Count}.\n" + string.Join("\n",issues);
        }
    }
}