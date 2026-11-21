using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli.Managers.Titles
{
    internal static class TitlePetitions
    {
        internal static string Block(FeudalTitle title,Hero petitioner)
        {
            var realm = petitioner?.Clan?.Kingdom;
            if (realm == null || petitioner.Clan.Leader != petitioner || title?.Fief?.OwnerClan != petitioner.Clan ||
                title.deJure == null || title.IsSovereignLevel || title.deJure == petitioner) return "O requerente deve comandar o clã que possui o feudo.";
            if (title.deJure.Clan?.Kingdom == null || realm.IsAtWarWith(title.deJure.Clan.Kingdom)) return "O titular e o requerente precisam estar em paz.";
            if (!title.HeroHasValidClaim(petitioner)) return "É preciso uma reivindicação madura.";
            if (CouncilDuties.Officer(realm,CouncilOffice.Chancellor) == null) return "É preciso um chanceler em exercício.";
            if (RealmPolitics.Current.Number("petition|" + title.StringId,0) > CampaignTime.Now.ToDays) return "O título já foi avaliado nas últimas quatro semanas.";
            return null;
        }
        internal static void Request(FeudalTitle title,Hero petitioner)
        {
            if (Block(title,petitioner) != null) return;
            var realm = petitioner.Clan.Kingdom;
            var legal = title.deJure;
            var foreign = legal.Clan.Kingdom;
            var envoy = CouncilDuties.Officer(realm,CouncilOffice.Chancellor);
            string request = $"{petitioner.Name} pede que {legal.Name} reconheça seu direito sobre {title.FullName}. A aprovação transfere o título de jure.";
            bool Eligible() => title.deJure == legal && petitioner.IsAlive && legal.IsAlive && petitioner.Clan?.Kingdom == realm &&
                legal.Clan?.Kingdom == foreign && !realm.IsAtWarWith(foreign) && title.Fief.OwnerClan == petitioner.Clan && title.HeroHasValidClaim(petitioner);
            void Grant()
            {
                if (!Eligible()) return;
                title.RemoveClaim(petitioner);
                title.AddClaim(legal,ClaimType.PreviousOwner,true);
                TitleManager.Instance.ExecuteOwnershipChange(legal,petitioner,title,true);
                RealmPolitics.Conduct(petitioner,realm,4,-2);
                Notice($"{petitioner.Name} obteve o reconhecimento de {title.FullName}.");
            }
            void Notice(string text)
            {
                RealmPolitics.Current.Write("foreign|" + realm.StringId,text);
                if (realm == Clan.PlayerClan?.Kingdom || foreign == Clan.PlayerClan?.Kingdom)
                    InformationManager.DisplayMessage(new InformationMessage(text));
            }
            void Refuse() => Notice($"O pedido de {petitioner.Name} por {title.FullName} foi recusado.");
            void Review()
            {
                if (!Eligible()) return;
                if (legal == Hero.MainHero || (foreign != realm && foreign.Leader == Hero.MainHero))
                    InformationManager.ShowInquiry(new InquiryData("Reconhecimento de título",request,true,true,"Reconhecer","Recusar",Grant,Refuse),true);
                else if (envoy.GetSkillValue(DefaultSkills.Charm) / 4f + legal.GetRelation(petitioner) -
                    RealmPolitics.Controversy(petitioner) + (foreign == realm ? RealmPolitics.Authority(realm)/4f : 0) >= 35) Grant();
                else Refuse();
            }
            RealmPolitics.Current.Write("petition|" + title.StringId,((int)CampaignTime.Now.ToDays+28).ToString());
            if (realm.Leader == Hero.MainHero && petitioner != Hero.MainHero && legal != Hero.MainHero)
                InformationManager.ShowInquiry(new InquiryData("Parecer do chanceler",request,true,true,"Encaminhar","Indeferir",Review,Refuse),true);
            else Review();
        }
        internal static void Weekly(Kingdom realm)
        {
            foreach (var fief in realm.Fiefs)
            {
                var claimant = fief.OwnerClan?.Leader;
                if (claimant == null || claimant == Hero.MainHero) continue;
                var title = TitleManager.Instance.GetTitle(fief.Settlement);
                if (Block(title,claimant) == null) { Request(title,claimant); return; }
            }
        }
    }
}
