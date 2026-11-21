using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli
{
    public partial class CasusBelliCampaignBehavior : CampaignBehaviorBase
    {
        private WarRegistry _warRegistry = new WarRegistry();
        private SettlementOwnershipRegistry _settlementOwnershipRegistry =
            new SettlementOwnershipRegistry();
        private List<string> _savedWarData = new List<string>();
        private List<string> _eligiblePairData = new List<string>();
        private List<string> _savedSettlementOwnershipData =
            new List<string>();
        private PendingClaimWar _pendingClaimWar = null;

        // Guarda, so durante a chamada de DeclareWarAction, o motivo da guerra
        // para o OnWarDeclared registrar a ficha com o tipo certo.
        private class PendingClaimWar
        {
            public string AttackerKingdomId = "";
            public string DefenderKingdomId = "";
            public string SettlementId = "";
            public string ClaimId = "";
        }

        private HashSet<string> _eligiblePairs =
            new HashSet<string>(StringComparer.Ordinal);

        private bool _diplomacyInitialized = false;
        private int _nextWarSequence = 1;
        private int _nextSettlementOwnershipSequence = 1;

        private const int FieldsPerWar = 6;
        private const int FieldsPerSettlementOwnershipRecord = 11;
        private const string ClaimWarCasusBelliType = "CLAIM";

        public override void RegisterEvents()
        {
            DebugLog.Log("CasusBelliCampaignBehavior.RegisterEvents INICIO");
            try
            {
                CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
                DebugLog.Log("CasusBelliCampaignBehavior.RegisterEvents: WarDeclared OK");
            }
            catch (Exception ex)
            {
                DebugLog.Log("CasusBelliCampaignBehavior.RegisterEvents EXCEPTION WarDeclared: " + ex.Message);
            }
            try
            {
                CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
                DebugLog.Log("CasusBelliCampaignBehavior.RegisterEvents: MakePeace OK");
            }
            catch (Exception ex2)
            {
                DebugLog.Log("CasusBelliCampaignBehavior.RegisterEvents EXCEPTION MakePeace: " + ex2.Message);
            }
            try
            {
                CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
                DebugLog.Log("CasusBelliCampaignBehavior.RegisterEvents: SettlementOwnerChanged OK");
            }
            catch (Exception ex3)
            {
                DebugLog.Log("CasusBelliCampaignBehavior.RegisterEvents EXCEPTION SettlementOwnerChanged: " + ex3.Message);
            }
            try
            {
                CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
                DebugLog.Log("CasusBelliCampaignBehavior.RegisterEvents: DailyTick OK");
            }
            catch (Exception ex4)
            {
                DebugLog.Log("CasusBelliCampaignBehavior.RegisterEvents EXCEPTION DailyTick: " + ex4.Message);
            }
            try
            {
                CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, OnNewGameCreatedFinished);
                DebugLog.Log("CasusBelliCampaignBehavior.RegisterEvents: OnNewGameCreatedFinished OK");
            }
            catch (Exception ex5)
            {
                DebugLog.Log("CasusBelliCampaignBehavior.RegisterEvents EXCEPTION OnNewGameCreatedFinished: " + ex5.Message);
            }
            try
            {
                CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
                DebugLog.Log("CasusBelliCampaignBehavior.RegisterEvents: OnGameLoadFinished OK");
            }
            catch (Exception ex6)
            {
                DebugLog.Log("CasusBelliCampaignBehavior.RegisterEvents EXCEPTION OnGameLoadFinished: " + ex6.Message);
            }
            DebugLog.Log("CasusBelliCampaignBehavior.RegisterEvents FIM");
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving) SyncArmistices(dataStore);
            if (dataStore.IsSaving)
            {
                SaveWarRecordsToData();
                _savedSettlementOwnershipData.Clear();
                foreach (SettlementOwnershipRecord allRecord in _settlementOwnershipRegistry.AllRecords)
                {
                    _savedSettlementOwnershipData.Add(allRecord.RecordId ?? "");
                    _savedSettlementOwnershipData.Add(allRecord.SettlementId ?? "");
                    _savedSettlementOwnershipData.Add(allRecord.OldOwnerHeroId ?? "");
                    _savedSettlementOwnershipData.Add(allRecord.NewOwnerHeroId ?? "");
                    _savedSettlementOwnershipData.Add(allRecord.OldOwnerClanId ?? "");
                    _savedSettlementOwnershipData.Add(allRecord.NewOwnerClanId ?? "");
                    _savedSettlementOwnershipData.Add(allRecord.OldOwnerKingdomId ?? "");
                    _savedSettlementOwnershipData.Add(allRecord.NewOwnerKingdomId ?? "");
                    _savedSettlementOwnershipData.Add(allRecord.CapturerHeroId ?? "");
                    _savedSettlementOwnershipData.Add(allRecord.ChangeReason ?? "");
                    _savedSettlementOwnershipData.Add(allRecord.CampaignTime ?? "");
                }
                _eligiblePairData.Clear();
                foreach (string eligiblePair in _eligiblePairs)
                {
                    _eligiblePairData.Add(eligiblePair);
                }
            }
            dataStore.SyncData("CasusBelli_WarData_v2", ref _savedWarDataV2);
            if (_savedWarData == null)
            {
                _savedWarData = new List<string>();
            }

            if (dataStore.IsLoading)
            {
                // formato antigo: lido so para migrar, nunca mais gravado
                dataStore.SyncData(
                    "CasusBelli_WarData_v1",
                    ref _savedWarData);
            }

            dataStore.SyncData(
                "CasusBelli_SettlementOwnershipData_v1",
                ref _savedSettlementOwnershipData);

            dataStore.SyncData(
                "CasusBelli_EligiblePairs_v1",
                ref _eligiblePairData);

            dataStore.SyncData(
                "CasusBelli_DiplomacyInitialized_v1",
                ref _diplomacyInitialized);

            dataStore.SyncData(
                "CasusBelli_NextWarSequence_v1",
                ref _nextWarSequence);

            dataStore.SyncData(
                "CasusBelli_NextSettlementOwnershipSequence_v1",
                ref _nextSettlementOwnershipSequence);

            if (dataStore.IsLoading)
            {
                _warRegistry.Clear();

                _settlementOwnershipRegistry.Clear();

                _eligiblePairs.Clear();

                LoadWarRecordsFromData();
                SyncArmistices(dataStore);

                if (_savedSettlementOwnershipData == null)
                {
                    _savedSettlementOwnershipData = new List<string>();
                }

                for (int i = 0;
                     i + FieldsPerSettlementOwnershipRecord <=
                         _savedSettlementOwnershipData.Count;
                     i += FieldsPerSettlementOwnershipRecord)
                {
                    SettlementOwnershipRecord record =
                        new SettlementOwnershipRecord
                        {
                            RecordId = _savedSettlementOwnershipData[i],
                            SettlementId = _savedSettlementOwnershipData[i + 1],
                            OldOwnerHeroId = _savedSettlementOwnershipData[i + 2],
                            NewOwnerHeroId = _savedSettlementOwnershipData[i + 3],
                            OldOwnerClanId = _savedSettlementOwnershipData[i + 4],
                            NewOwnerClanId = _savedSettlementOwnershipData[i + 5],
                            OldOwnerKingdomId = _savedSettlementOwnershipData[i + 6],
                            NewOwnerKingdomId = _savedSettlementOwnershipData[i + 7],
                            CapturerHeroId = _savedSettlementOwnershipData[i + 8],
                            ChangeReason = _savedSettlementOwnershipData[i + 9],
                            CampaignTime = _savedSettlementOwnershipData[i + 10]
                        };

                    _settlementOwnershipRegistry.AddRecord(record);
                }

                if (_eligiblePairData == null)
                {
                    _eligiblePairData = new List<string>();
                }

                foreach (string pair in _eligiblePairData)
                {
                    if (!string.IsNullOrWhiteSpace(pair))
                    {
                        _eligiblePairs.Add(pair);
                    }
                }

                if (_nextWarSequence < 1 || _nextWarSequence > 1000000)
                {
                    _nextWarSequence = 1;
                }

                if (_nextSettlementOwnershipSequence < 1 || _nextSettlementOwnershipSequence > 1000000)
                {
                    _nextSettlementOwnershipSequence = 1;
                }

                SanitizeLoadedData();
            }
        }

        private void SanitizeLoadedData()
        {
            try
            {
                foreach (WarRecord warRecord in _warRegistry.AllWars.ToArray())
                {
                    if (warRecord == null || string.IsNullOrEmpty(warRecord.WarId))
                    {
                        DebugLog.Log("SanitizeLoadedData: removendo guerra inválida");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog.Log("SanitizeLoadedData EXCEPTION: " + ex.Message);
            }
        }

        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            try
            {
                if (settlement != null)
                {
                    SettlementOwnershipRecord record = new SettlementOwnershipRecord
                    {
                        RecordId = "CB_SETTLEMENT_OWNERSHIP_" + _nextSettlementOwnershipSequence,
                        SettlementId = (settlement.StringId ?? ""),
                        OldOwnerHeroId = GetHeroId(oldOwner),
                        NewOwnerHeroId = GetHeroId(newOwner),
                        OldOwnerClanId = GetClanId(oldOwner),
                        NewOwnerClanId = GetClanId(newOwner),
                        OldOwnerKingdomId = GetKingdomId(oldOwner),
                        NewOwnerKingdomId = GetKingdomId(newOwner),
                        CapturerHeroId = GetHeroId(capturerHero),
                        ChangeReason = detail.ToString(),
                        CampaignTime = CampaignTime.Now.ToString()
                    };
                    SettlementOwnershipRegistry settlementOwnershipRegistry = _settlementOwnershipRegistry;
                    if (settlementOwnershipRegistry != null && settlementOwnershipRegistry.AddRecord(record))
                    {
                        _nextSettlementOwnershipSequence++;
                    }
                    MarkClaimWarObjective(settlement, newOwner);
                    TitleManager.Instance?.ApplyOwnerChange(settlement, newOwner);
                }
            }
            catch (Exception ex)
            {
                DebugLog.Log("OnSettlementOwnerChanged EXCEPTION: " + ex.Message);
            }
        }

        private void OnDailyTick()
        {

            RunDailyAi();
        }

        private Kingdom GetKingdom(string kingdomId)
        {
            if (string.IsNullOrEmpty(kingdomId))
            {
                return null;
            }

            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom != null && kingdom.StringId == kingdomId)
                {
                    return kingdom;
                }
            }

            return null;
        }

        private string GetHeroId(Hero hero)
        {
            if (hero == null)
            {
                return "";
            }

            return hero.StringId ?? "";
        }

        private string GetClanId(Hero hero)
        {
            if (hero == null || hero.Clan == null)
            {
                return "";
            }

            return hero.Clan.StringId ?? "";
        }

        private string GetKingdomId(Hero hero)
        {
            if (hero == null ||
                hero.Clan == null ||
                hero.Clan.Kingdom == null)
            {
                return "";
            }

            return hero.Clan.Kingdom.StringId ?? "";
        }

        private void OnNewGameCreatedFinished(
            CampaignGameStarter starter)
        {
            InitializeDiplomacyTracking();
        }

        private void OnGameLoadFinished()
        {
            InitializeDiplomacyTracking();
        }

        private void InitializeDiplomacyTracking()
        {
            if (_diplomacyInitialized)
            {
                return;
            }

            _eligiblePairs.Clear();

            foreach (Kingdom kingdom1 in Kingdom.All)
            {
                if (kingdom1 == null ||
                    string.IsNullOrEmpty(kingdom1.StringId))
                {
                    continue;
                }

                foreach (Kingdom kingdom2 in Kingdom.All)
                {
                    if (kingdom2 == null ||
                        kingdom1 == kingdom2 ||
                        string.IsNullOrEmpty(kingdom2.StringId))
                    {
                        continue;
                    }

                    string pairKey = GetPairKey(
                        kingdom1.StringId,
                        kingdom2.StringId);

                    if (!kingdom1.IsAtWarWith(kingdom2))
                    {
                        _eligiblePairs.Add(pairKey);
                    }
                }
            }

            RegisterExistingWarsWithoutCasusBelli();

            _diplomacyInitialized = true;
        }

        private void RegisterExistingWarsWithoutCasusBelli()
        {
            foreach (Kingdom kingdom1 in Kingdom.All)
            {
                if (kingdom1 == null ||
                    string.IsNullOrEmpty(kingdom1.StringId))
                {
                    continue;
                }

                foreach (Kingdom kingdom2 in Kingdom.All)
                {
                    if (kingdom2 == null ||
                        kingdom1 == kingdom2 ||
                        string.IsNullOrEmpty(kingdom2.StringId))
                    {
                        continue;
                    }

                    if (string.Compare(
                        kingdom1.StringId,
                        kingdom2.StringId,
                        StringComparison.Ordinal) >= 0)
                    {
                        continue;
                    }

                    if (!kingdom1.IsAtWarWith(kingdom2))
                    {
                        continue;
                    }

                    bool alreadyRegistered = false;

                    foreach (WarRecord existingWar in _warRegistry.AllWars)
                    {
                        bool samePair =
                            (existingWar.AttackerKingdomId == kingdom1.StringId &&
                             existingWar.DefenderKingdomId == kingdom2.StringId) ||
                            (existingWar.AttackerKingdomId == kingdom2.StringId &&
                             existingWar.DefenderKingdomId == kingdom1.StringId);

                        if (samePair && existingWar.IsActive)
                        {
                            alreadyRegistered = true;
                            break;
                        }
                    }

                    if (alreadyRegistered)
                    {
                        continue;
                    }

                    WarRecord war = new WarRecord
                    {
                        WarId = "CB_WAR_" + _nextWarSequence,
                        AttackerKingdomId = kingdom1.StringId,
                        DefenderKingdomId = kingdom2.StringId,
                        CasusBelliType = "UNASSIGNED",
                        TargetSettlementId = "",
                        IsActive = true
                    };

                    if (_warRegistry.AddWar(war))
                    {
                        _nextWarSequence++;
                    }
                }
            }
        }

        private void OnMakePeace(
            IFaction faction1,
            IFaction faction2,
            TaleWorlds.CampaignSystem.Actions
                .MakePeaceAction.MakePeaceDetail detail)
        {
            string pairKey = GetPairKey(faction1, faction2);

            if (pairKey == null)
            {
                return;
            }

            _eligiblePairs.Add(pairKey);

            foreach (WarRecord war in _warRegistry.AllWars)
            {
                if (!war.IsActive)
                {
                    continue;
                }

                bool samePair =
                    (war.AttackerKingdomId == faction1.StringId &&
                     war.DefenderKingdomId == faction2.StringId) ||
                    (war.AttackerKingdomId == faction2.StringId &&
                     war.DefenderKingdomId == faction1.StringId);

                if (samePair)
                {
                    if (war.ArmisticeEnds >= 0) continue;
                    war.IsActive = false;
                    FinalizeWar(war);
                }
            }
        }

        private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
        {
            try
            {
                if (!_diplomacyInitialized)
                {
                    return;
                }
                string pairKey = GetPairKey(faction1, faction2);
                if (faction1 is Kingdom first && faction2 is Kingdom second &&
                    TryGetActiveWarBetween(first.StringId, second.StringId, out var existing))
                {
                    if (existing.ArmisticeEnds > CampaignTime.Now.ToDays && first.IsAtWarWith(second))
                        MakePeaceAction.Apply(first, second);
                    return;
                }
                if (pairKey == null || !_eligiblePairs.Contains(pairKey))
                {
                    return;
                }
                _eligiblePairs.Remove(pairKey);
                Kingdom kingdom = faction1 as Kingdom;
                Kingdom kingdom2 = faction2 as Kingdom;
                if (kingdom == null || kingdom2 == null || string.IsNullOrEmpty(kingdom.StringId) || string.IsNullOrEmpty(kingdom2.StringId))
                {
                    return;
                }
                string text = "UNASSIGNED";
                string targetSettlementId = "";
                string claimId = "";
                FeudalTitle bestClaim;
                Settlement bestSettlement;
                if (_pendingClaimWar != null && _pendingClaimWar.AttackerKingdomId == kingdom.StringId && _pendingClaimWar.DefenderKingdomId == kingdom2.StringId)
                {
                    text = "CLAIM";
                    targetSettlementId = _pendingClaimWar.SettlementId ?? "";
                    claimId = _pendingClaimWar.ClaimId ?? "";
                }
                else if (TryFindBestClaimAgainst(kingdom, kingdom2, out bestClaim, out bestSettlement) && bestSettlement != null && bestClaim != null)
                {
                    text = "CLAIM";
                    targetSettlementId = bestSettlement.StringId;
                    claimId = bestClaim.StringId;
                }
                WarRecord war = new WarRecord
                {
                    WarId = "CB_WAR_" + _nextWarSequence,
                    AttackerKingdomId = kingdom.StringId,
                    DefenderKingdomId = kingdom2.StringId,
                    CasusBelliType = text,
                    TargetSettlementId = targetSettlementId,
                    IsActive = true,
                    StartedOnDay = CampaignTime.Now.ToDays,
                    ClaimId = claimId
                };
                if (_warRegistry.AddWar(war))
                {
                    _nextWarSequence++;
                    if (text == "UNASSIGNED")
                    {
                        ApplyUnassignedWarPenalty(kingdom);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog.Log("OnWarDeclared EXCEPTION: " + ex.Message);
            }
        }

        private string GetPairKey(
            IFaction faction1,
            IFaction faction2)
        {
            Kingdom kingdom1 = faction1 as Kingdom;
            Kingdom kingdom2 = faction2 as Kingdom;

            if (kingdom1 == null || kingdom2 == null)
            {
                return null;
            }

            return GetPairKey(
                kingdom1.StringId,
                kingdom2.StringId);
        }

        private string GetPairKey(
            string kingdomId1,
            string kingdomId2)
        {
            if (string.IsNullOrEmpty(kingdomId1) ||
                string.IsNullOrEmpty(kingdomId2))
            {
                return null;
            }

            if (string.Compare(
                kingdomId1,
                kingdomId2,
                StringComparison.Ordinal) < 0)
            {
                return kingdomId1 + "|" + kingdomId2;
            }

            return kingdomId2 + "|" + kingdomId1;
        }

        public WarRegistry GetWarRegistry()
        {
            return _warRegistry;
        }

        public TitleManager GetTitleManager()
        {
            return TitleManager.Instance;
        }

        public string BuildKingdomTitlesText(string kingdomId)
        {
            return TitleManager.Instance.BuildKingdomTitlesText(kingdomId);
        }

        public bool CreateTestWar()
        {
            WarRecord war = new WarRecord
            {
                WarId = "CASUS_BELLI_TEST_001",
                AttackerKingdomId = "TEST_ATTACKER",
                DefenderKingdomId = "TEST_DEFENDER",
                CasusBelliType = "TEST",
                TargetSettlementId = "TEST_SETTLEMENT",
                IsActive = true
            };

            return _warRegistry.AddWar(war);
        }
    }
}
