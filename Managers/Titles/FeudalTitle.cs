using System;
using System.Collections.Generic;
using System.Linq;
using CasusBelli.Managers.Titles.Governments;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace CasusBelli.Managers.Titles
{
    // Título feudal principal - equivalente ao FeudalTitle do BannerKings
    public class FeudalTitle
    {
        [SaveableProperty(1)] public TitleType TitleType { get; private set; }
        [SaveableProperty(2)] public Settlement Fief { get; private set; }
        [SaveableProperty(3)] public List<FeudalTitle> Vassals { get; private set; }
        [SaveableProperty(4)] public Hero deJure { get; internal set; }
        [SaveableProperty(5)] public Hero deFacto { get; internal set; }
        [SaveableProperty(6)] public TextObject FullName { get; private set; }
        [SaveableProperty(7)] public TextObject ShortName { get; private set; }
        [SaveableProperty(8)] public float DueTax { get; set; }
        [SaveableProperty(9)] public FeudalTitle Sovereign { get; private set; }
        [SaveableProperty(10)] public FeudalContract Contract { get; private set; }
        [SaveableProperty(11)] private Dictionary<Hero, ClaimType> _claims { get; set; }
        [SaveableProperty(12)] private Dictionary<Hero, CampaignTime> _ongoingClaims { get; set; }
        [SaveableProperty(13)] private Dictionary<FeudalTitle, float> _deJureDrift { get; set; }
        [SaveableProperty(14)] public string StringId { get; private set; }
        [SaveableProperty(15)] public bool CustomName { get; private set; }
        [SaveableProperty(16)] public Dictionary<ContractDuty, CampaignTime> Duties { get; private set; }
        [SaveableProperty(17)] public bool Priority { get; internal set; }

        // Construtor principal
        public FeudalTitle(TitleType type, Settlement fief, List<FeudalTitle> vassals, Hero deJure, Hero deFacto,
            TextObject name, FeudalContract contract, string stringId = null, TextObject fullName = null)
        {
            TitleType = type;
            Fief = fief;
            Vassals = vassals ?? new List<FeudalTitle>();
            this.deJure = deJure;
            this.deFacto = deFacto;

            if (fullName != null)
            {
                FullName = fullName;
                CustomName = true;
            }
            else
            {
                FullName = TextHelper.FormatFullName(type, name);
            }

            ShortName = name;
            Contract = contract;
            DueTax = 0;
            _claims = new Dictionary<Hero, ClaimType>();
            _ongoingClaims = new Dictionary<Hero, CampaignTime>();
            _deJureDrift = new Dictionary<FeudalTitle, float>();
            StringId = stringId;
            Duties = new Dictionary<ContractDuty, CampaignTime>();
            Priority = false;

            // Propagar soberano para vassalos
            if (Vassals != null)
            {
                foreach (var vassal in Vassals)
                {
                    if (vassal != null)
                        vassal.SetSovereign(this);
                }
            }
        }

        // Inicialização pós-carregamento
        public void PostInitialize()
        {
            Duties ??= new Dictionary<ContractDuty, CampaignTime>();
            _claims ??= new Dictionary<Hero, ClaimType>();
            _ongoingClaims ??= new Dictionary<Hero, CampaignTime>();
            _deJureDrift ??= new Dictionary<FeudalTitle, float>();
            Vassals ??= new List<FeudalTitle>();

            if (deJure != null && Contract != null)
                Contract.PostInitialize(deJure.Clan?.Kingdom, deJure.Culture);

            if (!CustomName)
            {
                FullName = TextHelper.FormatFullName(TitleType, ShortName);
            }
        }

        // Cumprir um dever contratual
        public void FulfillDuty(ContractDuty duty)
        {
            if (Duties.ContainsKey(duty))
            {
                Duties[duty] = CampaignTime.Now;
            }
            else
            {
                Duties.Add(duty, CampaignTime.Now);
            }
        }

        public CampaignTime GetDutyTime(ContractDuty duty)
        {
            if (Duties.ContainsKey(duty))
            {
                return Duties[duty];
            }
            return CampaignTime.Zero;
        }

        public bool HasTimePassedForDuty(ContractDuty duty)
            => GetDutyTime(duty).ElapsedSeasonsUntilNow < duty.SeasonsDelay;

        // Drift de jure (mudança gradual de soberania)
        public Dictionary<FeudalTitle, float> DeJureDrifts
        {
            get
            {
                _deJureDrift ??= new Dictionary<FeudalTitle, float>();
                return _deJureDrift.ToDictionary(pair => pair.Key, pair => pair.Value);
            }
        }

        public Dictionary<Hero, ClaimType> Claims => _claims ??= new Dictionary<Hero, ClaimType>();

        public Dictionary<Hero, CampaignTime> OngoingClaims => _ongoingClaims ??= new Dictionary<Hero, CampaignTime>();

        public bool HeroHasValidClaim(Hero hero)
        {
            if (_claims.ContainsKey(hero))
            {
                return _claims[hero] != ClaimType.None && _claims[hero] != ClaimType.Ongoing;
            }
            return false;
        }

        // Detentor de facto - calculado dinamicamente
        public Hero DeFactoHolder
        {
            get
            {
                try
                {
                    if (Fief != null)
                    {
                        if (Fief.IsVillage)
                        {
                            return Fief.Owner;
                        }

                        // Não modificar Fief - apenas usar o owner atual
                        if (Fief.OwnerClan != null)
                        {
                            return Fief.Owner;
                        }

                        // Tentar encontrar o feudo atualizado
                        var currentFief = Settlement.All?.FirstOrDefault(x => x != null && x.StringId == Fief.StringId);
                        if (currentFief != null)
                        {
                            return currentFief.Owner;
                        }

                        return Fief.Owner;
                    }

                    var contestors = GetDeFactoHolders();
                    if (contestors != null && contestors.Count > 0)
                    {
                        var highestCount = contestors.Values.Max();
                        var highestCountHeroes = contestors.Keys.Where(x => x != null && contestors[x] == highestCount).ToList();
                        if (highestCountHeroes.Contains(deJure))
                        {
                            return deJure;
                        }

                        if (highestCountHeroes.Count <= 1)
                        {
                            return highestCountHeroes.FirstOrDefault() ?? deJure;
                        }

                        var selected = highestCountHeroes[0];
                        foreach (var competitor in highestCountHeroes)
                        {
                            if (competitor != selected && competitor?.Clan != null && selected?.Clan != null &&
                                (competitor.Clan.Tier > selected.Clan.Tier || competitor.Clan.Influence > selected.Clan.Influence))
                            {
                                selected = competitor;
                            }
                        }

                        return selected;
                    }
                    else return deJure;
                }
                catch (Exception ex)
                {
                    // Fallback seguro
                    InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in DeFactoHolder: {ex.Message}"));
                    return deJure;
                }
            }
        }

        // Obter todos os detentores de facto (contando vassalos)
        public Dictionary<Hero, int> GetDeFactoHolders()
        {
            Dictionary<Hero, int> holders = new Dictionary<Hero, int>(10);
            AddContestantsFromVassals(this, holders, new HashSet<FeudalTitle>());
            return holders;
        }

        private void AddContestantsFromVassals(FeudalTitle title, Dictionary<Hero, int> contestants, HashSet<FeudalTitle> visited)
        {
            if (title == null || !visited.Add(title)) return;
            if (title.Vassals != null && title.Vassals.Count > 0)
            {
                foreach (var vassal in title.Vassals)
                {
                    if (vassal == null) continue;
                    Hero defacto = vassal.DeFactoHolder;
                    if (defacto != null)
                    {
                        if (contestants.ContainsKey(defacto)) contestants[defacto] += 1;
                        else contestants.Add(defacto, 1);
                    }
                    AddContestantsFromVassals(vassal, contestants, visited);
                }
            }
        }

        public bool Active => deJure != null || deFacto != null;

        public bool IsSovereignLevel => (int)TitleType <= 1;

        public void SetFullName(TextObject name)
        {
            FullName = name;
        }

        public void SetShortName(TextObject shortName)
        {
            ShortName = shortName;
        }

        // Processar claims em andamento (tick diário)
        public void TickClaims()
        {
            var toAdd = new Dictionary<Hero, ClaimType>();
            foreach (var pair in OngoingClaims)
            {
                if (pair.Value.RemainingDaysFromNow <= 0f)
                {
                    toAdd.Add(pair.Key, ClaimType.Fabricated);
                }
                else
                {
                    pair.Key.AddSkillXp(DefaultSkills.Leadership, 3.5f);
                }
            }

            foreach (var pair in toAdd)
            {
                AddClaim(pair.Key, pair.Value);
            }
        }

        internal bool AdvanceClaim(Hero claimant, int? days)
        {
            if (claimant == null || !OngoingClaims.TryGetValue(claimant, out var deadline)) return false;
            if (days.HasValue && days.Value < deadline.RemainingDaysFromNow)
                OngoingClaims[claimant] = deadline - CampaignTime.Days(days.Value);
            else
                AddClaim(claimant, ClaimType.Fabricated);
            return true;
        }

        // Drift de jure progressivo
        public void TickDrift(FeudalTitle current, float progress = 0.000793f)
        {
            AddDrift(current, progress);
            List<FeudalTitle> toRemove = null;
            foreach (var sovereign in _deJureDrift.Keys.ToList().Where(sovereign => sovereign != current))
            {
                _deJureDrift[sovereign] -= progress;
                if (!(_deJureDrift[sovereign] <= 0f))
                {
                    continue;
                }

                toRemove ??= new List<FeudalTitle>();
                toRemove.Add(sovereign);
            }

            if (toRemove == null) return;

            foreach (var title in toRemove)
            {
                _deJureDrift.Remove(title);
            }
        }

        private void AddDrift(FeudalTitle newSovereign, float progress)
        {
            if (DeJureDrifts.ContainsKey(newSovereign))
            {
                _deJureDrift[newSovereign] += progress;
            }
            else
            {
                _deJureDrift.Add(newSovereign, 0f);
            }

            switch (_deJureDrift[newSovereign])
            {
                case >= 1f:
                    DriftTitle(newSovereign);
                    break;
                case < 0f:
                    _deJureDrift[newSovereign] = 0f;
                    break;
            }
        }

        public ClaimType GetHeroClaim(Hero hero)
        {
            if (hero == null) return ClaimType.None;

            if (_claims.ContainsKey(hero))
            {
                return _claims[hero];
            }

            if (_ongoingClaims.ContainsKey(hero))
            {
                return ClaimType.Ongoing;
            }

            return ClaimType.None;
        }

        public void AddOngoingClaim(Hero hero)
        {
            if (hero == null) return;

            if (!_ongoingClaims.ContainsKey(hero))
            {
                var claimTime = CampaignTime.YearsFromNow(CouncilDuties.ClaimYears(hero));

                _ongoingClaims.Add(hero, claimTime);
            }
        }

        public void AddClaim(Hero hero, ClaimType type, bool force = false)
        {
            if (hero == null || type == ClaimType.None) return;

            if (!_claims.ContainsKey(hero))
            {
                _claims.Add(hero, type);
            }
            else
            {
                _claims[hero] = type;
            }

            if (_ongoingClaims.ContainsKey(hero))
            {
                _ongoingClaims.Remove(hero);
            }

            if (hero == Hero.MainHero)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=bdsY7u3H}You now have a claim on the {TITLE}")
                        .SetTextVariable("TITLE", FullName).ToString()));
            }
        }

        public void RemoveClaim(Hero hero)
        {
            if (_claims.ContainsKey(hero))
            {
                _claims.Remove(hero);
            }
        }

        // Limpar claims inválidos
        public void CleanClaims()
        {
            var currentDeFacto = DeFactoHolder;
            var toRemove = new List<Hero>();
            foreach (var pair in _claims)
            {
                if (pair.Key.IsDead)
                {
                    toRemove.Add(pair.Key);
                    continue;
                }

                if (pair.Value == ClaimType.DeFacto && pair.Key != currentDeFacto)
                {
                    toRemove.Add(pair.Key);
                }
            }

            foreach (var hero in toRemove)
            {
                _claims.Remove(hero);
            }

            toRemove.Clear();
            toRemove.AddRange(_ongoingClaims.Keys.ToList().Where(hero => hero.IsDead));

            foreach (var hero in toRemove)
            {
                _ongoingClaims.Remove(hero);
            }
        }

        // Drift de título - muda soberano
        public void DriftTitle(FeudalTitle newSovereign, bool notify = true)
        {
            try
            {
                if (TitleType < TitleType.Kingdom) return;
                if (newSovereign == null) return;

                TitleManager.Instance.GetImmediateSuzerain(this)?.Vassals.Remove(this);
                SetSovereign(newSovereign);
                if (newSovereign.Vassals != null)
                {
                    newSovereign.Vassals.Add(this);
                }

                if (newSovereign.Contract != null)
                {
                    ChangeContract(newSovereign.Contract.Government);
                    ChangeContract(newSovereign.Contract.Succession);
                    ChangeContract(newSovereign.Contract.Inheritance);
                    ChangeContract(newSovereign.Contract.GenderLaw);
                }

                if (notify && newSovereign != null && FullName != null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=5ryn9tJc}{TITLE} has drifted into a legal part of {SOVEREIGN}")
                            .SetTextVariable("TITLE", FullName)
                            .SetTextVariable("SOVEREIGN", newSovereign.FullName)
                            .ToString()));
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in DriftTitle: {ex.Message}"));
            }
        }

        public void SetSovereign(FeudalTitle sovereign)
        {
            Sovereign = sovereign;
            if (Vassals is { Count: > 0 })
            {
                foreach (var vassal in Vassals)
                {
                    vassal.SetSovereign(sovereign);
                }
            }
        }

        public void SetLaws(List<DemesneLaw> laws)
        {
            Contract.SetLaws(laws);
            if (Vassals is { Count: > 0 })
            {
                foreach (var vassal in Vassals)
                {
                    vassal.SetLaws(laws);
                }
            }
        }

        public void EnactLaw(DemesneLaw law, Hero enactor = null, bool costInfluence = true)
        {
            if (enactor != null)
            {
                law = law.GetCopy();
                law.SetIssueDate(CampaignTime.Now);
                if (costInfluence)
                {
                    GainKingdomInfluenceAction.ApplyForDefault(enactor, -law.InfluenceCost);
                }

                if (enactor.MapFaction == Hero.MainHero.MapFaction)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=fvbLMV0a}The {LAW} has been enacted in the {TITLE}.")
                            .SetTextVariable("LAW", law.Name)
                            .SetTextVariable("TITLE", FullName)
                            .ToString()));
                }
            }

            Contract.EnactLaw(law);
            if (Vassals is { Count: > 0 })
            {
                foreach (var vassal in Vassals)
                {
                    vassal.EnactLaw(law);
                }
            }
        }

        public void ChangeContract(Government government)
        {
            Contract.ChangeGovernment(government);
            if (Vassals is { Count: > 0 })
            {
                foreach (var vassal in Vassals)
                {
                    vassal.ChangeContract(government);
                }
            }
        }

        public void ChangeContract(Succession succession)
        {
            Contract.ChangeSuccession(succession);
            if (Vassals is { Count: > 0 })
            {
                foreach (var vassal in Vassals)
                {
                    vassal.ChangeContract(succession);
                }
            }
        }

        public void ChangeContract(Inheritance inheritance)
        {
            Contract.ChangeInheritance(inheritance);
            if (Vassals is { Count: > 0 })
            {
                foreach (var vassal in Vassals)
                {
                    vassal.ChangeContract(inheritance);
                }
            }
        }

        public void ChangeContract(GenderLaw genderLaw)
        {
            Contract.ChangeGenderLaw(genderLaw);
            if (Vassals is { Count: > 0 })
            {
                foreach (var vassal in Vassals)
                {
                    vassal.ChangeContract(genderLaw);
                }
            }
        }

        // Aspectos contratuais (Conquest, Taxes, etc.)
        public void ChangeContract(ContractAspect aspect)
        {
            if (aspect == null) return;
            Contract.AddAspect(aspect);
            if (Vassals is { Count: > 0 })
            {
                foreach (var vassal in Vassals)
                {
                    vassal.ChangeContract(aspect);
                }
            }
        }

        // Verifica se o herói/clã/reino é detentor
        public bool IsHolder(Hero hero)
        {
            if (hero == null) return false;
            return deJure == hero || DeFactoHolder == hero;
        }

        public bool IsHolder(Clan clan)
        {
            if (clan == null) return false;
            if (deJure != null && deJure.Clan == clan) return true;
            if (DeFactoHolder != null && DeFactoHolder.Clan == clan) return true;
            return false;
        }

        public bool IsHolder(Kingdom kingdom)
        {
            if (kingdom == null) return false;
            if (deJure != null && deJure.Clan != null && deJure.Clan.Kingdom == kingdom) return true;
            if (DeFactoHolder != null && DeFactoHolder.Clan != null && DeFactoHolder.Clan.Kingdom == kingdom) return true;
            return false;
        }

        public bool IsHolder(string kingdomId)
        {
            if (string.IsNullOrEmpty(kingdomId)) return false;
            if (deJure != null && deJure.Clan != null && deJure.Clan.Kingdom != null && deJure.Clan.Kingdom.StringId == kingdomId) return true;
            if (DeFactoHolder != null && DeFactoHolder.Clan != null && DeFactoHolder.Clan.Kingdom != null && DeFactoHolder.Clan.Kingdom.StringId == kingdomId) return true;
            return false;
        }

        // Verifica se foi usurpado
        public bool IsUsurped
        {
            get
            {
                if (deJure != null && DeFactoHolder != null)
                {
                    return deJure != DeFactoHolder && deJure.Clan != DeFactoHolder.Clan;
                }
                return false;
            }
        }
    }
}
