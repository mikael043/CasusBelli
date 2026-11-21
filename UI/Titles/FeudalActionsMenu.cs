using System;
using System.Collections.Generic;
using System.Linq;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli.UI.Titles
{
    internal static class FeudalActionsMenu
    {
        internal static void Open(FeudalTitle title,Action back)
        {
            var hero = Hero.MainHero;
            var manager = TitleManager.Instance;
            var model = manager.GetModel();
            var items = new List<InquiryElement>();
            void Add(ActionType type,string label,Hero recipient = null)
            {
                var action = model.GetAction(type,title,hero,recipient);
                items.Add(PoliticsMenu.Item(label,() =>
                {
                    string details = $"{label} · {title.FullName}\nOuro: {action.Gold:0}. Influência: {action.Influence:0}. Renome: {action.Renown:0}.";
                    if (type == ActionType.Revoke && title.GetHeroClaim(hero) == ClaimType.None) details += "\nRevogar sem direito prévio custa 4 de autoridade e gera 10 de controvérsia.";
                    InformationManager.ShowInquiry(new InquiryData(label,details,true,true,"Confirmar","Voltar",() => {
                        var check = model.GetAction(type,title,hero,recipient);
                        if (check.Possible) check.TakeAction(recipient);
                        back();
                    },back));
                },action.Possible ? null : action.Reason?.ToString() ?? "Indisponível."));
            }
            if (title.deJure == hero && !title.IsSovereignLevel && hero.Clan?.Kingdom != null)
                foreach (var clan in hero.Clan.Kingdom.Clans.Where(c => c.Leader != null && !c.IsEliminated && !c.IsUnderMercenaryService && c.Leader != hero))
                    Add(ActionType.Grant,"Conceder a " + clan.Leader.Name,clan.Leader);
            if (title.deJure != null && title.deJure != hero)
            {
                Add(ActionType.Revoke,"Revogar o título");
                Add(ActionType.Usurp,"Usurpar o título");
                items.Add(PoliticsMenu.Item("Pedir reconhecimento ao chanceler",() => TitlePetitions.Request(title,hero),TitlePetitions.Block(title,hero)));
            }
            PoliticsMenu.Choose(title.FullName.ToString(),"Escolha o ato feudal para consultar seus requisitos e custos.",items,back);
        }
    }
}
