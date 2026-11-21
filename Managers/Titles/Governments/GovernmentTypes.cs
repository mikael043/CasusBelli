using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace CasusBelli.Managers.Titles.Governments
{
    // Tipos de governo
    public class Government
    {
        [SaveableProperty(1)] public string StringId { get; private set; }
        [SaveableProperty(2)] public TextObject Name { get; private set; }
        [SaveableProperty(3)] public TextObject Description { get; private set; }
        [SaveableProperty(4)] public int Centralization { get; private set; }  // -100 a 100
        [SaveableProperty(5)] public int VassalLimit { get; private set; }
        [SaveableProperty(6)] public float TaxEfficiency { get; private set; }
        [SaveableProperty(7)] public float LevyEfficiency { get; private set; }
        [SaveableProperty(8)] public float InfluenceGain { get; private set; }

        public Government(string stringId, TextObject name, TextObject description,
            int centralization = 0, int vassalLimit = 10, float taxEfficiency = 1f,
            float levyEfficiency = 1f, float influenceGain = 1f)
        {
            StringId = stringId;
            Name = name;
            Description = description;
            Centralization = centralization;
            VassalLimit = vassalLimit;
            TaxEfficiency = taxEfficiency;
            LevyEfficiency = levyEfficiency;
            InfluenceGain = influenceGain;
        }

        public override bool Equals(object obj)
        {
            return obj is Government other && StringId == other.StringId;
        }

        public override int GetHashCode()
        {
            return StringId?.GetHashCode() ?? 0;
        }
    }

    // Tipos de sucessão
    public class Succession
    {
        [SaveableProperty(1)] public string StringId { get; private set; }
        [SaveableProperty(2)] public TextObject Name { get; private set; }
        [SaveableProperty(3)] public TextObject Description { get; private set; }
        [SaveableProperty(4)] public bool IsElective { get; private set; }
        [SaveableProperty(5)] public int CandidateLimit { get; private set; }

        public Succession(string stringId, TextObject name, TextObject description, bool isElective, int candidateLimit = 6)
        {
            StringId = stringId;
            Name = name;
            Description = description;
            IsElective = isElective;
            CandidateLimit = candidateLimit;
        }

        // Obtém candidatos à sucessão para um título
        public virtual HashSet<Hero> GetSuccessionCandidates(Hero currentLeader, FeudalTitle title)
        {
            var candidates = new HashSet<Hero>();

            if (title?.Contract == null || currentLeader == null)
                return candidates;

            var clan = currentLeader.Clan;
            if (clan == null)
                return candidates;

            if (IsElective)
            {
                // Sucessão eletiva: vassalos e família
                foreach (var hero in clan.Heroes)
                {
                    if (!hero.IsChild && hero.IsAlive && hero.Occupation == Occupation.Lord)
                        candidates.Add(hero);
                }

                // Adicionar vassalos do título
                if (title.Vassals != null)
                {
                    foreach (var vassalTitle in title.Vassals)
                    {
                        if (vassalTitle.deJure != null && !vassalTitle.deJure.IsChild && vassalTitle.deJure.IsAlive)
                            candidates.Add(vassalTitle.deJure);
                    }
                }
            }
            else
            {
                // Sucessão hereditária: herdeiros do clã
                foreach (var hero in clan.Heroes)
                {
                    if (!hero.IsChild && hero.IsAlive && hero.Occupation == Occupation.Lord && hero != currentLeader)
                        candidates.Add(hero);
                }
            }

            return candidates;
        }

        public override bool Equals(object obj)
        {
            return obj is Succession other && StringId == other.StringId;
        }

        public override int GetHashCode()
        {
            return StringId?.GetHashCode() ?? 0;
        }
    }

    // Tipos de herança
    public class Inheritance
    {
        [SaveableProperty(1)] public string StringId { get; private set; }
        [SaveableProperty(2)] public TextObject Name { get; private set; }
        [SaveableProperty(3)] public TextObject Description { get; private set; }
        [SaveableProperty(4)] public bool GenderEqual { get; private set; }
        [SaveableProperty(5)] public bool SeniorityBased { get; private set; }

        public Inheritance(string stringId, TextObject name, TextObject description, bool genderEqual, bool seniorityBased)
        {
            StringId = stringId;
            Name = name;
            Description = description;
            GenderEqual = genderEqual;
            SeniorityBased = seniorityBased;
        }

        public override bool Equals(object obj)
        {
            return obj is Inheritance other && StringId == other.StringId;
        }

        public override int GetHashCode()
        {
            return StringId?.GetHashCode() ?? 0;
        }
    }

    // Leis de gênero
    public class GenderLaw
    {
        [SaveableProperty(1)] public string StringId { get; private set; }
        [SaveableProperty(2)] public TextObject Name { get; private set; }
        [SaveableProperty(3)] public TextObject Description { get; private set; }
        [SaveableProperty(4)] public bool FemaleCanInherit { get; private set; }
        [SaveableProperty(5)] public bool FemaleCanRule { get; private set; }

        public GenderLaw(string stringId, TextObject name, TextObject description, bool femaleCanInherit, bool femaleCanRule)
        {
            StringId = stringId;
            Name = name;
            Description = description;
            FemaleCanInherit = femaleCanInherit;
            FemaleCanRule = femaleCanRule;
        }

        public override bool Equals(object obj)
        {
            return obj is GenderLaw other && StringId == other.StringId;
        }

        public override int GetHashCode()
        {
            return StringId?.GetHashCode() ?? 0;
        }
    }

    // Leis do domínio (demesne laws)
    public class DemesneLaw
    {
        public enum DemesneLawType
        {
            Taxation,
            Levies,
            Justice,
            Infrastructure,
            Trade,
            Religion,
            Education,
            Fortifications
        }

        [SaveableProperty(1)] public string StringId { get; private set; }
        [SaveableProperty(2)] public TextObject Name { get; private set; }
        [SaveableProperty(3)] public TextObject Description { get; private set; }
        [SaveableProperty(4)] public DemesneLawType LawType { get; private set; }
        [SaveableProperty(5)] public int Level { get; private set; }  // 1-5
        [SaveableProperty(6)] public float TaxModifier { get; private set; }
        [SaveableProperty(7)] public float LevyModifier { get; private set; }
        [SaveableProperty(8)] public float InfluenceCost { get; private set; }
        [SaveableProperty(9)] public CampaignTime IssueDate { get; private set; }

        public DemesneLaw(string stringId, TextObject name, TextObject description, DemesneLawType type, int level,
            float taxModifier = 0f, float levyModifier = 0f, float influenceCost = 50f)
        {
            StringId = stringId;
            Name = name;
            Description = description;
            LawType = type;
            Level = level;
            TaxModifier = taxModifier;
            LevyModifier = levyModifier;
            InfluenceCost = influenceCost;
            IssueDate = CampaignTime.Zero;
        }

        public DemesneLaw GetCopy()
        {
            var copy = new DemesneLaw(StringId, Name, Description, LawType, Level, TaxModifier, LevyModifier, InfluenceCost);
            copy.SetIssueDate(CampaignTime.Now);
            return copy;
        }

        public void SetIssueDate(CampaignTime date)
        {
            IssueDate = date;
        }

        public override bool Equals(object obj)
        {
            return obj is DemesneLaw other && StringId == other.StringId;
        }

        public override int GetHashCode()
        {
            return StringId?.GetHashCode() ?? 0;
        }
    }

    // Deveres contratuais
    public class ContractDuty
    {
        [SaveableProperty(1)] public string StringId { get; private set; }
        [SaveableProperty(2)] public TextObject Name { get; private set; }
        [SaveableProperty(3)] public TextObject Description { get; private set; }
        [SaveableProperty(4)] public int SeasonsDelay { get; private set; }
        [SaveableProperty(5)] public float InfluenceReward { get; private set; }
        [SaveableProperty(6)] public float RelationReward { get; private set; }

        public ContractDuty(string stringId, TextObject name, TextObject description, int seasonsDelay, float influenceReward = 10f, float relationReward = 5f)
        {
            StringId = stringId;
            Name = name;
            Description = description;
            SeasonsDelay = seasonsDelay;
            InfluenceReward = influenceReward;
            RelationReward = relationReward;
        }

        public override bool Equals(object obj)
        {
            return obj is ContractDuty other && StringId == other.StringId;
        }

        public override int GetHashCode()
        {
            return StringId?.GetHashCode() ?? 0;
        }
    }

    // Governo padrão
    public class DefaultGovernments
    {
        private static DefaultGovernments _instance;
        public static DefaultGovernments Instance => _instance ??= new DefaultGovernments();

        public Government Feudal { get; private set; }
        public Government Republic { get; private set; }
        public Government Clan { get; private set; }
        public Government Theocracy { get; private set; }
        public Government Imperial { get; private set; }

        public Dictionary<string, Government> All { get; private set; }

        private DefaultGovernments()
        {
            Feudal = new Government("feudal", new TextObject("{=BKGovFeudal}Feudal"),
                new TextObject("{=BKGovFeudalDesc}Traditional feudal hierarchy with hereditary titles."),
                0, 10, 1f, 1f, 1f);

            Republic = new Government("republic", new TextObject("{=BKGovRepublic}Republic"),
                new TextObject("{=BKGovRepublicDesc}Elected leadership with merchant influence."),
                -20, 15, 1.2f, 0.8f, 1.1f);

            Clan = new Government("clan", new TextObject("{=BKGovClan}Clan"),
                new TextObject("{=BKGovClanDesc}Tribal organization based on kinship."),
                -30, 20, 0.8f, 1.3f, 0.9f);

            Theocracy = new Government("theocracy", new TextObject("{=BKGovTheocracy}Theocracy"),
                new TextObject("{=BKGovTheocracyDesc}Religious leadership with divine mandate."),
                20, 8, 1.1f, 1f, 1.2f);

            Imperial = new Government("imperial", new TextObject("{=BKGovImperial}Imperial"),
                new TextObject("{=BKGovImperialDesc}Centralized imperial bureaucracy."),
                50, 5, 1.3f, 1.2f, 1.5f);

            All = new Dictionary<string, Government>
            {
                { Feudal.StringId, Feudal },
                { Republic.StringId, Republic },
                { Clan.StringId, Clan },
                { Theocracy.StringId, Theocracy },
                { Imperial.StringId, Imperial }
            };
        }

        public Government GetById(string id)
        {
            All.TryGetValue(id, out var gov);
            return gov ?? Feudal;
        }
    }

    // Sucessões padrão
    public class DefaultSuccessions
    {
        private static DefaultSuccessions _instance;
        public static DefaultSuccessions Instance => _instance ??= new DefaultSuccessions();

        public Succession Hereditary { get; private set; }
        public Succession FeudalElective { get; private set; }
        public Succession Republic { get; private set; }
        public Succession Seniority { get; private set; }
        public Succession Primogeniture { get; private set; }
        public Succession Ultimogeniture { get; private set; }

        public Dictionary<string, Succession> All { get; private set; }

        private DefaultSuccessions()
        {
            Hereditary = new Succession("hereditary", new TextObject("{=BKSuccHereditary}Hereditary"),
                new TextObject("{=BKSuccHereditaryDesc}Title passes to the ruler's heir."), false);

            FeudalElective = new Succession("feudal_elective", new TextObject("{=BKSuccFeudalElective}Feudal Elective"),
                new TextObject("{=BKSuccFeudalElectiveDesc}Vassals elect the next ruler."), true);

            Republic = new Succession("republic", new TextObject("{=BKSuccRepublic}Republican"),
                new TextObject("{=BKSuccRepublicDesc}Citizens elect the leader."), true);

            Seniority = new Succession("seniority", new TextObject("{=BKSuccSeniority}Seniority"),
                new TextObject("{=BKSuccSeniorityDesc}Oldest eligible heir inherits."), false);

            Primogeniture = new Succession("primogeniture", new TextObject("{=BKSuccPrimogeniture}Primogeniture"),
                new TextObject("{=BKSuccPrimogenitureDesc}Firstborn child inherits."), false);

            Ultimogeniture = new Succession("ultimogeniture", new TextObject("{=BKSuccUltimogeniture}Ultimogeniture"),
                new TextObject("{=BKSuccUltimogenitureDesc}Youngest child inherits."), false);

            All = new Dictionary<string, Succession>
            {
                { Hereditary.StringId, Hereditary },
                { FeudalElective.StringId, FeudalElective },
                { Republic.StringId, Republic },
                { Seniority.StringId, Seniority },
                { Primogeniture.StringId, Primogeniture },
                { Ultimogeniture.StringId, Ultimogeniture }
            };
        }

        public Succession GetById(string id)
        {
            All.TryGetValue(id, out var succ);
            return succ ?? Hereditary;
        }
    }

    // Heranças padrão
    public class DefaultInheritances
    {
        private static DefaultInheritances _instance;
        public static DefaultInheritances Instance => _instance ??= new DefaultInheritances();

        public Inheritance Primogeniture { get; private set; }
        public Inheritance Ultimogeniture { get; private set; }
        public Inheritance Seniority { get; private set; }
        public Inheritance Gavelkind { get; private set; }
        public Inheritance Appointment { get; private set; }

        public Dictionary<string, Inheritance> All { get; private set; }

        private DefaultInheritances()
        {
            Primogeniture = new Inheritance("primogeniture", new TextObject("{=BKInhPrimogeniture}Primogeniture"),
                new TextObject("{=BKInhPrimogenitureDesc}Firstborn inherits everything."), true, false);

            Ultimogeniture = new Inheritance("ultimogeniture", new TextObject("{=BKInhUltimogeniture}Ultimogeniture"),
                new TextObject("{=BKInhUltimogenitureDesc}Youngest inherits everything."), true, false);

            Seniority = new Inheritance("seniority", new TextObject("{=BKInhSeniority}Seniority"),
                new TextObject("{=BKInhSeniorityDesc}Oldest inherits."), true, true);

            Gavelkind = new Inheritance("gavelkind", new TextObject("{=BKInhGavelkind}Gavelkind"),
                new TextObject("{=BKInhGavelkindDesc}Title divided among all children."), true, false);

            Appointment = new Inheritance("appointment", new TextObject("{=BKInhAppointment}Appointment"),
                new TextObject("{=BKInhAppointmentDesc}Ruler appoints heir."), true, false);

            All = new Dictionary<string, Inheritance>
            {
                { Primogeniture.StringId, Primogeniture },
                { Ultimogeniture.StringId, Ultimogeniture },
                { Seniority.StringId, Seniority },
                { Gavelkind.StringId, Gavelkind },
                { Appointment.StringId, Appointment }
            };
        }

        public Inheritance GetById(string id)
        {
            All.TryGetValue(id, out var inh);
            return inh ?? Primogeniture;
        }
    }

    // Leis de gênero padrão
    public class DefaultGenderLaws
    {
        private static DefaultGenderLaws _instance;
        public static DefaultGenderLaws Instance => _instance ??= new DefaultGenderLaws();

        public GenderLaw Agnatic { get; private set; }
        public GenderLaw AgnaticCognatic { get; private set; }
        public GenderLaw Cognatic { get; private set; }
        public GenderLaw AbsoluteCognatic { get; private set; }

        public Dictionary<string, GenderLaw> All { get; private set; }

        private DefaultGenderLaws()
        {
            Agnatic = new GenderLaw("agnatic", new TextObject("{=BKGenAgnatic}Agnatic"),
                new TextObject("{=BKGenAgnaticDesc}Only males can inherit and rule."), false, false);

            AgnaticCognatic = new GenderLaw("agnatic_cognatic", new TextObject("{=BKGenAgnaticCognatic}Agnatic-Cognatic"),
                new TextObject("{=BKGenAgnaticCognaticDesc}Males inherit first, females if no males."), true, false);

            Cognatic = new GenderLaw("cognatic", new TextObject("{=BKGenCognatic}Cognatic"),
                new TextObject("{=BKGenCognaticDesc}Both genders inherit equally."), true, true);

            AbsoluteCognatic = new GenderLaw("absolute_cognatic", new TextObject("{=BKGenAbsoluteCognatic}Absolute Cognatic"),
                new TextObject("{=BKGenAbsoluteCognaticDesc}Females preferred in inheritance."), true, true);

            All = new Dictionary<string, GenderLaw>
            {
                { Agnatic.StringId, Agnatic },
                { AgnaticCognatic.StringId, AgnaticCognatic },
                { Cognatic.StringId, Cognatic },
                { AbsoluteCognatic.StringId, AbsoluteCognatic }
            };
        }

        public GenderLaw GetById(string id)
        {
            All.TryGetValue(id, out var law);
            return law ?? Agnatic;
        }
    }

    // Leis do domínio padrão
    public class DefaultDemesneLaws
    {
        private static DefaultDemesneLaws _instance;
        public static DefaultDemesneLaws Instance => _instance ??= new DefaultDemesneLaws();

        public DemesneLaw LowTaxes { get; private set; }
        public DemesneLaw MediumTaxes { get; private set; }
        public DemesneLaw HighTaxes { get; private set; }
        public DemesneLaw OppressiveTaxes { get; private set; }

        public DemesneLaw LowLevies { get; private set; }
        public DemesneLaw MediumLevies { get; private set; }
        public DemesneLaw HighLevies { get; private set; }
        public DemesneLaw MaximumLevies { get; private set; }

        public DemesneLaw LocalJustice { get; private set; }
        public DemesneLaw RoyalJustice { get; private set; }
        public DemesneLaw HarshJustice { get; private set; }

        public DemesneLaw BasicInfrastructure { get; private set; }
        public DemesneLaw ImprovedInfrastructure { get; private set; }
        public DemesneLaw AdvancedInfrastructure { get; private set; }

        public DemesneLaw FreeTrade { get; private set; }
        public DemesneLaw RegulatedTrade { get; private set; }
        public DemesneLaw RestrictedTrade { get; private set; }

        public Dictionary<string, DemesneLaw> All { get; private set; }

        private DefaultDemesneLaws()
        {
            LowTaxes = new DemesneLaw("low_taxes", new TextObject("{=BKLawLowTaxes}Low Taxes"),
                new TextObject("{=BKLawLowTaxesDesc}Light taxation, happy populace."),
                DemesneLaw.DemesneLawType.Taxation, 1, -0.2f, 0f, 20f);

            MediumTaxes = new DemesneLaw("medium_taxes", new TextObject("{=BKLawMediumTaxes}Medium Taxes"),
                new TextObject("{=BKLawMediumTaxesDesc}Balanced taxation."),
                DemesneLaw.DemesneLawType.Taxation, 2, 0f, 0f, 50f);

            HighTaxes = new DemesneLaw("high_taxes", new TextObject("{=BKLawHighTaxes}High Taxes"),
                new TextObject("{=BKLawHighTaxesDesc}Heavy taxation, discontent populace."),
                DemesneLaw.DemesneLawType.Taxation, 3, 0.3f, 0f, 80f);

            OppressiveTaxes = new DemesneLaw("oppressive_taxes", new TextObject("{=BKLawOppressiveTaxes}Oppressive Taxes"),
                new TextObject("{=BKLawOppressiveTaxesDesc}Crushing taxation, rebellion risk."),
                DemesneLaw.DemesneLawType.Taxation, 4, 0.6f, 0f, 120f);

            LowLevies = new DemesneLaw("low_levies", new TextObject("{=BKLawLowLevies}Low Levies"),
                new TextObject("{=BKLawLowLeviesDesc}Few troops required from vassals."),
                DemesneLaw.DemesneLawType.Levies, 1, 0f, -0.3f, 20f);

            MediumLevies = new DemesneLaw("medium_levies", new TextObject("{=BKLawMediumLevies}Medium Levies"),
                new TextObject("{=BKLawMediumLeviesDesc}Standard levy obligations."),
                DemesneLaw.DemesneLawType.Levies, 2, 0f, 0f, 50f);

            HighLevies = new DemesneLaw("high_levies", new TextObject("{=BKLawHighLevies}High Levies"),
                new TextObject("{=BKLawHighLeviesDesc}Heavy military obligations."),
                DemesneLaw.DemesneLawType.Levies, 3, 0f, 0.3f, 80f);

            MaximumLevies = new DemesneLaw("maximum_levies", new TextObject("{=BKLawMaxLevies}Maximum Levies"),
                new TextObject("{=BKLawMaxLeviesDesc}All available manpower mobilized."),
                DemesneLaw.DemesneLawType.Levies, 4, 0f, 0.6f, 120f);

            LocalJustice = new DemesneLaw("local_justice", new TextObject("{=BKLawLocalJustice}Local Justice"),
                new TextObject("{=BKLawLocalJusticeDesc}Vassals handle their own justice."),
                DemesneLaw.DemesneLawType.Justice, 1, 0f, 0f, 20f);

            RoyalJustice = new DemesneLaw("royal_justice", new TextObject("{=BKLawRoyalJustice}Royal Justice"),
                new TextObject("{=BKLawRoyalJusticeDesc}Liege's courts have authority."),
                DemesneLaw.DemesneLawType.Justice, 2, 0f, 0f, 50f);

            HarshJustice = new DemesneLaw("harsh_justice", new TextObject("{=BKLawHarshJustice}Harsh Justice"),
                new TextObject("{=BKLawHarshJusticeDesc}Swift and severe punishment."),
                DemesneLaw.DemesneLawType.Justice, 3, 0f, 0f, 80f);

            BasicInfrastructure = new DemesneLaw("basic_infrastructure", new TextObject("{=BKLawBasicInfra}Basic Infrastructure"),
                new TextObject("{=BKLawBasicInfraDesc}Minimal public works."),
                DemesneLaw.DemesneLawType.Infrastructure, 1, 0f, 0f, 20f);

            ImprovedInfrastructure = new DemesneLaw("improved_infrastructure", new TextObject("{=BKLawImprovedInfra}Improved Infrastructure"),
                new TextObject("{=BKLawImprovedInfraDesc}Good roads and facilities."),
                DemesneLaw.DemesneLawType.Infrastructure, 2, 0.1f, 0f, 50f);

            AdvancedInfrastructure = new DemesneLaw("advanced_infrastructure", new TextObject("{=BKLawAdvancedInfra}Advanced Infrastructure"),
                new TextObject("{=BKLawAdvancedInfraDesc}Excellent infrastructure."),
                DemesneLaw.DemesneLawType.Infrastructure, 3, 0.2f, 0f, 80f);

            FreeTrade = new DemesneLaw("free_trade", new TextObject("{=BKLawFreeTrade}Free Trade"),
                new TextObject("{=BKLawFreeTradeDesc}No trade restrictions."),
                DemesneLaw.DemesneLawType.Trade, 1, 0.2f, 0f, 20f);

            RegulatedTrade = new DemesneLaw("regulated_trade", new TextObject("{=BKLawRegulatedTrade}Regulated Trade"),
                new TextObject("{=BKLawRegulatedTradeDesc}Moderate trade regulations."),
                DemesneLaw.DemesneLawType.Trade, 2, 0f, 0f, 50f);

            RestrictedTrade = new DemesneLaw("restricted_trade", new TextObject("{=BKLawRestrictedTrade}Restricted Trade"),
                new TextObject("{=BKLawRestrictedTradeDesc}Heavy trade controls."),
                DemesneLaw.DemesneLawType.Trade, 3, -0.2f, 0f, 80f);

            All = new Dictionary<string, DemesneLaw>
            {
                { LowTaxes.StringId, LowTaxes },
                { MediumTaxes.StringId, MediumTaxes },
                { HighTaxes.StringId, HighTaxes },
                { OppressiveTaxes.StringId, OppressiveTaxes },
                { LowLevies.StringId, LowLevies },
                { MediumLevies.StringId, MediumLevies },
                { HighLevies.StringId, HighLevies },
                { MaximumLevies.StringId, MaximumLevies },
                { LocalJustice.StringId, LocalJustice },
                { RoyalJustice.StringId, RoyalJustice },
                { HarshJustice.StringId, HarshJustice },
                { BasicInfrastructure.StringId, BasicInfrastructure },
                { ImprovedInfrastructure.StringId, ImprovedInfrastructure },
                { AdvancedInfrastructure.StringId, AdvancedInfrastructure },
                { FreeTrade.StringId, FreeTrade },
                { RegulatedTrade.StringId, RegulatedTrade },
                { RestrictedTrade.StringId, RestrictedTrade }
            };
        }

        // Obtém leis adequadas para um título baseado no tipo
        public List<DemesneLaw> GetAdequateLaws(FeudalTitle title)
        {
            var laws = new List<DemesneLaw>();

            if (title == null) return laws;

            // Impérios e Reinos começam com leis médias
            if (title.TitleType <= TitleType.Kingdom)
            {
                laws.Add(MediumTaxes);
                laws.Add(MediumLevies);
                laws.Add(RoyalJustice);
                laws.Add(ImprovedInfrastructure);
                laws.Add(RegulatedTrade);
            }
            // Ducados e Condados
            else if (title.TitleType <= TitleType.County)
            {
                laws.Add(MediumTaxes);
                laws.Add(MediumLevies);
                laws.Add(LocalJustice);
                laws.Add(BasicInfrastructure);
                laws.Add(RegulatedTrade);
            }
            // Baronias e Senhorios
            else
            {
                laws.Add(LowTaxes);
                laws.Add(LowLevies);
                laws.Add(LocalJustice);
                laws.Add(BasicInfrastructure);
                laws.Add(FreeTrade);
            }

            return laws;
        }

        public DemesneLaw GetById(string id)
        {
            All.TryGetValue(id, out var law);
            return law;
        }
    }

    // Deveres padrão
    public class DefaultContractDuties
    {
        private static DefaultContractDuties _instance;
        public static DefaultContractDuties Instance => _instance ??= new DefaultContractDuties();

        public ContractDuty AttendCourt { get; private set; }
        public ContractDuty ProvideCounsel { get; private set; }
        public ContractDuty HostLiege { get; private set; }
        public ContractDuty MilitaryService { get; private set; }
        public ContractDuty GuardDuty { get; private set; }

        public Dictionary<string, ContractDuty> All { get; private set; }

        private DefaultContractDuties()
        {
            AttendCourt = new ContractDuty("attend_court", new TextObject("{=BKDutyAttendCourt}Attend Court"),
                new TextObject("{=BKDutyAttendCourtDesc}Attend the liege's court regularly."), 4, 10f, 5f);

            ProvideCounsel = new ContractDuty("provide_counsel", new TextObject("{=BKDutyProvideCounsel}Provide Counsel"),
                new TextObject("{=BKDutyProvideCounselDesc}Offer advice to the liege."), 2, 15f, 10f);

            HostLiege = new ContractDuty("host_liege", new TextObject("{=BKDutyHostLiege}Host Liege"),
                new TextObject("{=BKDutyHostLiegeDesc}Host the liege when visiting."), 8, 20f, 15f);

            MilitaryService = new ContractDuty("military_service", new TextObject("{=BKDutyMilitary}Military Service"),
                new TextObject("{=BKDutyMilitaryDesc}Serve in the liege's armies."), 1, 25f, 10f);

            GuardDuty = new ContractDuty("guard_duty", new TextObject("{=BKDutyGuard}Guard Duty"),
                new TextObject("{=BKDutyGuardDesc}Guard the liege's holdings."), 2, 10f, 5f);

            All = new Dictionary<string, ContractDuty>
            {
                { AttendCourt.StringId, AttendCourt },
                { ProvideCounsel.StringId, ProvideCounsel },
                { HostLiege.StringId, HostLiege },
                { MilitaryService.StringId, MilitaryService },
                { GuardDuty.StringId, GuardDuty }
            };
        }

        public ContractDuty GetById(string id)
        {
            All.TryGetValue(id, out var duty);
            return duty;
        }
    }
}