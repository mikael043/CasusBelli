using System;
using System.Collections.Generic;
using System.Linq;
using CasusBelli.Disputes;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli.UI.Titles
{
    internal static class PoliticsMenu
    {
        internal static void Info(string heading,string body,Action back) =>
            InformationManager.ShowInquiry(new InquiryData(heading,body,true,false,"Voltar","",back,null));
        internal static InquiryElement Item(string name,Action action,string block = null) =>
            new InquiryElement(action,name,null,block == null,block ?? name);
        internal static void Choose(string heading,string body,List<InquiryElement> items,Action back)
        {
            if (items.Count == 0) { Info(heading,body,back); return; }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(heading,body,items,true,1,1,"Abrir","Voltar",
                choices => { if (choices.Count == 1) ((Action)choices[0].Identifier)(); },choices => back(),isSeachAvailable: items.Count > 12));
        }
        internal static string Summary(Kingdom realm) =>
             $"{RealmLaw.Name(realm.StringId)}. Sede da corte: {RoyalSeat.Get(realm)?.Name}. Autoridade da Coroa: {RealmPolitics.Authority(realm)}/100. " +
            "A revogação exige 45 de autoridade. A Coroa pode contrariar uma votação ordinária a partir de 60. " +
            "Com 75, os vassalos perdem o direito de lançar ultimatos privados. A eleição do soberano é vinculante.";
        internal static void Open(Kingdom realm)
        {
            var state = RealmPolitics.Current;
            if (state == null || realm == null) return;
            Action back = () => Open(realm);
            string Report(string prefix,string empty) => state.Read(prefix + "|" + realm.StringId) ?? empty;
            var items = new List<InquiryElement> {
                Item("Leis e sucessão",() => Laws(realm,back)),
                Item("Última votação nominal",() => Lines("Última votação",Report("ballot","Nenhuma votação apurada desde a atualização."),back)),
                Item("Lealdade e controvérsia",() => Choose("Nobres do reino","Escala de 0 a 100.",realm.Clans.Where(c => !c.IsEliminated && c.Leader != null)
                    .Select(c => Item($"{c.Leader.Name} · {c.Name}",() => Info(c.Name.ToString(),
                    $"Lealdade ao soberano: {RealmPolitics.Loyalty(c.Leader,realm)}. Controvérsia: {RealmPolitics.Controversy(c.Leader)}. Peso de voto: {RealmLaw.Weight(c)}.",back))).ToList(),back)),
                Item("Prestação de contas",() => Info("Tesouro real",$"Saldo: {realm.KingdomBudgetWallet:N0} denários.\n" + Report("accounts","O intendente ainda não fechou uma semana."),back)),
                Item("Facções e sucessão contestada",() => Factions(realm,back)),
                Item("Diplomacia e guerras",() => Foreign(realm,back)) };
            if (realm.Leader == Hero.MainHero)
                items.Add(Item("Relatório de inteligência",() => Choose("Mestre dos Espiões",Report("intelligence","Nenhuma descoberta comunicada."),
                    realm.Clans.Where(c => c.Leader != null && c != realm.RulingClan && !c.IsEliminated && !c.IsUnderMercenaryService)
                    .Select(c => Item("Investigar " + c.Leader.Name + " · 15 de influência",() =>
                    {
                        CourtInvestigation.Investigate(realm,c.Leader);
                        Info("Resultado da investigação",Report("intelligence","Sem relatório."),back);
                    },CourtInvestigation.Block(realm,c.Leader))).ToList(),back)));
            Choose(realm.Name.ToString(),Summary(realm),items,() => {});
        }
        private static void Lines(string heading,string body,Action back)
        {
            var lines = body.Split('\n');
            Choose(heading,lines[0],lines.Skip(1).Select(line => Item(line,() => Info(heading,line,back))).ToList(),back);
        }
        private static void Laws(Kingdom realm,Action back)
        {
            var crown = TitleManager.Instance.GetSovereignTitle(realm);
            string rules = RealmLaw.Preference(realm.StringId);
            var laws = crown?.Contract?.DemesneLaws?.Select(l => l.StringId + " · " + l.Level) ?? Enumerable.Empty<string>();
            Info("Leis e sucessão",$"{RealmLaw.Name(realm.StringId)}\n{rules}\n\nO direito sobre cada título pessoal segue o contrato exibido na hierarquia.\n\n" +
                string.Join("\n",laws.Select(RealmLaw.Translate)),back);
        }
        private static void Factions(Kingdom realm,Action back)
        {
            var factions = RealmFactions.Current;
            var movement = factions.Find(realm);
            var items = new List<InquiryElement>();
            string body = RealmPolitics.Current.Read("crisis|" + realm.StringId) ?? "Nenhum ultimato resolvido.";
            if (movement == null && Hero.MainHero.Clan?.Kingdom == realm && realm.Leader != Hero.MainHero && RealmPeers.Rank(Clan.PlayerClan) == 2 && Clan.PlayerClan.Leader == Hero.MainHero)
                foreach (var demand in new[]{CivilWar.Autonomy,CivilWar.Relief,CivilWar.Crown,CivilWar.Independence,CivilWar.Throne})
                    items.Add(Item("Organizar apoio para " + demand,() => { factions.Start(realm,Hero.MainHero,demand); Factions(realm,back); },
                        RealmPolitics.Current.Number("quiet|" + realm.StringId,0) > CampaignTime.Now.ToDays ? "Há uma trégua entre as facções." : null));
            if (movement != null && (movement.Exposed || movement.Ultimatum || movement.Rebels != null || movement.Pretender.Clan == Clan.PlayerClan || movement.Offers.Any(o => o.Recipient == Clan.PlayerClan && !o.Arrival.IsFuture)))
            {
                body = $"{movement.Pretender.Name} defende {movement.Demand}. " +
                    (movement.Rebels != null ? $"Em guerra sob o estandarte de {movement.Rebels.Name}." : $"Clãs comprometidos: {string.Join(", ",movement.Members().Select(c => c.Name))}.");
                if (movement.Rebels == null && movement.Pretender == Hero.MainHero)
                {
                    foreach (var c in realm.Clans.Where(movement.CanInvite))
                        items.Add(Item("Enviar emissário a " + c.Name,() => { movement.Invite(c); Factions(realm,back); }));
                    items.Add(Item("Apresentar ultimato",() => factions.Press(movement),!movement.CanPress() ?
                        "Exige um feudo próprio, dois clãs e pelo menos 40% da força de campanha do reino." : null));
                }
                else if (movement.Rebels == null && !movement.Ultimatum && movement.Offers.Any(o => o.Recipient == Clan.PlayerClan && o.Accepted))
                    items.Add(Item("Retirar meu apoio",() => { movement.Offers.RemoveAll(o => o.Recipient == Clan.PlayerClan); Factions(realm,back); }));
            }
            Choose("Facções políticas",body,items,back);
        }
        private static void Foreign(Kingdom realm,Action back)
        {
            var main = Campaign.Current.GetCampaignBehavior<CasusBelliCampaignBehavior>();
            var score = Campaign.Current.GetCampaignBehavior<CasusBelliWarScoreBehavior>();
            var options = main.GetWarRegistry().AllWars.Where(w => w.IsActive && (w.AttackerKingdomId == realm.StringId || w.DefenderKingdomId == realm.StringId))
                .Select(w => Item(w.Description + " · " + w.WarId,() => Info("Guerra",score.BuildWarText(w,realm),back))).ToList();
            foreach (var other in Kingdom.All.Where(k => !k.IsEliminated && k != realm && !realm.IsAtWarWith(k)))
            {
                void Embassy()
                {
                    int days = RealmDiplomacy.PactDays(realm.StringId,other.StringId);
                    var actions = new List<InquiryElement>();
                    if (realm.Leader == Hero.MainHero) foreach (bool alliance in new[]{false,true})
                        actions.Add(Item(alliance ? "Propor aliança · 100 denários" : "Propor pacto anual · 100 denários",
                            () => RealmDiplomacy.Offer(realm,other,alliance),RealmDiplomacy.Block(realm,other,alliance)));
                    Choose(other.Name.ToString(),days > 0 ? $"Pacto em vigor por mais {days} dias." : "Os reinos estão em paz.",actions,() => Foreign(realm,back));
                }
                options.Add(Item(other.Name.ToString(),Embassy));
            }
            Choose("Diplomacia",RealmPolitics.Current.Read("foreign|" + realm.StringId) ?? "Consulte uma guerra ou uma embaixada.",options,back);
        }
    }
}
