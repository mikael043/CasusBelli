using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using CasusBelli.Managers.Titles.Governments;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace CasusBelli.Managers.Titles
{
    // Gerenciador principal do sistema de títulos feudais
    // Equivalente ao TitleManager do BannerKings
    public class TitleManager
    {
        private static TitleManager _instance;

        public static TitleManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new TitleManager();
                }
                return _instance;
            }
        }

        [SaveableProperty(1)] private List<FeudalTitle> _titles { get; set; }
        [SaveableProperty(2)] public Dictionary<FeudalTitle, Kingdom> Kingdoms { get; private set; }
        [SaveableProperty(3)] private Dictionary<Hero, float> _knights { get; set; } = new();

        private Dictionary<Hero, List<FeudalTitle>> _deJuresCache;
        private Dictionary<Settlement, FeudalTitle> _settlementCache;

        // Cache de vassalos por clã (com invalidação diária)
        private Dictionary<Clan, (long day, List<Hero> vassals)> _allVassalsCache;

        // Flag de inicialização
        private bool _initialized = false;
        public bool IsInitialized => _initialized;

        // Lock para thread safety (UI thread + campaign thread)
        private object _cacheLock;
        private object CacheLock
        {
            get
            {
                if (_cacheLock == null)
                    System.Threading.Interlocked.CompareExchange(ref _cacheLock, new object(), null);
                return _cacheLock;
            }
        }

        internal List<FeudalTitle> AllTitles => _titles ?? new List<FeudalTitle>();

        private TitleManager()
        {
            _titles = new List<FeudalTitle>();
            Kingdoms = new Dictionary<FeudalTitle, Kingdom>();
            _deJuresCache = new Dictionary<Hero, List<FeudalTitle>>();
            _settlementCache = new Dictionary<Settlement, FeudalTitle>();
            _allVassalsCache = new Dictionary<Clan, (long, List<Hero>)>();
            _knights = new Dictionary<Hero, float>();
        }

        // Inicialização do sistema de títulos (chamado no início de novo jogo ou load)
        public void Initialize()
        {
            try
            {
                if (_initialized) return;
                _initialized = true;

                // Garantir que todas as coleções estão inicializadas
                _titles ??= new List<FeudalTitle>();
                Kingdoms ??= new Dictionary<FeudalTitle, Kingdom>();
                _knights ??= new Dictionary<Hero, float>();
                _deJuresCache ??= new Dictionary<Hero, List<FeudalTitle>>();
                _settlementCache ??= new Dictionary<Settlement, FeudalTitle>();
                _allVassalsCache ??= new Dictionary<Clan, (long, List<Hero>)>();

                RefreshCaches();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[TitleSystem] Erro na inicialização: " + ex.Message));
            }
        }

        // Inicialização após carregamento do save
        public void RefreshCaches()
        {
            lock (CacheLock)
            {
                _settlementCache ??= new Dictionary<Settlement, FeudalTitle>();

                if (_deJuresCache == null)
                {
                    _deJuresCache = new Dictionary<Hero, List<FeudalTitle>>();
                }
                else
                {
                    _settlementCache.Clear();
                    _deJuresCache.Clear();
                }

                foreach (FeudalTitle title in _titles)
                {
                    Hero hero = title.deJure;
                    if (hero != null)
                    {
                        if (!_deJuresCache.ContainsKey(hero))
                        {
                            _deJuresCache.Add(hero, new List<FeudalTitle> { title });
                        }
                        else
                        {
                            _deJuresCache[hero].Add(title);
                        }
                    }

                    if (title.Fief != null)
                    {
                        _settlementCache.Add(title.Fief, title);
                    }
                }

                _knights ??= new Dictionary<Hero, float>();

                // Invalidar cache de vassalos
                if (_allVassalsCache == null) _allVassalsCache = new Dictionary<Clan, (long, List<Hero>)>();
                else _allVassalsCache.Clear();
            }
        }

        public void PostInitialize()
        {
            try
            {
                RefreshCaches();
                foreach (var title in _titles)
                {
                    try
                    {
                        if (title?.Contract != null && (title.Contract.DemesneLaws == null || title.Contract.DemesneLaws.Count == 0))
                            title.Contract.SetLaws(DefaultDemesneLaws.Instance.GetAdequateLaws(title));

                        title?.PostInitialize();

                        if (title?.Contract?.DemesneLaws != null)
                        {
                            foreach (var law in DefaultDemesneLaws.Instance.GetAdequateLaws(title))
                                if (!title.Contract.DemesneLaws.Any(x => x.LawType == law.LawType))
                                    title.Contract.DemesneLaws.Add(law);
                        }
                    }
                    catch (Exception ex)
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in PostInitialize for title: {ex.Message}"));
                    }
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in PostInitialize: {ex.Message}"));
            }
        }

        public bool IsHeroTitleHolder(Hero hero)
        {
            lock (CacheLock)
            {
                if (_deJuresCache != null && _deJuresCache.TryGetValue(hero, out var list))
                {
                    return list.Count > 0;
                }
                return false;
            }
        }

        public bool IsKnight(Hero hero)
        {
            if (hero == null) return false;
            lock (CacheLock)
            {
                return _knights != null && _knights.ContainsKey(hero);
            }
        }

        // Obter título de um assentamento
        public FeudalTitle GetTitle(Settlement settlement)
        {
            try
            {
                if (_titles == null || settlement == null) return null;

                lock (CacheLock)
                {
                    if (_settlementCache != null && _settlementCache.TryGetValue(settlement, out var cached))
                    {
                        return cached;
                    }
                }

                return _titles.FirstOrDefault(x => x.Fief != null && x.Fief.StringId == settlement.StringId);
            }
            catch (Exception ex)
            {
                throw new Exception($"Exception in GetTitle method. Settlement: {settlement?.Name} [{settlement?.StringId}]", ex);
            }
        }

        public List<FeudalTitle> GetAllTitlesByType(TitleType type)
        {
            try
            {
                return _titles?.FindAll(x => x.TitleType == type) ?? new List<FeudalTitle>();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in GetAllTitlesByType: {ex.Message}"));
                return new List<FeudalTitle>();
            }
        }

        public FeudalTitle GetTitleByName(string name)
        {
            try
            {
                return _titles?.FirstOrDefault(x => x.FullName.ToString() == name);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in GetTitleByName: {ex.Message}"));
                return null;
            }
        }

        public FeudalTitle GetTitleByStringId(string stringId)
        {
            try
            {
                return _titles?.FirstOrDefault(x => x.StringId == stringId);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in GetTitleByStringId: {ex.Message}"));
                return null;
            }
        }

        public Government GetSettlementGovernment(Settlement settlement)
        {
            Government type = DefaultGovernments.Instance.Feudal;
            var title = GetTitle(settlement);
            if (title?.Contract != null)
            {
                type = title.Contract.Government;
            }
            return type;
        }

        // Conceder cavalaria (knighthood)
        public void GrantKnighthood(FeudalTitle title, Hero knight, Hero grantor, bool ignoreCosts = false)
        {
            var action = GetModel().GetAction(ActionType.Grant, title, grantor);
            if (!ignoreCosts)
                action.Influence = -GetModel().GetGrantKnighthoodCost(grantor).ResultNumber;
            GrantTitle(action, knight);

            if (grantor == Hero.MainHero)
            {
                GiveGoldAction.ApplyBetweenCharacters(grantor, knight, 5000);
            }

            knight.Clan = grantor.Clan;

            if (Clan.PlayerClan.Kingdom != null && grantor.Clan.Kingdom == Clan.PlayerClan.Kingdom)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=AyXDhK2V}The {CLAN} has knighted {KNIGHT}.")
                        .SetTextVariable("CLAN", grantor.Clan.EncyclopediaLinkWithName)
                        .SetTextVariable("KNIGHT", knight.EncyclopediaLinkWithName)
                        .ToString()));
            }

            grantor.AddSkillXp(DefaultSkills.Leadership, 300f);
            AddKnightInfluence(knight, 0f);
        }

        public bool IsHeroKnighted(Hero hero)
        {
            return hero.IsLord && IsHeroTitleHolder(hero);
        }

        // Obter suserano imediato
        public FeudalTitle GetImmediateSuzerain(FeudalTitle target)
        {
            try
            {
                foreach (var pair in _titles)
                {
                    if (pair.Vassals != null && pair.Vassals.Contains(target))
                    {
                        return pair;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in GetImmediateSuzerain: {ex.Message}"));
                return null;
            }
        }

        // Executar mudança de dono
        internal void ExecuteOwnershipChange(Hero oldOwner, Hero newOwner, FeudalTitle title, bool deJure)
        {
            try
            {
                if (_titles.Contains(title))
                {
                    if (deJure)
                    {
                        title.deJure = newOwner;
                        lock (CacheLock)
                        {
                            if (oldOwner != null && _deJuresCache != null && _deJuresCache.TryGetValue(oldOwner, out var oldList))
                            {
                                oldList.Remove(title);
                                if (oldList.Count == 0)
                                {
                                    _deJuresCache.Remove(oldOwner);
                                    if (_knights != null && _knights.ContainsKey(oldOwner))
                                    {
                                        _knights.Remove(oldOwner);
                                    }
                                }
                            }

                            if (newOwner != null)
                            {
                                _deJuresCache ??= new Dictionary<Hero, List<FeudalTitle>>();
                                if (_deJuresCache.TryGetValue(newOwner, out var newList)) newList.Add(title);
                                else _deJuresCache.Add(newOwner, new List<FeudalTitle> { title });
                            }

                            // Invalidar cache de vassalos apenas dos clãs afetados
                            if (_allVassalsCache != null)
                            {
                                if (oldOwner?.Clan != null) _allVassalsCache.Remove(oldOwner.Clan);
                                if (newOwner?.Clan != null) _allVassalsCache.Remove(newOwner.Clan);
                            }
                        }
                    }
                    else title.deFacto = newOwner;
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in ExecuteOwnershipChange: {ex.Message}"));
            }
        }

        internal void ExecuteAddTitle(FeudalTitle title)
        {
            if (!_titles.Contains(title)) _titles.Add(title);
            RefreshCaches();
        }

        // Criar novo título (público para uso externo)
        public FeudalTitle CreateTitle(Settlement settlement, Kingdom kingdom, FeudalContract contract, TitleType type = TitleType.County)
        {
            var title = TitleGenerator.CreateLandedTitle(settlement, settlement.Owner, type, contract);
            return title;
        }

        // Calcular suserano de um herói
        public FeudalTitle CalculateHeroSuzerain(Hero hero)
        {
            try
            {
                var title = GetHighestTitle(hero);
                if (title == null)
                {
                    return null;
                }

                var kingdom1 = GetTitleFaction(title);

                if (kingdom1 == null || hero.Clan?.Kingdom == null)
                {
                    return null;
                }

                var suzerain = GetImmediateSuzerain(title);
                if (suzerain != null)
                {
                    var kingdom2 = GetTitleFaction(suzerain);
                    if (kingdom2 == kingdom1)
                    {
                        return suzerain;
                    }

                    var factionTitle = GetHighestTitleWithinFaction(hero, kingdom1);
                    if (factionTitle != null)
                    {
                        var suzerainFaction = GetImmediateSuzerain(factionTitle);
                        return suzerainFaction;
                    }

                    return GetHighestTitle(kingdom1.Leader);
                }

                return null;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in CalculateHeroSuzerain: {ex.Message}"));
                return null;
            }
        }

        // Calcular todos os vassalos de um clã (com cache diário)
        public List<Hero> CalculateAllVassals(Clan clan)
        {
            try
            {
                if (clan == null) return new List<Hero>();

                long day = (long)Math.Floor(CampaignTime.Now.ToDays);
                lock (CacheLock)
                {
                    if (_allVassalsCache != null
                        && _allVassalsCache.TryGetValue(clan, out var cached)
                        && cached.day == day)
                    {
                        return cached.vassals;
                    }
                }

                // Computar FORA do lock (operação pesada)
                var result = CalculateAllVassalsUncached(clan);

                lock (CacheLock)
                {
                    _allVassalsCache ??= new Dictionary<Clan, (long, List<Hero>)>();
                    _allVassalsCache[clan] = (day, result);
                }
                return result;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in CalculateAllVassals: {ex.Message}"));
                return new List<Hero>();
            }
        }

        private List<Hero> CalculateAllVassalsUncached(Clan clan)
        {
            try
            {
                var set = new HashSet<Hero>();
                foreach (var title in GetAllDeJure(clan))
                {
                    if (title.Fief != null && title.Fief.IsVillage)
                    {
                        // Gentry logic could go here
                    }

                    if (title.Vassals == null || title.Vassals.Count == 0)
                    {
                        continue;
                    }

                    foreach (var vassal in title.Vassals)
                    {
                        var deJure = vassal.deJure;
                        if (deJure != null && deJure != clan.Leader)
                        {
                            if (deJure.Clan == clan)
                            {
                                set.Add(deJure);
                            }
                            else
                            {
                                var suzerain = CalculateHeroSuzerain(deJure);
                                if (suzerain != null && suzerain.deJure == clan.Leader && clan.MapFaction == vassal.deJure.MapFaction)
                                {
                                    set.Add(deJure);
                                }
                            }
                        }
                    }
                }

                return set.ToList();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in CalculateAllVassalsUncached: {ex.Message}"));
                return new List<Hero>();
            }
        }

        public Dictionary<Clan, List<FeudalTitle>> CalculateVassals(Clan suzerainClan, Clan targetClan = null)
        {
            var clans = new Dictionary<Clan, List<FeudalTitle>>();
            var kingdom = suzerainClan?.Kingdom;
            if (kingdom == null || suzerainClan == null)
            {
                return clans;
            }

            var suzerainTitles = GetAllDeJure(suzerainClan);
            if (suzerainTitles.Count == 0)
            {
                return clans;
            }

            foreach (var title in suzerainTitles)
            {
                if (title.Vassals is not { Count: > 0 })
                {
                    continue;
                }

                foreach (var vassal in title.Vassals)
                {
                    if (vassal.deJure == null || vassal.deJure.Clan == suzerainClan ||
                        (targetClan != null && vassal.deJure.Clan != targetClan))
                    {
                        continue;
                    }

                    var vassalSuzerain = CalculateHeroSuzerain(vassal.deJure);
                    if (vassalSuzerain == null)
                    {
                        continue;
                    }

                    var suzerainDeJureClan = vassalSuzerain.deJure.Clan;
                    if (suzerainDeJureClan != suzerainClan)
                    {
                        continue;
                    }

                    var vassalDeJureClan = vassal.deJure.Clan;
                    if (!clans.ContainsKey(vassalDeJureClan))
                    {
                        clans.Add(vassalDeJureClan, new List<FeudalTitle> { vassal });
                    }
                    else
                    {
                        clans[vassalDeJureClan].Add(title);
                    }
                }
            }

            return clans;
        }

        public bool HasSuzerain(FeudalTitle vassal)
        {
            var suzerain = GetImmediateSuzerain(vassal);
            return suzerain != null;
        }

        // Herdar todos os títulos
        public void InheritAllTitles(Hero oldOwner, Hero heir)
        {
            if (IsHeroTitleHolder(oldOwner))
            {
                var set = GetAllDeJure(oldOwner);
                var titles = new List<FeudalTitle>(set);
                foreach (var title in titles)
                {
                    if (title.deJure == oldOwner)
                    {
                        ExecuteOwnershipChange(oldOwner, heir, title, true);
                    }

                    if (title.deFacto == oldOwner)
                    {
                        ExecuteOwnershipChange(oldOwner, heir, title, false);
                    }
                }
            }
        }

        public void InheritTitle(Hero oldOwner, Hero heir, FeudalTitle title)
        {
            if (oldOwner != null)
            {
                if (title.deJure == oldOwner)
                {
                    ExecuteOwnershipChange(oldOwner, heir, title, true);
                }

                if (title.deFacto == oldOwner)
                {
                    ExecuteOwnershipChange(oldOwner, heir, title, false);
                }
            }
        }

        // Adicionar claim em andamento
        public void AddOngoingClaim(TitleAction action)
        {
            var claimant = action.ActionTaker;

            if (action.Title.GetHeroClaim(claimant) != ClaimType.None) return;
            action.Title.AddOngoingClaim(claimant);
            GainKingdomInfluenceAction.ApplyForDefault(claimant, -action.Influence);
            claimant.ChangeHeroGold((int)-action.Gold);
            claimant.Clan.Renown -= (int)action.Renown;
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(action.ActionTaker, action.Title.deJure, (int)Math.Min(-5f, new TitleModel().GetRelationImpact(action.Title) * -0.1f));

            Campaign.Current.GetCampaignBehavior<TitleDisputeBehavior>()?.Track(action.Title, claimant);

            if (action.Title.deJure == Hero.MainHero)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=xEOemRjF}{CLAIMANT} is building a claim on your title, {TITLE}.")
                        .SetTextVariable("CLAIMANT", claimant.EncyclopediaLinkWithName)
                        .SetTextVariable("TITLE", action.Title.FullName)
                        .ToString()));
            }
        }

        // Criar título
        public void CreateTitle(TitleAction action)
        {
            FeudalTitle title = action.Title;
            title.SetFullName(TextHelper.FormatFullName(title.TitleType, title.ShortName));

            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=dFTm4AbE}The {TITLE} has been founded by {FOUNDER}.")
                    .SetTextVariable("FOUNDER", action.ActionTaker.EncyclopediaLinkWithName)
                    .SetTextVariable("TITLE", title.FullName)
                    .ToString()));

            title.RemoveClaim(action.ActionTaker);
            ExecuteOwnershipChange(null, action.ActionTaker, title, true);

            if (action.Gold > 0)
            {
                action.ActionTaker.ChangeHeroGold((int)-action.Gold);
            }

            if (action.Influence > 0)
            {
                GainKingdomInfluenceAction.ApplyForDefault(action.ActionTaker, -action.Influence);
            }

            if (action.Renown > 0)
            {
                GainRenownAction.Apply(action.ActionTaker, action.Renown);
            }

            action.ActionTaker.AddSkillXp(DefaultSkills.Leadership,
                GetModel().GetSkillReward(title.TitleType, action.Type));
        }

        // Revogar título
        public void RevokeTitle(TitleAction action)
        {
            var currentOwner = action.Title.deJure;
            if (action.Title.GetHeroClaim(action.ActionTaker) == ClaimType.None)
            {
                RealmPolitics.Conduct(action.ActionTaker,action.ActionTaker.Clan?.Kingdom,0,10);
                RealmPolitics.Shift(action.ActionTaker.Clan?.Kingdom,-4);
            }
            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=D50E4DZk}{REVOKER} has revoked the {TITLE}.")
                    .SetTextVariable("REVOKER", action.ActionTaker.EncyclopediaLinkWithName)
                    .SetTextVariable("TITLE", action.Title.FullName)
                    .ToString()));
            var impact = new TitleModel().GetRelationImpact(action.Title);
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(action.ActionTaker, currentOwner, impact);

            action.Title.RemoveClaim(action.ActionTaker);
            action.Title.AddClaim(currentOwner, ClaimType.PreviousOwner, true);
            ExecuteOwnershipChange(currentOwner, action.ActionTaker, action.Title, true);

            if (action.Gold > 0)
            {
                action.ActionTaker.ChangeHeroGold((int)-action.Gold);
            }

            if (action.Influence > 0)
            {
                action.ActionTaker.Clan.Influence -= action.Influence;
            }

            if (action.Renown > 0)
            {
                action.ActionTaker.Clan.Renown -= action.Renown;
            }
        }

        // Conceder terras (estate)
        public void GrantEstate(EstateAction action)
        {
            var grantor = action.ActionTaker;

            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(grantor, action.ActionTarget, 15);
            GainKingdomInfluenceAction.ApplyForDefault(grantor, -action.Influence);
            grantor.AddSkillXp(DefaultSkills.Leadership, 25);

            // EstateAction não tem Estate - usamos o título para transferir
            if (action.Title?.Fief != null)
            {
                ChangeOwnerOfSettlementAction.ApplyByDefault(action.ActionTarget, action.Title.Fief);
            }
        }

        // Conceder título
        public void GrantTitle(TitleAction action, Hero receiver)
        {
            var grantor = action.ActionTaker;

            ExecuteOwnershipChange(grantor, receiver, action.Title, true);
            var kingdom = grantor.Clan.Kingdom;
            if (action.Title.Fief == null && receiver.Clan.Kingdom != null && receiver.Clan.Kingdom == kingdom)
            {
                ExecuteOwnershipChange(grantor, receiver, action.Title, false);
            }

            var relationChange = GetModel().GetRelationImpact(action.Title);

            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(grantor, receiver, -relationChange);
            GainKingdomInfluenceAction.ApplyForDefault(grantor, -action.Influence);
            grantor.AddSkillXp(DefaultSkills.Leadership,
                GetModel().GetSkillReward(action.Title.TitleType, action.Type));

            GainRenownAction.Apply(grantor, -action.Renown);

            var fief = action.Title.Fief;
            if (receiver.Clan.Leader == receiver && fief?.OwnerClan == grantor.Clan && fief != null && (fief.IsTown || fief.IsCastle))
            {
                ChangeOwnerOfSettlementAction.ApplyByGift(fief, receiver);
            }

            if (receiver.CompanionOf != null)
            {
                receiver.Clan = grantor.Clan;
            }

            if (receiver == Hero.MainHero)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=!}{GRANTOR} has decided to grant you the {TITLE}")
                    .SetTextVariable("GRANTOR", grantor.Name)
                    .SetTextVariable("TITLE", action.Title.FullName)
                    .ToString(),
                    Color.FromUint(4278255360)));
            }
        }

        // Usurpar título
        public void UsurpTitle(Hero oldOwner, TitleAction action)
        {
            var usurper = action.ActionTaker;

            var title = action.Title;
            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=c9RCCv20}{USURPER} has usurped the {TITLE}.")
                    .SetTextVariable("USURPER", usurper.EncyclopediaLinkWithName)
                    .SetTextVariable("TITLE", action.Title.FullName)
                    .ToString()));
            if (title.deJure == Hero.MainHero)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=ZAjBRwSY}{USURPER} has usurped your title, {TITLE}.")
                        .SetTextVariable("USURPER", usurper.EncyclopediaLinkWithName)
                        .SetTextVariable("TITLE", action.Title.FullName)
                        .ToString()));
            }

            if (title.IsSovereignLevel)
            {
                Kingdoms[title] = action.ActionTaker.Clan.Kingdom;
            }

            var impact = GetModel().GetRelationImpact(title);
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(usurper, oldOwner, impact);
            var kingdom = oldOwner.Clan.Kingdom;
            if (kingdom != null)
            {
                foreach (var clan in kingdom.Clans)
                {
                    if (clan == oldOwner.Clan || clan == usurper.Clan || clan.IsUnderMercenaryService)
                    {
                        continue;
                    }

                    var random = MBRandom.RandomInt(1, 100);
                    if (random <= 10)
                    {
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(usurper, oldOwner, (int)(impact * 0.3f));
                    }
                }
            }

            if (action.Gold > 0)
            {
                usurper.ChangeHeroGold((int)-action.Gold);
            }

            if (action.Influence > 0)
            {
                usurper.Clan.Influence -= action.Influence;
            }

            if (action.Renown > 0)
            {
                usurper.Clan.Renown -= action.Renown;
            }

            title.RemoveClaim(usurper);
            title.AddClaim(oldOwner, ClaimType.PreviousOwner, true);
            ExecuteOwnershipChange(oldOwner, usurper, title, true);

            action.ActionTaker.AddSkillXp(DefaultSkills.Leadership,
                GetModel().GetSkillReward(action.Title.TitleType, action.Type));
        }

        // Dar senhorio ao entrar em reino
        public void GiveLordshipOnKingdomJoin(Kingdom newKingdom, Clan clan, bool force = false)
        {
            var clanTitles = GetAllDeJure(clan.Leader);
            if (clanTitles.Count > 0)
            {
                return;
            }

            var sovereign = GetSovereignTitle(newKingdom);
            if (sovereign?.Contract == null)
            {
                return;
            }

            if (force)
            {
                goto GIVE;
            }

            if (!sovereign.Contract.ContractAspects.Contains(DefaultContractAspects.Instance.Enfeoffment))
            {
                return;
            }

        GIVE:
            Hero owner = newKingdom.Leader;
            var titles = GetAllDeJure(newKingdom.Leader);
            if (titles.Count == 0 || titles.FindAll(x => x.TitleType == TitleType.Lordship).Count == 0)
            {
                foreach (Clan kingdomClan in newKingdom.Clans)
                {
                    if (kingdomClan != Clan.PlayerClan)
                    {
                        owner = kingdomClan.Leader;
                        titles = GetAllDeJure(kingdomClan.Leader);
                        if (titles.Count > 0 && titles.FindAll(x => x.TitleType == TitleType.Lordship).Count > 0)
                        {
                            break;
                        }
                    }
                }
            }

            var lordships = titles.FindAll(x => x.TitleType == TitleType.Lordship);
            if (lordships.Count == 0)
            {
                return;
            }

            var lordship = (from l in lordships where l.Fief != null select l into x orderby x.Fief.Village.Hearth select x)
                .FirstOrDefault();
            if (lordship != null)
            {
                var action = GetModel().GetAction(ActionType.Grant, lordship, owner);
                action.Influence = -GetModel().GetGrantKnighthoodCost(owner)
                    .ResultNumber;
                action.TakeAction(clan.Leader);

                if (clan == Clan.PlayerClan)
                {
                    GameTexts.SetVariable("FIEF", lordship.FullName);
                    GameTexts.SetVariable("SOVEREIGN", sovereign.FullName);
                    InformationManager.ShowInquiry(new InquiryData("Enfeoffment Right",
                        new TextObject("{=pmmxMLmr}You have been generously granted the {FIEF} as part of your vassal rights to the {SOVEREIGN}.").ToString(),
                        true, false, GameTexts.FindText("str_done").ToString(), null, null, null));
                }
            }
        }

        public void AddKnightInfluence(Hero hero, float influence)
        {
            if (hero == null) return;
            lock (CacheLock)
            {
                _knights ??= new Dictionary<Hero, float>();
                if (_knights.ContainsKey(hero))
                {
                    _knights[hero] += influence;
                }
                else
                {
                    _knights.Add(hero, influence);
                }
            }
        }

        public void RemoveKnights(Hero hero)
        {
            if (hero == null) return;
            lock (CacheLock)
            {
                if (_knights != null && _knights.ContainsKey(hero))
                {
                    _knights.Remove(hero);
                }
            }
        }

        public float GetKnightInfluence(Hero hero)
        {
            if (hero == null) return 0f;
            lock (CacheLock)
            {
                if (_knights != null && _knights.TryGetValue(hero, out var value))
                {
                    return value;
                }
            }
            return 0f;
        }

        // Obter todos os títulos de jure de um herói
        public List<FeudalTitle> GetAllDeJure(Hero hero)
        {
            lock (CacheLock)
            {
                if (_deJuresCache != null)
                {
                    _deJuresCache.TryGetValue(hero, out var titleList);
                    if (titleList == null)
                        return new List<FeudalTitle>();

                    // Snapshot para evitar race condition
                    return new List<FeudalTitle>(titleList);
                }
            }

            var list = new List<FeudalTitle>();
            foreach (var title in _titles.ToList())
                if (title.deJure == hero)
                    list.Add(title);

            return list;
        }

        public List<FeudalTitle> GetAllDeJure(Clan clan)
        {
            var list = new List<FeudalTitle>();
            foreach (var hero in clan.Heroes)
                list.AddRange(GetAllDeJure(hero));

            return list;
        }

        // Obter título mais alto de um herói
        public FeudalTitle GetHighestTitle(Hero hero)
        {
            if (hero == null) return null;
            lock (CacheLock)
            {
                if (_deJuresCache == null || !_deJuresCache.TryGetValue(hero, out var titles)) return null;
                FeudalTitle highest = null;
                foreach (var title in titles)
                {
                    if (highest == null || title.TitleType < highest.TitleType) highest = title;
                }
                return highest;
            }
        }

        // Adicionar título da hierarquia XML
        public FeudalTitle GetHighestTitleWithinFaction(Hero hero, Kingdom faction)
        {
            if (hero != null && faction != null && IsHeroTitleHolder(hero))
            {
                FeudalTitle highestTitle = null;
                foreach (var title in GetAllDeJure(hero))
                {
                    if ((highestTitle == null || title.TitleType < highestTitle.TitleType) && GetTitleFaction(title) == faction)
                    {
                        highestTitle = title;
                    }
                }

                return highestTitle;
            }

            return null;
        }

        public List<FeudalTitle> GetSovereignTitleList(Kingdom faction)
        {
            try
            {
                if (faction != null)
                {
                    List<FeudalTitle> titles = new List<FeudalTitle>();
                    foreach (var pair in Kingdoms)
                        if (pair.Value == faction)
                            titles.Add(pair.Key);

                    return titles;
                }
                else throw new NullReferenceException();
            }
            catch (Exception ex)
            {
                throw new Exception($"Exception in GetSovereignTitleList. Faction: {faction?.Name}", ex);
            }
        }

        public FeudalTitle GetSovereignTitle(Kingdom faction)
        {
            try
            {
                if (faction != null)
                {
                    List<FeudalTitle> list = GetSovereignTitleList(faction);
                    var empire = list.FirstOrDefault(x => x.TitleType == TitleType.Empire);
                    if (empire != null)
                        return empire;

                    if (list.Count > 1)
                        list.Sort((x, y) => x.Priority.CompareTo(y.Priority));

                    return list.FirstOrDefault();
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Exception in GetSovereignTitle. Faction: {faction?.Name}", ex);
            }
        }

        public List<FeudalTitle> GetVassals(TitleType threshold, Hero lord)
        {
            var allTitles = GetAllDeJure(lord);
            var vassals = new List<FeudalTitle>();
            foreach (var title in allTitles)
            {
                if (title.deFacto.MapFaction == lord.MapFaction && (title.deFacto == title.deJure ||
                    title.deJure.MapFaction == lord.MapFaction)
                && (int)title.TitleType <= (int)threshold)
                {
                    vassals.Add(title);
                }
            }

            return vassals;
        }

        public List<FeudalTitle> GetVassals(Hero lord)
        {
            var vassals = new List<FeudalTitle>();
            var highest = GetHighestTitle(lord);
            if (highest != null)
            {
                var threshold = GetHighestTitle(lord).TitleType + 1;
                var allTitles = GetAllDeJure(lord);

                foreach (var title in allTitles)
                {
                    if (title.deFacto == null || title.deJure == null) continue;

                    if (title.deFacto.MapFaction == lord.MapFaction && (title.deFacto == title.deJure ||
                        title.deJure.MapFaction == lord.MapFaction)
                    && (int)title.TitleType >= (int)threshold)
                    {
                        vassals.Add(title);
                    }
                }
            }

            return vassals;
        }

        public Kingdom GetTitleFaction(FeudalTitle title)
        {
            Kingdom faction;
            FeudalTitle sovereign = title.Sovereign;
            Kingdoms.TryGetValue(sovereign != null ? sovereign : title, out faction);

            return faction;
        }

        // Aplicar mudança de dono de feudo
        public void ApplyOwnerChange(Settlement settlement, Hero newOwner)
        {
            var title = GetTitle(settlement);
            if (title == null)
            {
                return;
            }

            ExecuteOwnershipChange(title.deFacto, newOwner, title, false);
            if (!settlement.IsVillage && settlement.BoundVillages is { Count: > 0 } &&
                title.Vassals is { Count: > 0 })
            {
                foreach (var lordship in title.Vassals.Where(y => y.TitleType == TitleType.Lordship))
                {
                    ExecuteOwnershipChange(lordship.deFacto, newOwner, lordship, false);
                }
            }
        }

        // Chamado quando o dono de jure muda (usurpação passiva)
        public void OnDeJureOwnerChanged(string settlementId, string newKingdomId)
        {
            var title = GetTitleByStringId(settlementId);
            if (title == null) return;

            // Encontrar o novo dono de jure (líder do reino)
            Kingdom kingdom = null;
            foreach (Kingdom k in Kingdom.All)
            {
                if (k.StringId == newKingdomId)
                {
                    kingdom = k;
                    break;
                }
            }

            if (kingdom?.Leader != null)
            {
                ExecuteOwnershipChange(title.deJure, kingdom.Leader, title, true);
            }
        }

        public void DeactivateTitle(FeudalTitle title)
        {
            ExecuteOwnershipChange(title.deJure, null, title, true);
            ExecuteOwnershipChange(title.deFacto, null, title, false);
        }

        public string BuildKingdomTitlesText(string kingdomId)
        {
            if (string.IsNullOrEmpty(kingdomId))
                return "";

            var titles = new List<FeudalTitle>();
            foreach (var title in _titles)
            {
                if (title.IsHolder(kingdomId))
                {
                    titles.Add(title);
                }
            }

            if (titles.Count == 0)
            {
                return "Nenhum título registrado.";
            }

            var sb = new System.Text.StringBuilder();
            var kingdom = Kingdom.All.FirstOrDefault(k => k.StringId == kingdomId);
            string kingdomName = kingdom?.Name?.ToString() ?? kingdomId;

            sb.AppendLine($"Títulos de {kingdomName}:");

            foreach (var t in titles.OrderBy(x => x.TitleType))
            {
                string settlementName = t.Fief?.Name?.ToString() ?? "Unlanded";
                string rankName = TextHelper.RankWithArticle(t.TitleType, kingdomId);

                if (t.IsUsurped)
                {
                    rankName += " (usurpado)";
                }

                sb.AppendLine($"  {rankName} ({settlementName})");
            }

            return sb.ToString();
        }

        public void ShowContract(Hero lord, string buttonString)
        {
            var kingdom = lord.Clan.Kingdom;
            if (kingdom == null)
            {
                return;
            }

            var sovereign = GetSovereignTitle(kingdom);
            if (sovereign?.Contract == null)
            {
                return;
            }

            var description = GetContractText(sovereign);
            InformationManager.ShowInquiry(new InquiryData(
                $"Enfeoffment Contract for {sovereign.FullName}",
                description, true, false, buttonString, "", null, null));
        }

        public FeudalTitle GetDuchy(FeudalTitle title)
        {

            var suzerain1 = GetImmediateSuzerain(title);
            if (suzerain1 == null)
            {
                return null;
            }

            if (suzerain1.TitleType == TitleType.Dukedom)
            {
                return suzerain1;
            }

            var suzerain2 = GetImmediateSuzerain(suzerain1);
            if (suzerain2 == null)
            {
                return null;
            }

            if (suzerain2.TitleType == TitleType.Dukedom)
            {
                return suzerain2;
            }

            var suzerain3 = GetImmediateSuzerain(suzerain2);
            return suzerain3 is { TitleType: TitleType.Dukedom }
                ? suzerain3
                : null;
        }

        public string GetContractText(FeudalTitle title)
        {
            TextObject text = new TextObject("{=AkTU4Qwg}You, {NAME}, formally accept to be henceforth bound to the {TITLE}, fulfill your duties as well as uphold your rights, what can not be undone by means other than abdication of all rights and lands associated with the contract, treachery, or death.")
                .SetTextVariable("NAME", Hero.MainHero.Name)
                .SetTextVariable("TITLE", title.FullName);

            return text.ToString();
        }

        // Obter modelo de título (para cálculos de custo, etc.)
        public TitleModel GetModel()
        {
            return new TitleModel();
        }

        internal static void BeginCampaign()
        {
            _instance = new TitleManager();
        }

        internal static void SyncCampaign(IDataStore store)
        {
            TitleManager manager = store.IsSaving && Instance.IsInitialized ? Instance : null;
            store.SyncData("CasusBelli_FeudalTitles_v1", ref manager);
            if (store.IsLoading && manager != null)
            {
                _instance = manager;
                manager.Initialize();
            }
        }

        internal void SeedFromFile(string path)
        {
            if (IsInitialized) return;
            bool generate = AddTitlesFromFile(path);
            if (generate)
            {
                foreach (var fief in Settlement.All)
                {
                    if (!fief.IsFortification) continue;
                    var kingdom = fief.OwnerClan?.Kingdom;
                    if (kingdom?.Leader == null || GetTitle(fief) != null) continue;
                    var sovereign = GetSovereignTitle(kingdom) ?? TitleGenerator.CreateKingdom(
                        kingdom.Leader, kingdom, TitleType.Kingdom, new List<FeudalTitle>(),
                        TitleGenerator.GenerateContract("feudal"), kingdom.StringId + "_sovereign");
                    if (sovereign == null) continue;
                    var title = TitleGenerator.CreateLandedTitle(fief, fief.Owner, TextHelper.GetDefaultRank(fief), sovereign.Contract);
                    if (title == null) continue;
                    sovereign.Vassals.Add(title);
                    title.SetSovereign(sovereign);
                }
            }
            if (AllTitles.Count == 0) throw new InvalidOperationException("Nenhum título carregado de " + path);
            Initialize();
        }

        private bool AddTitlesFromFile(string path)
        {
            var doc = new XmlDocument { XmlResolver = null };
            doc.Load(path);
            var root = doc.SelectSingleNode("//titles") ?? throw new InvalidDataException("Elemento titles ausente.");
            FeudalRules.Validate(root);
            foreach (XmlElement node in root.SelectNodes("kingdom"))
            {
                var kingdom = Kingdom.All.FirstOrDefault(k => k.StringId == node.GetAttribute("faction"));
                if (kingdom?.Leader == null) continue;
                string government = FeudalRules.LawId(node.GetAttribute("government"));
                string inheritance = FeudalRules.LawId(node.GetAttribute("inheritance"));
                string gender = FeudalRules.LawId(node.GetAttribute("genderLaw"));
                var contract = TitleGenerator.GenerateContract(government,FeudalRules.SuccessionId(node.GetAttribute("succession")),inheritance,gender);
                var personal = TitleGenerator.GenerateContract(government,"hereditary",inheritance,gender);
                var sovereign = GetSovereignTitle(kingdom) ?? TitleGenerator.CreateKingdom(
                    GetHeroById(node.GetAttribute("deJure")) ?? kingdom.Leader, kingdom, TitleType.Kingdom,
                    new List<FeudalTitle>(), contract, kingdom.StringId + "_sovereign");
                if (sovereign == null) continue;
                foreach (XmlElement duchy in node.SelectNodes("duchy"))
                    LoadTitleHierarchy(duchy, sovereign, sovereign,personal);
            }
            return string.Equals(root.Attributes["autoGenerate"]?.Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private void LoadTitleHierarchy(XmlElement node, FeudalTitle parent, FeudalTitle sovereign, FeudalContract parentContract)
        {
            FeudalTitle title;
            var holder = GetHeroById(node.GetAttribute("deJure"));
            var contract = FeudalRules.Contract(node,parentContract);
            if (node.Name == "duchy")
            {
                string name = node.GetAttribute("name");
                string id = sovereign.StringId + "_" + name.Replace(" ", "_").ToLowerInvariant();
                title = GetTitleByStringId(id);
                if (title == null)
                {
                    title = new FeudalTitle(TitleType.Dukedom, null, null, holder ?? parent.deJure,
                        holder ?? parent.deJure, new TextObject(name), contract, id);
                    ExecuteAddTitle(title);
                }
            }
            else
            {
                var fief = Settlement.Find(node.GetAttribute("settlement"));
                if (fief == null)
                {
                    DebugLog.Log("[TitleSystem] Feudo desconhecido: " + node.GetAttribute("settlement"));
                    return;
                }
                title = GetTitle(fief) ?? TitleGenerator.CreateLandedTitle(fief, holder ?? fief.Owner,
                    node.Name == "county" ? TitleType.County : TitleType.Barony, contract);
            }
            if (title == null) return;
            if (!parent.Vassals.Contains(title)) parent.Vassals.Add(title);
            title.SetSovereign(sovereign);
            foreach (XmlElement child in node.SelectNodes("county|barony"))
                LoadTitleHierarchy(child, title, sovereign,title.Contract);
        }

        private Hero GetHeroById(string heroId)
        {
            if (string.IsNullOrEmpty(heroId)) return null;

            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero.StringId == heroId) return hero;
            }
            foreach (var hero in Hero.DeadOrDisabledHeroes)
            {
                if (hero.StringId == heroId) return hero.Clan?.Leader;
            }
            return null;
        }
    }
}