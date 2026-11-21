using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli.UI.Titles
{
    internal static class RoyalCouncilMenu
    {
        internal static void Open(Kingdom realm, Action back)
        {
            var council = Campaign.Current.GetCampaignBehavior<RoyalCouncil>();
            var court = CouncilSelection.Court(realm);
            var seats = CouncilSelection.Offices.Select(o =>
            {
                var h = court.FirstOrDefault(c => c.StringId == council.Member(realm,o));
                var label = h == null ? "Vago" : $"{h.Name} ({CouncilSelection.Score(realm,h,o):0.0})";
                return new InquiryElement(o, CouncilSelection.Name(o) + " — " + label,null);
            }).ToList();
            if (realm.Leader != Hero.MainHero)
            {
                InformationManager.ShowInquiry(new InquiryData("Conselho do reino", string.Join("\n", seats.Select(s => s.Title)),
                    true,false,"Voltar","",back,null));
                return;
            }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Conselho do reino",
                "Escolha um cargo para consultar os candidatos por competência.",seats,true,1,1,"Consultar","Voltar",
                selection => { if (selection.Count == 1) Choose(realm,(CouncilOffice)selection[0].Identifier,() => Open(realm,back)); }, s => back()));
        }
        private static void Choose(Kingdom realm, CouncilOffice office, Action back)
        {
            var council = Campaign.Current.GetCampaignBehavior<RoyalCouncil>();
            var options = council.Candidates(realm,office,CouncilSelection.Court(realm))
                .Select(h => new InquiryElement(h,$"{h.Name} — {CouncilSelection.Score(realm,h,office):0.0}" +
                    (office == CouncilOffice.Marshal ? $" · {h.Clan.Influence:0} de influência no clã" : ""),null)).ToList();
            if (council.Member(realm,office) != null) options.Add(new InquiryElement("dismiss","Dispensar o titular",null));
            if (options.Count == 0)
            {
                InformationManager.ShowInquiry(new InquiryData(CouncilSelection.Name(office),"Nenhum candidato elegível disponível.",
                    true,false,"Voltar","",back,null));
                return;
            }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(CouncilSelection.Name(office),
                office == CouncilOffice.Marshal ? MarshalCommand.Restriction : "Escolha o titular do cargo.",options,true,1,1,"Confirmar","Voltar",
                selection => { if (selection.Count == 1) council.Appoint(realm,office,selection[0].Identifier as Hero); back(); }, s => back()));
        }
    }
}
