using System;
using System.Collections.Generic;
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
                CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, starter => EnsureTitlesReady());
                CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, starter => EnsureTitlesReady());
                CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
                CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(
                    this, EnsureTitlesReady);
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

            DebugLog.Log("TitleCampaignBehavior.RegisterEvents FIM");
        }

        public override void SyncData(IDataStore dataStore)
        {
            TitleManager.SyncCampaign(dataStore);
        }

        private void EnsureTitlesReady()
        {
            if (_initialized || Settlement.All.Count == 0 || Kingdom.All.Count == 0) return;
            var manager = TitleManager.Instance;
            try
            {
                if (!manager.IsInitialized)
                {
                    string path = System.IO.Path.Combine(TaleWorlds.ModuleManager.ModuleHelper.GetModuleFullPath("CasusBelli"),
                        "ModuleData", "titles.xml");
                    manager.SeedFromFile(path);
                    DebugLog.Log("[TitleSystem] " + manager.AllTitles.Count + " títulos inicializados a partir de " + path);
                }
                manager.PostInitialize();
                _initialized = true;
            }
            catch (Exception ex)
            {
                DebugLog.Log("[TitleSystem] Falha ao inicializar títulos: " + ex);
                InformationManager.DisplayMessage(new InformationMessage("Falha ao carregar títulos: " + ex.Message));
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
                    var current = TitleManager.Instance.GetSovereignTitle(title.Fief?.OwnerClan?.Kingdom);
                    if (current != null && title.Sovereign != current && title.Fief?.IsFortification == true)
                    {
                        title.TickDrift(current);
                    }
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in ProcessDeJureDrift: {ex.Message}"));
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