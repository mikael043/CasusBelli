using System.Collections.Generic;
using CasusBelli.Disputes;
using CasusBelli.Managers.Titles.Governments;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace CasusBelli.Managers.Titles
{
    public class TitleSaveDefiner : SaveableTypeDefiner
    {
        public TitleSaveDefiner() : base(671385000) { }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(TitleManager), 1);
            AddClassDefinition(typeof(FeudalTitle), 2);
            AddClassDefinition(typeof(FeudalContract), 3);
            AddClassDefinition(typeof(Government), 4);
            AddClassDefinition(typeof(Succession), 5);
            AddClassDefinition(typeof(Inheritance), 6);
            AddClassDefinition(typeof(GenderLaw), 7);
            AddClassDefinition(typeof(ContractAspect), 8);
            AddClassDefinition(typeof(DemesneLaw), 9);
            AddClassDefinition(typeof(ContractDuty), 10);
            AddClassDefinition(typeof(TitleDispute), 11);
            AddClassDefinition(typeof(PeerageDecision), 13);
            AddClassDefinition(typeof(PeerVote), 14);
            AddClassDefinition(typeof(ArmySummonsQuest), 15);
            AddClassDefinition(typeof(SupportOffer), 12);
        }

        protected override void DefineEnumTypes()
        {
            AddEnumDefinition(typeof(TitleType), 21);
            AddEnumDefinition(typeof(ClaimType), 22);
            AddEnumDefinition(typeof(DemesneLaw.DemesneLawType), 23);
            AddEnumDefinition(typeof(ContractAspectType), 24);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(Dictionary<Hero, float>));
            ConstructContainerDefinition(typeof(List<FeudalTitle>));
            ConstructContainerDefinition(typeof(Dictionary<FeudalTitle, Kingdom>));
            ConstructContainerDefinition(typeof(Dictionary<Hero, ClaimType>));
            ConstructContainerDefinition(typeof(Dictionary<FeudalTitle, float>));
            ConstructContainerDefinition(typeof(Dictionary<ContractDuty, CampaignTime>));
            ConstructContainerDefinition(typeof(List<ContractAspect>));
            ConstructContainerDefinition(typeof(List<DemesneLaw>));
            ConstructContainerDefinition(typeof(List<ContractDuty>));
            ConstructContainerDefinition(typeof(List<TitleDispute>));
            ConstructContainerDefinition(typeof(List<SupportOffer>));
        }
    }
}
