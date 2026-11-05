
using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CasusBelli
{
    public partial class CasusBelliCampaignBehavior : CampaignBehaviorBase
    {
        private WarRegistry _warRegistry = new WarRegistry();
        private SettlementOwnershipRegistry _settlementOwnershipRegistry =
            new SettlementOwnershipRegistry();
        private SettlementDeJureRegistry _settlementDeJureRegistry =
            new SettlementDeJureRegistry();

        private List<string> _savedWarData = new List<string>();
        private List<string> _eligiblePairData = new List<string>();
        private List<string> _savedSettlementOwnershipData =
            new List<string>();
        private List<string> _savedSettlementDeJureData =
            new List<string>();
        private List<string> _savedSettlementDeJureDataV1 =
            new List<string>();

        private ClaimRegistry _claimRegistry = new ClaimRegistry();
        private List<string> _savedClaimData = new List<string>();
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
        private bool _deJureOwnershipInitialized = false;
        private bool _deJureOwnershipInitializedV1 = false;
        private int _nextWarSequence = 1;
        private int _nextSettlementOwnershipSequence = 1;

        private const int FieldsPerWar = 6;
        private const int FieldsPerSettlementOwnershipRecord = 11;
        private const int FieldsPerSettlementDeJureRecord = 5;
        private const int FieldsPerSettlementDeJureRecordV1 = 4;
        private const double ActiveUsurpationDays = 42.0;
        private const double PassiveUsurpationDays = 100.0;
        private const int FieldsPerClaim = 8;
        private const double ClaimKeepDays = 30.0;
        private const string ClaimWarCasusBelliType = "CLAIM";

        public override void RegisterEvents()
        {
            CampaignEvents.WarDeclared.AddNonSerializedListener(
                this,
                OnWarDeclared);

            CampaignEvents.MakePeace.AddNonSerializedListener(
                this,
                OnMakePeace);

            CampaignEvents.OnSettlementOwnerChangedEvent
                .AddNonSerializedListener(
                    this,
                    OnSettlementOwnerChanged);
     
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(
                this,
                OnDailyTick);

            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent
                .AddNonSerializedListener(
                    this,
                    OnNewGameCreatedFinished);

            CampaignEvents.OnGameLoadFinishedEvent
                .AddNonSerializedListener(
                    this,
                    OnGameLoadFinished);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                SaveWarRecordsToData();
                SaveFabricationProjects();

                _savedSettlementOwnershipData.Clear();

                foreach (SettlementOwnershipRecord record in
                    _settlementOwnershipRegistry.AllRecords)
                {
                    _savedSettlementOwnershipData.Add(record.RecordId ?? "");
                    _savedSettlementOwnershipData.Add(record.SettlementId ?? "");
                    _savedSettlementOwnershipData.Add(record.OldOwnerHeroId ?? "");
                    _savedSettlementOwnershipData.Add(record.NewOwnerHeroId ?? "");
                    _savedSettlementOwnershipData.Add(record.OldOwnerClanId ?? "");
                    _savedSettlementOwnershipData.Add(record.NewOwnerClanId ?? "");
                    _savedSettlementOwnershipData.Add(record.OldOwnerKingdomId ?? "");
                    _savedSettlementOwnershipData.Add(record.NewOwnerKingdomId ?? "");
                    _savedSettlementOwnershipData.Add(record.CapturerHeroId ?? "");
                    _savedSettlementOwnershipData.Add(record.ChangeReason ?? "");
                    _savedSettlementOwnershipData.Add(record.CampaignTime ?? "");
                }

                _savedSettlementDeJureData.Clear();

                foreach (SettlementDeJureRecord record in
                    _settlementDeJureRegistry.AllRecords)
                {
                    _savedSettlementDeJureData.Add(record.SettlementId ?? "");
                    _savedSettlementDeJureData.Add(record.DeJureKingdomId ?? "");
                    _savedSettlementDeJureData.Add(record.DeFactoKingdomId ?? "");
                    _savedSettlementDeJureData.Add(
                        record.OccupationStartedOnDay.ToString(
                            "R",
                            CultureInfo.InvariantCulture));
                    _savedSettlementDeJureData.Add(
                        record.ActiveUsurpationStartedOnDay.ToString(
                            "R",
                            CultureInfo.InvariantCulture));
                }

                if (_savedClaimData == null)
                {
                    _savedClaimData = new List<string>();
                }

                _savedClaimData.Clear();

                foreach (ClaimRecord claim in _claimRegistry.AllRecords)
                {
                    _savedClaimData.Add(claim.ClaimId ?? "");
                    _savedClaimData.Add(claim.SettlementId ?? "");
                    _savedClaimData.Add(claim.ClaimantKingdomId ?? "");
                    _savedClaimData.Add(
                        ((int)claim.Source).ToString(
                            CultureInfo.InvariantCulture));
                    _savedClaimData.Add(
                        claim.Strength.ToString(
                            CultureInfo.InvariantCulture));
                    _savedClaimData.Add(
                        claim.CreatedOnDay.ToString(
                            "R",
                            CultureInfo.InvariantCulture));
                    _savedClaimData.Add(
                        claim.ExpiresOnDay.ToString(
                            "R",
                            CultureInfo.InvariantCulture));
                    _savedClaimData.Add(claim.IsUsed.ToString());
                }

                _eligiblePairData.Clear();

                foreach (string pair in _eligiblePairs)
                {
                    _eligiblePairData.Add(pair);
                }
            }

            dataStore.SyncData(
                "CasusBelli_WarData_v2",
                ref _savedWarDataV2);

            if (dataStore.IsLoading)
            {
                // formato antigo: lido so para migrar, nunca mais gravado
                dataStore.SyncData(
                    "CasusBelli_WarData_v1",
                    ref _savedWarData);
            }

            dataStore.SyncData(
                "CasusBelli_FabricationData_v1",
                ref _savedFabricationData);

            dataStore.SyncData(
                "CasusBelli_SettlementOwnershipData_v1",
                ref _savedSettlementOwnershipData);

            dataStore.SyncData(
                "CasusBelli_SettlementDeJureData_v2",
                ref _savedSettlementDeJureData);

            dataStore.SyncData(
                "CasusBelli_EligiblePairs_v1",
                ref _eligiblePairData);

            dataStore.SyncData(
                "CasusBelli_ClaimData_v1",
                ref _savedClaimData);

            dataStore.SyncData(
                "CasusBelli_DiplomacyInitialized_v1",
                ref _diplomacyInitialized);

            dataStore.SyncData(
                "CasusBelli_DeJureOwnershipInitialized_v2",
                ref _deJureOwnershipInitialized);

            if (dataStore.IsLoading)
            {
                dataStore.SyncData(
                    "CasusBelli_SettlementDeJureData_v1",
                    ref _savedSettlementDeJureDataV1);

                dataStore.SyncData(
                    "CasusBelli_DeJureOwnershipInitialized_v1",
                    ref _deJureOwnershipInitializedV1);
            }

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

                _settlementDeJureRegistry.Clear();

                _claimRegistry.Clear();

                _eligiblePairs.Clear();

                LoadWarRecordsFromData();
                LoadFabricationProjects();

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

                if (_savedSettlementDeJureData == null)
                {
                    _savedSettlementDeJureData = new List<string>();
                }

                List<string> deJureData = _savedSettlementDeJureData;
                int fieldsPerDeJureRecord = FieldsPerSettlementDeJureRecord;
                bool isDeJureDataV1 = false;

                if (!_deJureOwnershipInitialized &&
                    _deJureOwnershipInitializedV1)
                {
                    deJureData = _savedSettlementDeJureDataV1 ??
                        new List<string>();
                    fieldsPerDeJureRecord =
                        FieldsPerSettlementDeJureRecordV1;
                    isDeJureDataV1 = true;
                    _deJureOwnershipInitialized = true;
                }

                for (int i = 0;
                     i + fieldsPerDeJureRecord <= deJureData.Count;
                     i += fieldsPerDeJureRecord)
                {
                    double occupationStartedOnDay;
                    double activeUsurpationStartedOnDay = -1.0;

                    if (!double.TryParse(
                        deJureData[i + 3],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out occupationStartedOnDay))
                    {
                        continue;
                    }

                    if (!isDeJureDataV1 &&
                        !double.TryParse(
                            deJureData[i + 4],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out activeUsurpationStartedOnDay))
                    {
                        continue;
                    }

                    SettlementDeJureRecord record =
                        new SettlementDeJureRecord
                        {
                            SettlementId = deJureData[i],
                            DeJureKingdomId = deJureData[i + 1],
                            DeFactoKingdomId = deJureData[i + 2],
                            OccupationStartedOnDay = occupationStartedOnDay,
                            ActiveUsurpationStartedOnDay =
                                activeUsurpationStartedOnDay
                        };

                    _settlementDeJureRegistry.AddRecord(record);
                }

                if (_savedClaimData == null)
                {
                    _savedClaimData = new List<string>();
                }

                for (int i = 0;
                     i + FieldsPerClaim <= _savedClaimData.Count;
                     i += FieldsPerClaim)
                {
                    int sourceValue;
                    int strength;
                    double createdOnDay;
                    double expiresOnDay;
                    bool isUsed;

                    if (!int.TryParse(
                            _savedClaimData[i + 3],
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out sourceValue) ||
                        !int.TryParse(
                            _savedClaimData[i + 4],
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out strength) ||
                        !double.TryParse(
                            _savedClaimData[i + 5],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out createdOnDay) ||
                        !double.TryParse(
                            _savedClaimData[i + 6],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out expiresOnDay) ||
                        !bool.TryParse(
                            _savedClaimData[i + 7],
                            out isUsed) ||
                        !Enum.IsDefined(typeof(ClaimSource), sourceValue))
                    {
                        continue;
                    }

                    ClaimRecord claim = new ClaimRecord
                    {
                        ClaimId = _savedClaimData[i],
                        SettlementId = _savedClaimData[i + 1],
                        ClaimantKingdomId = _savedClaimData[i + 2],
                        Source = (ClaimSource)sourceValue,
                        Strength = strength,
                        CreatedOnDay = createdOnDay,
                        ExpiresOnDay = expiresOnDay,
                        IsUsed = isUsed
                    };

                    _claimRegistry.AddLoadedRecord(claim);
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

                if (_nextWarSequence < 1)
                {
                    _nextWarSequence = 1;
                }

                if (_nextSettlementOwnershipSequence < 1)
                {
                    _nextSettlementOwnershipSequence = 1;
                }
            }
        }

        private void OnSettlementOwnerChanged(
            Settlement settlement,
            bool openToClaim,
            Hero newOwner,
            Hero oldOwner,
            Hero capturerHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (settlement == null)
            {
                return;
            }

            SettlementOwnershipRecord record =
                new SettlementOwnershipRecord
                {
                    RecordId = "CB_SETTLEMENT_OWNERSHIP_" +
                        _nextSettlementOwnershipSequence,
                    SettlementId = settlement.StringId ?? "",
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

            if (_settlementOwnershipRegistry.AddRecord(record))
            {
                _nextSettlementOwnershipSequence++;
            }

            UpdateDeFactoOwnership(settlement, newOwner);
            ConsumeClaimIfConquered(settlement, newOwner);
            MarkClaimWarObjective(settlement, newOwner);
        }

        private void OnDailyTick()
        {
            _claimRegistry.RemoveExpired(
                CampaignTime.Now.ToDays,
                ClaimKeepDays);

            ProcessFabricationProjects();
            ProcessClaimWarAutoPeace();
            RunDailyAi();

            foreach (SettlementDeJureRecord record in
                _settlementDeJureRegistry.AllRecords)
            {
                Settlement settlement = Settlement.Find(record.SettlementId);

                if (settlement == null || !settlement.IsFortification)
                {
                    continue;
                }

                UpdateActiveUsurpationClock(settlement, record);

                if (string.IsNullOrEmpty(record.DeFactoKingdomId) ||
                    record.DeFactoKingdomId == record.DeJureKingdomId ||
                    GetOccupiedDays(record) < PassiveUsurpationDays)
                {
                    continue;
                }

                if (GetKingdomId(settlement.Owner) != record.DeFactoKingdomId)
                {
                    continue;
                }

                record.DeJureKingdomId = record.DeFactoKingdomId;
                record.OccupationStartedOnDay = 0.0;
                record.ActiveUsurpationStartedOnDay = -1.0;

                Debug.Print(
                "CB: usurpação passiva concluida para " +
                 settlement.StringId + ". Novo dono de jure: " +
                 record.DeJureKingdomId);
            }
        }

        private void UpdateDeFactoOwnership(
            Settlement settlement,
            Hero newOwner)
        {
            if (!_deJureOwnershipInitialized ||
                !settlement.IsFortification)
            {
                return;
            }

            SettlementDeJureRecord record;

            if (!_settlementDeJureRegistry.TryGetRecord(
                settlement.StringId,
                out record))
            {
                return;
            }

            string newKingdomId = GetKingdomId(newOwner);

            if (record.DeFactoKingdomId == newKingdomId)
            {
                return;
            }

            record.DeFactoKingdomId = newKingdomId;
            record.OccupationStartedOnDay =
                newKingdomId == record.DeJureKingdomId
                    ? 0.0
                    : CampaignTime.Now.ToDays;
            record.ActiveUsurpationStartedOnDay = -1.0;

            UpdateActiveUsurpationClock(settlement, record);
        }

        private double GetOccupiedDays(SettlementDeJureRecord record)
        {
            if (record == null ||
                record.DeFactoKingdomId == record.DeJureKingdomId)
            {
                return 0.0;
            }

            return CampaignTime.Now.ToDays - record.OccupationStartedOnDay;
        }

        private void UpdateActiveUsurpationClock(
            Settlement settlement,
            SettlementDeJureRecord record)
        {
            if (!CanRunActiveUsurpationClock(settlement, record))
            {
                record.ActiveUsurpationStartedOnDay = -1.0;
                return;
            }

            if (record.ActiveUsurpationStartedOnDay < 0.0)
            {
                record.ActiveUsurpationStartedOnDay = CampaignTime.Now.ToDays;
            }
        }

        private bool CanRunActiveUsurpationClock(
            Settlement settlement,
            SettlementDeJureRecord record)
        {
            if (settlement == null ||
                record == null ||
                settlement.IsUnderSiege ||
                string.IsNullOrEmpty(record.DeFactoKingdomId) ||
                record.DeFactoKingdomId == record.DeJureKingdomId ||
                GetKingdomId(settlement.Owner) != record.DeFactoKingdomId)
            {
                return false;
            }

            Kingdom deJureKingdom = GetKingdom(record.DeJureKingdomId);
            Kingdom deFactoKingdom = GetKingdom(record.DeFactoKingdomId);

            return deJureKingdom != null &&
                deFactoKingdom != null &&
                !deJureKingdom.IsAtWarWith(deFactoKingdom);
        }

        private double GetActiveUsurpationDays(
            SettlementDeJureRecord record)
        {
            if (record == null ||
                record.ActiveUsurpationStartedOnDay < 0.0)
            {
                return 0.0;
            }

            return CampaignTime.Now.ToDays -
                record.ActiveUsurpationStartedOnDay;
        }

        public bool IsActiveUsurpationAvailable(Settlement settlement)
        {
            if (settlement == null ||
                !settlement.IsFortification ||
                settlement.IsUnderSiege)
            {
                return false;
            }

            SettlementDeJureRecord record;

            if (!_settlementDeJureRegistry.TryGetRecord(
                settlement.StringId,
                out record) ||
                record.DeFactoKingdomId == record.DeJureKingdomId ||
                GetActiveUsurpationDays(record) < ActiveUsurpationDays)
            {
                return false;
            }

            return CanRunActiveUsurpationClock(settlement, record);
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
            TryInitializeDeJureOwnership();
            InitializeDiplomacyTracking();
        }

        private void OnGameLoadFinished()
        {
            InitializeDiplomacyTracking();
        }

        public bool InitializeDeJureOwnershipFromCurrentMap()
        {
            return TryInitializeDeJureOwnership();
        }

        private bool TryInitializeDeJureOwnership()
        {
            if (_deJureOwnershipInitialized)
            {
                return false;
            }

            _settlementDeJureRegistry.Clear();

            int recordCount = 0;

            foreach (Settlement settlement in Settlement.All)
            {
                if (settlement == null ||
                    !settlement.IsFortification ||
                    string.IsNullOrEmpty(settlement.StringId))
                {
                    continue;
                }

                string kingdomId = GetKingdomId(settlement.Owner);

                if (string.IsNullOrEmpty(kingdomId))
                {
                    continue;
                }

                SettlementDeJureRecord record =
                    new SettlementDeJureRecord
                    {
                        SettlementId = settlement.StringId,
                        DeJureKingdomId = kingdomId,
                        DeFactoKingdomId = kingdomId,
                        OccupationStartedOnDay = 0.0,
                        ActiveUsurpationStartedOnDay = -1.0
                    };

                if (_settlementDeJureRegistry.AddRecord(record))
                {
                    recordCount++;

                    Debug.Print(
                        "CB: estado inicial de jure | Feudo: " +
                        record.SettlementId + " | Reino: " +
                        record.DeJureKingdomId);
                }
            }

            _deJureOwnershipInitialized = true;

            InformationManager.DisplayMessage(
                new InformationMessage(
                    "CB: estado inicial de jure registrado para " +
                    recordCount + " feudos."));

            return true;
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
                    war.IsActive = false;
                    FinalizeWar(war);
                }
            }
        }


        private void OnWarDeclared(
            IFaction faction1,
            IFaction faction2,
            TaleWorlds.CampaignSystem.Actions
                .DeclareWarAction.DeclareWarDetail detail)
        {
            if (!_diplomacyInitialized)
            {
                return;
            }

            string pairKey = GetPairKey(faction1, faction2);

            if (pairKey == null)
            {
                return;
            }

            if (!_eligiblePairs.Contains(pairKey))
            {
                return;
            }

            _eligiblePairs.Remove(pairKey);

            Kingdom kingdom1 = faction1 as Kingdom;
            Kingdom kingdom2 = faction2 as Kingdom;

            if (kingdom1 == null || kingdom2 == null)
            {
                return;
            }

            string casusBelliType = "UNASSIGNED";
            string targetSettlementId = "";
            string claimId = "";

            if (_pendingClaimWar != null &&
                _pendingClaimWar.AttackerKingdomId == kingdom1.StringId &&
                _pendingClaimWar.DefenderKingdomId == kingdom2.StringId)
            {
                casusBelliType = ClaimWarCasusBelliType;
                targetSettlementId = _pendingClaimWar.SettlementId;
                claimId = _pendingClaimWar.ClaimId;
            }
            else
            {
                // Guerra declarada pelo caminho normal do jogo (decisao do
                // reino, IA do jogo base): se o atacante tem um claim contra
                // o defensor, ela vale como guerra de reivindicacao.
                ClaimRecord adoptedClaim;
                Settlement adoptedSettlement;

                if (TryFindBestClaimAgainst(
                    kingdom1,
                    kingdom2,
                    out adoptedClaim,
                    out adoptedSettlement))
                {
                    casusBelliType = ClaimWarCasusBelliType;
                    targetSettlementId = adoptedSettlement.StringId;
                    claimId = adoptedClaim.ClaimId;
                }
            }

            WarRecord war = new WarRecord
            {
                WarId = "CB_WAR_" + _nextWarSequence,
                AttackerKingdomId = kingdom1.StringId,
                DefenderKingdomId = kingdom2.StringId,
                CasusBelliType = casusBelliType,
                TargetSettlementId = targetSettlementId,
                IsActive = true,
                StartedOnDay = CampaignTime.Now.ToDays,
                ClaimId = claimId
            };

            if (_warRegistry.AddWar(war))
            {
                _nextWarSequence++;

                if (casusBelliType == "UNASSIGNED")
                {
                    ApplyUnassignedWarPenalty(kingdom1);
                }
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

        public ClaimRegistry GetClaimRegistry()
        {
            return _claimRegistry;
        }

        // Efeito da usurpacao ativa: o dono de facto vira o de jure.
        // Mesmo efeito final da usurpacao passiva, so que antes do prazo.
        public bool TryPerformActiveUsurpation(
            Settlement settlement,
            out string message)
        {
            message = "";

            if (!IsActiveUsurpationAvailable(settlement))
            {
                message = "usurpação ativa indisponivel (exige " +
                    ActiveUsurpationDays + " dias sem cerco e sem " +
                    "guerra entre os reinos)";
                return false;
            }

            SettlementDeJureRecord record;

            if (!_settlementDeJureRegistry.TryGetRecord(
                settlement.StringId,
                out record))
            {
                message = "feudo sem estado de jure";
                return false;
            }

            string formerDeJureKingdomId = record.DeJureKingdomId;

            record.DeJureKingdomId = record.DeFactoKingdomId;
            record.OccupationStartedOnDay = 0.0;
            record.ActiveUsurpationStartedOnDay = -1.0;

            message = "usurpação ativa concluida: " + settlement.StringId +
                " | De jure: " + formerDeJureKingdomId + " -> " +
                record.DeJureKingdomId;
            return true;
        }

        // Um claim guardado perde o sentido quando o proprio reino ja
        // possui o feudo. (O claim de jure se resolve sozinho:
        // de facto passa a ser igual ao de jure.)
        private void ConsumeClaimIfConquered(
            Settlement settlement,
            Hero newOwner)
        {
            string newKingdomId = GetKingdomId(newOwner);

            if (string.IsNullOrEmpty(newKingdomId))
            {
                return;
            }

            ClaimRecord claim;

            if (_claimRegistry.TryGetStoredClaim(
                newKingdomId,
                settlement.StringId,
                CampaignTime.Now.ToDays,
                out claim))
            {
                _claimRegistry.MarkUsed(claim.ClaimId);
            }
        }

        public WarRegistry GetWarRegistry()
        {
            return _warRegistry;
        }

        public SettlementDeJureRegistry GetSettlementDeJureRegistry()
        {
            return _settlementDeJureRegistry;
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
