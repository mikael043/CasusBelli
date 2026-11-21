using System;
using System.Linq;
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
                CampaignEvents.RulingClanChanged.AddNonSerializedListener(this, (realm, clan) => SyncCrown(realm));
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

                foreach (var realm in Kingdom.All) SyncCrown(realm);
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
                var manager = TitleManager.Instance;
                var crowns = Kingdom.All.Where(k => !k.IsEliminated).ToDictionary(k => k,k => manager.GetSovereignTitle(k));
                foreach (var title in manager.AllTitles)
                {
                    if (title.Fief?.IsFortification != true) continue;
                    var realm = title.Fief.OwnerClan?.Kingdom;
                    title.TickDrift(realm != null && crowns.TryGetValue(realm,out var crown) ? crown : null);
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

                var manager = TitleManager.Instance;
                int partition = 0;
                foreach (var title in manager.GetAllDeJure(victim).OrderBy(t => t.TitleType).ThenBy(t => t.StringId,StringComparer.Ordinal))
                {
                    if (title.IsSovereignLevel) continue;
                    var heir = FeudalRules.PersonalHeir(victim,title,partition);
                    if (title.Contract?.Inheritance?.StringId == "gavelkind") partition++;
                    manager.ExecuteOwnershipChange(victim,heir,title,true);
                    if (title.deFacto == victim)
                        manager.ExecuteOwnershipChange(victim,title.Fief == null ? heir : title.Fief.OwnerClan?.Leader,title,false);
                }

                // Remover claims do morto
                foreach (var title in TitleManager.Instance.AllTitles)
                {
                    title.RemoveClaim(victim);
                    if (!title.IsSovereignLevel && title.deFacto == victim)
                        manager.ExecuteOwnershipChange(victim,title.Fief?.OwnerClan?.Leader == victim ? null : title.Fief?.OwnerClan?.Leader,title,false);
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in OnHeroKilled: {ex.Message}"));
            }
        }

        private static void SyncCrown(Kingdom realm)
        {
            var manager = TitleManager.Instance;
            if (realm == null || !manager.IsInitialized) return;
            var crowns = manager.GetSovereignTitleList(realm);
            if (realm.IsEliminated)
            {
                foreach (var abandoned in crowns.Where(t => t.Active)) manager.DeactivateTitle(abandoned);
                return;
            }
            if (realm.Leader == null || !realm.Leader.IsAlive) return;
            if (crowns.Count == 0 && realm.Fiefs.Count > 0)
            {
                RoyalSeat.Move(realm);
                TitleGenerator.CreateKingdom(realm.Leader,realm,TitleType.Kingdom,new System.Collections.Generic.List<FeudalTitle>(),
                    TitleGenerator.GenerateContract("feudal"),realm.StringId + "_sovereign");
            }
            foreach (var crown in crowns)
            {
                if (crown.deJure != realm.Leader) manager.ExecuteOwnershipChange(crown.deJure,realm.Leader,crown,true);
                if (crown.deFacto != realm.Leader) manager.ExecuteOwnershipChange(crown.deFacto,realm.Leader,crown,false);
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

            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TitleSystem] Error in OnClanChangedKingdom: {ex.Message}"));
            }
        }
    }
}