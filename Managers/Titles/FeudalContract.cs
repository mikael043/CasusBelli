using System.Collections.Generic;
using CasusBelli.Managers.Titles.Governments;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace CasusBelli.Managers.Titles
{
    // Contrato feudal que define as leis de um título
    // Inclui governo, sucessão, herança, lei de gênero e aspectos contratuais
    public class FeudalContract
    {
        [SaveableProperty(1)] public Government Government { get; private set; }
        [SaveableProperty(2)] public Succession Succession { get; private set; }
        [SaveableProperty(3)] public Inheritance Inheritance { get; private set; }
        [SaveableProperty(4)] public GenderLaw GenderLaw { get; private set; }
        [SaveableProperty(5)] public List<ContractAspect> ContractAspects { get; private set; }
        [SaveableProperty(6)] public List<DemesneLaw> DemesneLaws { get; private set; }
        [SaveableProperty(7)] public List<ContractDuty> Duties { get; private set; }

        public FeudalContract(Government government, Succession succession, Inheritance inheritance, GenderLaw genderLaw)
        {
            Government = government;
            Succession = succession;
            Inheritance = inheritance;
            GenderLaw = genderLaw;
            ContractAspects = new List<ContractAspect>();
            DemesneLaws = new List<DemesneLaw>();
            Duties = new List<ContractDuty>();
        }

        // Inicialização pós-carregamento
        public void PostInitialize(Kingdom kingdom, CultureObject culture)
        {
            // Aplicar leis padrão se não houver
            if (DemesneLaws == null || DemesneLaws.Count == 0)
            {
                // Precisamos de um FeudalTitle para isso, mas não temos aqui
                // As leis serão aplicadas quando o título for criado
            }
        }

        // Define as leis do domínio
        public void SetLaws(List<DemesneLaw> laws)
        {
            DemesneLaws = laws ?? new List<DemesneLaw>();
        }

        // Promulga uma nova lei
        public void EnactLaw(DemesneLaw law)
        {
            if (law == null) return;

            // Remove lei do mesmo tipo se existir
            DemesneLaws.RemoveAll(l => l.LawType == law.LawType);
            DemesneLaws.Add(law);
        }

        // Muda o governo
        public void ChangeGovernment(Government government)
        {
            if (government != null)
                Government = government;
        }

        // Muda a sucessão
        public void ChangeSuccession(Succession succession)
        {
            if (succession != null)
                Succession = succession;
        }

        // Muda a herança
        public void ChangeInheritance(Inheritance inheritance)
        {
            if (inheritance != null)
                Inheritance = inheritance;
        }

        // Muda a lei de gênero
        public void ChangeGenderLaw(GenderLaw genderLaw)
        {
            if (genderLaw != null)
                GenderLaw = genderLaw;
        }

        // Adiciona ou substitui um aspecto contratual
        public void AddAspect(ContractAspect aspect)
        {
            if (aspect == null) return;

            ContractAspects ??= new List<ContractAspect>();
            ContractAspects.RemoveAll(a => a.AspectType == aspect.AspectType);
            ContractAspects.Add(aspect);
        }

        // Verifica se tem um aspecto específico
        public bool HasAspect(ContractAspectType type)
        {
            return ContractAspects?.Exists(a => a.AspectType == type) ?? false;
        }

        // Obtém um aspecto específico
        public ContractAspect GetAspect(ContractAspectType type)
        {
            return ContractAspects?.Find(a => a.AspectType == type);
        }
    }

    // Tipos de aspectos contratuais
    public enum ContractAspectType
    {
        Conquest,     // Direito de conquista
        Taxes,        // Tributação
        Enfeoffment,  // Enfeoffment (direito de conceder títulos)
        Military,     // Obrigações militares
        Judicial      // Jurisdição judicial
    }

    // Aspecto contratual
    public class ContractAspect
    {
        [SaveableProperty(1)] public ContractAspectType AspectType { get; private set; }
        [SaveableProperty(2)] public TextObject Name { get; private set; }
        [SaveableProperty(3)] public TextObject Description { get; private set; }
        [SaveableProperty(4)] public float InfluenceCost { get; private set; }

        public ContractAspect(ContractAspectType type, TextObject name, TextObject description, float influenceCost = 0f)
        {
            AspectType = type;
            Name = name;
            Description = description;
            InfluenceCost = influenceCost;
        }
    }

    // Aspectos contratuais padrão
    public class DefaultContractAspects
    {
        private static DefaultContractAspects _instance;
        public static DefaultContractAspects Instance => _instance ??= new DefaultContractAspects();

        public ContractAspect Conquest { get; private set; }
        public ContractAspect Taxes { get; private set; }
        public ContractAspect Enfeoffment { get; private set; }
        public ContractAspect Military { get; private set; }
        public ContractAspect Judicial { get; private set; }

        private DefaultContractAspects()
        {
            Conquest = new ContractAspect(ContractAspectType.Conquest,
                new TextObject("{=BKConquest}Right of Conquest"),
                new TextObject("{=BKConquestDesc}Allows the title holder to claim neighboring territories by force."),
                100f);

            Taxes = new ContractAspect(ContractAspectType.Taxes,
                new TextObject("{=BKTaxes}Right of Taxation"),
                new TextObject("{=BKTaxesDesc}Allows the title holder to collect taxes from vassals and settlements."),
                50f);

            Enfeoffment = new ContractAspect(ContractAspectType.Enfeoffment,
                new TextObject("{=BKEnfeoffment}Right of Enfeoffment"),
                new TextObject("{=BKEnfeoffmentDesc}Allows the title holder to grant titles to vassals."),
                75f);

            Military = new ContractAspect(ContractAspectType.Military,
                new TextObject("{=BKMilitary}Military Obligation"),
                new TextObject("{=BKMilitaryDesc}Vassals must provide military service to their liege."),
                50f);

            Judicial = new ContractAspect(ContractAspectType.Judicial,
                new TextObject("{=BKJudicial}Judicial Authority"),
                new TextObject("{=BKJudicialDesc}The title holder has judicial authority over vassals."),
                50f);
        }

        public List<ContractAspect> GetAll()
        {
            return new List<ContractAspect> { Conquest, Taxes, Enfeoffment, Military, Judicial };
        }
    }
}