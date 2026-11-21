using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace CasusBelli
{
    public class CasusBelliCampaignBehavior : CampaignBehaviorBase
    {
        private WarRegistry _warRegistry = new WarRegistry();

        private List<string> _savedWarData = new List<string>();

        private const int FieldsPerWar = 6;

        public override void RegisterEvents()
        {
            // Os eventos de guerra serão registrados depois.
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                _savedWarData.Clear();

                foreach (WarRecord war in _warRegistry.AllWars.ToList())
                {
                    _savedWarData.Add(war.WarId ?? "");
                    _savedWarData.Add(war.AttackerKingdomId ?? "");
                    _savedWarData.Add(war.DefenderKingdomId ?? "");
                    _savedWarData.Add(war.CasusBelliType ?? "");
                    _savedWarData.Add(war.TargetSettlementId ?? "");
                    _savedWarData.Add(war.IsActive.ToString());
                }
            }

            dataStore.SyncData(
                "CasusBelli_WarData_v1",
                ref _savedWarData);

            if (dataStore.IsLoading)
            {
                _warRegistry.Clear();

                if (_savedWarData == null)
                {
                    _savedWarData = new List<string>();
                }

                for (int i = 0;
                     i + FieldsPerWar <= _savedWarData.Count;
                     i += FieldsPerWar)
                {
                    bool isActive;

                    if (!bool.TryParse(
                        _savedWarData[i + 5],
                        out isActive))
                    {
                        continue;
                    }

                    WarRecord war = new WarRecord
                    {
                        WarId = _savedWarData[i],
                        AttackerKingdomId = _savedWarData[i + 1],
                        DefenderKingdomId = _savedWarData[i + 2],
                        CasusBelliType = _savedWarData[i + 3],
                        TargetSettlementId = _savedWarData[i + 4],
                        IsActive = isActive
                    };

                    _warRegistry.AddWar(war);
                }
            }
        }

        public WarRegistry GetWarRegistry()
        {
            return _warRegistry;
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
