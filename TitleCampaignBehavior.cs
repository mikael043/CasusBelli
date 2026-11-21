using System;
using System.Collections.Generic;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli.Managers.Titles
{
    // Comportamento de campanha para títulos feudais
    // Integra com o sistema de diplomacia/guerras do CasusBelli
    public class TitleCampaignBehavior : CampaignBehaviorBase
    {
        private bool _initialized = false;

        public override void RegisterEvents()
        {
            DebugLog.Log("TitleCampaignBehavior.RegisterEvents INICIO");
            try
            {
                // NÃO inicializar títulos no OnNewGameCreated - Settlement.All não está pronto
                // Inicialização será feita no OnGameLoadFinished quando o jogo estiver totalmente carregado
                CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(
                    this, OnGameLoadFinished);
                DebugLog.Log("TitleCampaignBehavior.RegisterEvents: OnGameLoadFinished OK");
            }
            catch (Exception ex)
            {
                DebugLog.Log("TitleCampaignBehavior.RegisterEvents EXCEPTION OnGameLoadFinished: " + ex.Message);
            }

            try
            {
                CampaignEvents.HeroKilledEvent.AddNonSerializedListener(
                    this, new Action<Hero, Hero, KillCharacterAction.KillCharacterActionDetail, bool>((victim, killer, detail, isSuicide) => OnHeroKilled(victim, killer, isSuicide)));
                DebugLog.Log("TitleCampaignBehavior.RegisterEvents: HeroKilled OK");
            }
            catch (Exception ex)
            {
                DebugLog.Log("TitleCampaignBehavior.RegisterEvents EXCEPTION HeroKilled: " + ex.Message);
            }

            try
            {
                CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(
                    this, new Action<Clan, Kingdom, Kingdom, ChangeKingdomAction.ChangeKingdomActionDetail, bool>((clan, oldKingdom, newKingdom, detail, initial) => OnClanChangedKingdom(clan, oldKingdom, newKingdom, detail)));
                DebugLog.Log("TitleCampaignBehavior.RegisterEvents: OnClanChangedKingdom OK");
            }
            catch (Exception ex)
            {
                DebugLog.Log("TitleCampaignBehavior.RegisterEvents EXCEPTION OnClanChangedKingdom: " + ex.Message);
            }

            // WarDeclared, MakePeace, OnSettlementOwnerChangedEvent e DailyTickEvent
            // são registrados no CasusBelliCampaignBehavior para evitar duplicação

            DebugLog.Log("TitleCampaignBehavior.RegisterEvents FIM");
        }

        public override void SyncData(IDataStore dataStore)
        {
            // O SaveSystem do Bannerlord serializa automaticamente as propriedades [SaveableProperty]
            // do TitleManager (que é uma instância singleton)
            // Não precisa de SyncData manual - as propriedades são salvas automaticamente
        }

        private void OnGameLoadFinished()
        {
            try
            {
                DebugLog.Log("[TitleSystem] OnGameLoadFinished CALLED, _initialized=" + _initialized);
                if (!_initialized)
                {
                    InitializeTitles();
                }
                else
                {
                    TitleManager.Instance.RefreshCaches();
                    TitleManager.Instance.PostInitialize();
                }
            }
            catch (Exception ex)
            {
                DebugLog.Log("[TitleSystem] OnGameLoadFinished EXCEPTION: " + ex.Message);
            }
        }

        private void InitializeTitles()
        {
            DebugLog.Log("[TitleSystem] InitializeTitles START, _initialized=" + _initialized);
            if (_initialized) return;
            _initialized = true;

            // Inicializar títulos para todos os feudos existentes
            // Adicionar try-catch defensivo para evitar crash no sandbox
            try
            {
                DebugLog.Log("[TitleSystem] Settlement.All count = " + (Settlement.All?.Count ?? -1));
                // Verificar se Settlement.All está disponível
                if (Settlement.All == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage("[TitleSystem] Settlement.All is null, skipping title initialization"));
                    DebugLog.Log("[TitleSystem] Settlement.All is null, returning");
                    return;
                }

                int fortCount = 0;
                foreach (Settlement settlement in Settlement.All)
                {
                    if (settlement != null && settlement.IsFortification)
                        fortCount++;
                }
                DebugLog.Log("[TitleSystem] Fortification count = " + fortCount);

                foreach (Settlement settlement in Settlement.All)
                {
                    try
                    {
                        DebugLog.Log("[TitleSystem] Checking settlement: " + settlement?.Name + " IsFortification=" + settlement?.IsFortification + " OwnerClan=" + settlement?.OwnerClan?.Name + " Kingdom=" + settlement?.OwnerClan?.Kingdom?.Name);
                        if (settlement == null || !settlement.IsFortification)
                            continue;

                        if (settlement.OwnerClan == null || settlement.OwnerClan.Kingdom == null)
                        {
                            DebugLog.Log("[TitleSystem] Skipping - OwnerClan or Kingdom is null");
                            continue;
                        }

                        var title = TitleManager.Instance.GetTitle(settlement);
                        if (title == null)
                        {
                            DebugLog.Log("[TitleSystem] No title found, creating for: " + settlement.Name);
                            CreateTitleForSettlement(settlement);
                        }
                        else
                        {
                            DebugLog.Log("[TitleSystem] Title already exists: " + title.FullName);
                        }
                    }
                    catch (Exception ex)
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error creating title for settlement: {ex.Message}"));
                    }
                }

                // Processar drift de jure diário
                ProcessDeJureDrift();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in InitializeTitles: {ex.Message}"));
            }
        }

        private void CreateTitleForSettlement(Settlement settlement)
        {
            try
            {
                if (settlement == null) return;

                Hero deJure = settlement.Owner;
                if (deJure == null || deJure.Clan == null || deJure.Clan.Kingdom == null)
                    return;

                Kingdom kingdom = deJure.Clan.Kingdom;
                TitleType rank = TextHelper.GetDefaultRank(settlement);

                // Obter ou criar contrato do soberano
                var sovereignTitle = TitleManager.Instance.GetSovereignTitle(kingdom);
                FeudalContract contract;

                if (sovereignTitle?.Contract != null)
                {
                    contract = sovereignTitle.Contract;
                }
                else
                {
                    try
                    {
                        contract = TitleGenerator.GenerateContract("feudal");
                    }
                    catch (Exception ex)
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Erro gerando contrato: {ex.Message}"));
                        return;
                    }
                }

                if (contract == null) return;

                // Criar título
                var title = TitleGenerator.CreateLandedTitle(settlement, deJure, rank, contract);

                // Se é um reino sem título soberano, criar um
                if (sovereignTitle == null && kingdom.Leader != null)
                {
                    var kingdomTitle = TitleGenerator.CreateKingdom(
                        kingdom.Leader, kingdom, TitleType.Kingdom,
                        new List<FeudalTitle>(), contract);

                    // Associar títulos existentes ao novo soberano
                    if (kingdomTitle != null)
                    {
                        foreach (var t in TitleManager.Instance.AllTitles)
                        {
                            if (t != null && t.Sovereign == null && t.deJure?.Clan?.Kingdom == kingdom)
                            {
                                try
                                {
                                    t.DriftTitle(kingdomTitle);
                                }
                                catch (Exception ex)
                                {
                                    InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Erro drift título: {ex.Message}"));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in CreateTitleForSettlement: {ex.Message}"));
            }
        }

        private void OnDailyTick()
        {
            try
            {
                if (!_initialized) return;

                // Tick de claims em todos os títulos
                foreach (var title in TitleManager.Instance.AllTitles)
                {
                    try
                    {
                        title.TickClaims();
                        title.CleanClaims();
                    }
                    catch (Exception ex)
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in TickClaims: {ex.Message}"));
                    }
                }

                // Processar drift de jure
                ProcessDeJureDrift();

                // Verificar claims maduros e gerar Casus Belli
                CheckMatureClaimsForCasusBelli();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in OnDailyTick: {ex.Message}"));
            }
        }

        private void ProcessDeJureDrift()
        {
            try
            {
                foreach (var title in TitleManager.Instance.AllTitles)
                {
                    if (title.Sovereign != null && title.TitleType >= TitleType.Kingdom)
                    {
                        title.TickDrift(title.Sovereign);
                    }
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in ProcessDeJureDrift: {ex.Message}"));
            }
        }

        private void CheckMatureClaimsForCasusBelli()
        {
            try
            {
                // Integrar com sistema de Casus Belli do mod
                // Claims maduros em títulos de outros reinos geram CB
                foreach (var title in TitleManager.Instance.AllTitles)
                {
                    if (title.deJure == null || title.deJure.Clan?.Kingdom == null)
                        continue;

                    var targetKingdom = title.deJure.Clan.Kingdom;

                    // Verificar claims de outros reinos
                    foreach (var claim in title.Claims)
                    {
                        if (claim.Value == ClaimType.Fabricated || claim.Value == ClaimType.PreviousOwner)
                        {
                            var claimant = claim.Key;
                            if (claimant.Clan?.Kingdom != null && claimant.Clan.Kingdom != targetKingdom)
                            {
                                // Gerar Casus Belli se não existir
                                GenerateCasusBelliFromClaim(claimant.Clan.Kingdom, targetKingdom, title);
                            }
                        }
                    }

                    // Claim de facto do detentor atual
                    var deFacto = title.DeFactoHolder;
                    if (deFacto != null && deFacto.Clan?.Kingdom != null && deFacto.Clan.Kingdom != targetKingdom)
                    {
                        if (!title.Claims.ContainsKey(deFacto))
                        {
                            title.AddClaim(deFacto, ClaimType.DeFacto);
                            GenerateCasusBelliFromClaim(deFacto.Clan.Kingdom, targetKingdom, title);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in CheckMatureClaimsForCasusBelli: {ex.Message}"));
            }
        }

        private void GenerateCasusBelliFromClaim(Kingdom attacker, Kingdom defender, FeudalTitle title)
        {
            // Integração com sistema de Casus Belli existente
            // Isso criaria um CasusBelliRecord ou similar
            try
            {
                var behavior = Campaign.Current.GetCampaignBehavior<CasusBelliCampaignBehavior>();
                if (behavior != null)
                {
                    // O comportamento CasusBelliCampaignBehavior tem método para criar claims/guerras
                    // Aqui apenas logamos para debug
                    InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Claim on {title.FullName} by {attacker.Name} against {defender.Name} - would generate CB"));
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error generating CB from claim: {ex.Message}"));
            }
        }

        private void OnHeroKilled(Hero victim, Hero killer, bool isSuicide)
        {
            try
            {
                if (victim == null) return;

                // Herdar títulos
                var heir = GetHeir(victim);
                if (heir != null)
                {
                    TitleManager.Instance.InheritAllTitles(victim, heir);
                }
                else
                {
                    // Sem herdeiro - títulos ficam vagos
                    var titles = TitleManager.Instance.GetAllDeJure(victim);
                    foreach (var title in titles)
                    {
                        TitleManager.Instance.ExecuteOwnershipChange(victim, null, title, true);
                    }
                }

                // Remover claims do morto
                foreach (var title in TitleManager.Instance.AllTitles)
                {
                    title.RemoveClaim(victim);
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in OnHeroKilled: {ex.Message}"));
            }
        }

        private Hero GetHeir(Hero deadHero)
        {
            try
            {
                if (deadHero.Clan == null) return null;

                // Usar sucessão do título mais alto
                var highestTitle = TitleManager.Instance.GetHighestTitle(deadHero);
                if (highestTitle?.Contract?.Succession != null)
                {
                    var candidates = highestTitle.Contract.Succession.GetSuccessionCandidates(deadHero, highestTitle);
                    var bestScore = float.MinValue;
                    Hero bestCandidate = null;

                    foreach (var candidate in candidates)
                    {
                        var score = TitleManager.Instance.GetModel().GetSuccessionHeirScore(deadHero, candidate, highestTitle, false);
                        if (score.ResultNumber > bestScore)
                        {
                            bestScore = score.ResultNumber;
                            bestCandidate = candidate;
                        }
                    }

                    return bestCandidate;
                }

                // Fallback: líder do clã
                return deadHero.Clan.Leader != deadHero ? deadHero.Clan.Leader : null;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in GetHeir: {ex.Message}"));
                return null;
            }
        }

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail)
        {
            try
            {
                if (clan == null) return;

                // Se entrou em novo reino, dar senhorio se elegível
                if (newKingdom != null && oldKingdom != null)
                {
                    TitleManager.Instance.GiveLordshipOnKingdomJoin(newKingdom, clan);
                }

                // Atualizar títulos do clã
                foreach (var hero in clan.Heroes)
                {
                    var titles = TitleManager.Instance.GetAllDeJure(hero);
                    foreach (var title in titles)
                    {
                        if (title.deJure == hero)
                        {
                            // Título permanece com o herói, mas reino mudou
                            // Drift pode acontecer ao longo do tempo
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in OnClanChangedKingdom: {ex.Message}"));
            }
        }
    }
}