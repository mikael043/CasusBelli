using System;
using System.Linq;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli
{
    internal static class RealmDiplomacy
    {
        private static string Pair(string a,string b) => string.CompareOrdinal(a,b) < 0 ? a + "|" + b : b + "|" + a;
        internal static int PactDays(string a,string b) => Math.Max(0,(RealmPolitics.Current?.Number("pact|" + Pair(a,b),0) ?? 0) - (int)CampaignTime.Now.ToDays);
        internal static void Breach(Kingdom aggressor, Kingdom other)
        {
            if (aggressor == null || other == null || PactDays(aggressor.StringId,other.StringId) == 0) return;
            RealmPolitics.Current.Write("pact|" + Pair(aggressor.StringId,other.StringId),null);
            RealmPolitics.Conduct(aggressor.Leader,aggressor,0,18);
            RealmPolitics.Shift(aggressor,-8);
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(aggressor.Leader,other.Leader,-15,false);
        }
        internal static void CallDefenders(Kingdom attacker,Kingdom defender)
        {
            if (attacker == null || defender == null || RealmPolitics.Current == null) return;
            var treaties = Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>();
            if (treaties == null) return;
            var main = Campaign.Current.GetCampaignBehavior<CasusBelliCampaignBehavior>();
            foreach (var ally in Kingdom.All.Where(k => k != attacker && k != defender && !k.IsEliminated && k.Leader != null).ToList())
            {
                if (RealmPolitics.Current.Number("defense|" + Pair(ally.StringId,defender.StringId),0) <= CampaignTime.Now.ToDays ||
                    !treaties.IsAllyWithKingdom(ally,defender) || ally.IsAtWarWith(attacker) ||
                    treaties.IsAllyWithKingdom(ally,attacker) || main.IsTruceActive(ally.StringId,attacker.StringId)) continue;
                if (ally.Leader == Hero.MainHero) treaties.OnCallToWarAgreementProposedToPlayerKingdom(defender,attacker);
                else DeclareWarAction.ApplyByCallToWarAgreement(ally,attacker);
            }
        }
        internal static string Block(Kingdom realm,Kingdom other,bool alliance)
        {
            if (realm?.Leader == null || other?.Leader == null || realm == other || realm.IsEliminated || other.IsEliminated || realm.IsAtWarWith(other)) return "Os reinos devem estar em paz.";
            if (CouncilDuties.Officer(realm,CouncilOffice.Chancellor) == null) return "É preciso nomear um chanceler.";
            if (realm.KingdomBudgetWallet < 100) return "A missão custa 100 denários do tesouro.";
            var politics = RealmPolitics.Current;
            if (politics.Number("envoy|" + Pair(realm.StringId,other.StringId),0) > CampaignTime.Now.ToDays) return "A missão anterior é recente.";
            if (!alliance && PactDays(realm.StringId,other.StringId) > 0) return "Já existe um pacto em vigor.";
            if (alliance && !Campaign.Current.Models.AllianceModel.CanMakeAlliance(realm,other,realm,out var reason,true)) return reason?.ToString() ?? "Aliança indisponível.";
            return null;
        }
        private static float Merit(Kingdom realm,Kingdom other) => realm.Leader.GetRelation(other.Leader) +
            (CouncilDuties.Officer(realm,CouncilOffice.Chancellor)?.GetSkillValue(DefaultSkills.Charm) ?? 0) / 8f -
            RealmPolitics.Controversy(realm.Leader) / 2f + (realm.Culture == other.Culture ? 8 : 0);
        internal static void Offer(Kingdom realm,Kingdom other,bool alliance)
        {
            if (Block(realm,other,alliance) != null) return;
            realm.KingdomBudgetWallet -= 100;
            var politics = RealmPolitics.Current;
            politics.Write("envoy|" + Pair(realm.StringId,other.StringId),((int)CampaignTime.Now.ToDays+28).ToString());
            string name = alliance ? "aliança" : "pacto de não agressão por um ano";
            void Answer(bool yes)
            {
                if (realm.IsAtWarWith(other) || realm.IsEliminated || other.IsEliminated) return;
                if (yes && alliance && Campaign.Current.Models.AllianceModel.CanMakeAlliance(realm,other,realm,out _,false))
                {
                    var treaties = Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>();
                    treaties.StartAlliance(realm,other);
                    politics.Write("defense|" + Pair(realm.StringId,other.StringId),((int)Math.Ceiling(treaties.GetAllianceEndDate(realm,other).ToDays)).ToString());
                }
                else if (yes && !alliance)
                    politics.Write("pact|" + Pair(realm.StringId,other.StringId),((int)Math.Ceiling(CampaignTime.YearsFromNow(1).ToDays)).ToString());
                else yes = false;
                string result = $"{other.Name} {(yes ? "aceitou" : "recusou")} a proposta de {name} de {realm.Name}.";
                politics.Write("foreign|" + realm.StringId,result);
                politics.Write("foreign|" + other.StringId,result);
                if (Clan.PlayerClan?.Kingdom == realm || Clan.PlayerClan?.Kingdom == other)
                    InformationManager.DisplayMessage(new InformationMessage(result));
            }
            if (other.Leader == Hero.MainHero) InformationManager.ShowInquiry(new InquiryData("Proposta diplomática",
                $"{realm.Name} propõe {name}.",true,true,"Aceitar","Recusar",() => Answer(true),() => Answer(false)),true);
            else Answer(Merit(realm,other) >= (alliance ? 45 : 22));
        }
        internal static void Weekly(Kingdom realm)
        {
            if (realm.Leader == Hero.MainHero || CouncilDuties.Officer(realm,CouncilOffice.Chancellor) == null || MBRandom.RandomFloat >= .15f) return;
            var other = Kingdom.All.Where(k => k.Leader != null && !k.IsEliminated && k != realm && !realm.IsAtWarWith(k))
                .OrderByDescending(k => realm.Leader.GetRelation(k.Leader)).FirstOrDefault(k =>
                !Campaign.Current.GetCampaignBehavior<CasusBelliCampaignBehavior>().HasUsableClaimAgainst(realm,k) && Merit(realm,k) >= 22 &&
                (Block(realm,k,false) == null || Block(realm,k,true) == null));
            if (other != null) Offer(realm,other,Block(realm,other,false) != null);
        }
    }
}
