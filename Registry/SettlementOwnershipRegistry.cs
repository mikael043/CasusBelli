using System.Collections.Generic;

namespace CasusBelli
{
    public class SettlementOwnershipRegistry
    {
        private readonly List<SettlementOwnershipRecord> _records =
            new List<SettlementOwnershipRecord>();

        public IEnumerable<SettlementOwnershipRecord> AllRecords
        {
            get { return _records; }
        }

        public bool AddRecord(SettlementOwnershipRecord record)
        {
            if (record == null ||
                string.IsNullOrWhiteSpace(record.RecordId) ||
                string.IsNullOrWhiteSpace(record.SettlementId))
            {
                return false;
            }

            foreach (SettlementOwnershipRecord existing in _records)
            {
                if (existing.RecordId == record.RecordId)
                {
                    return false;
                }
            }

            _records.Add(record);
            return true;
        }

        public List<SettlementOwnershipRecord> GetHistoryForSettlement(
            string settlementId)
        {
            List<SettlementOwnershipRecord> result =
                new List<SettlementOwnershipRecord>();

            if (string.IsNullOrWhiteSpace(settlementId))
            {
                return result;
            }

            foreach (SettlementOwnershipRecord record in _records)
            {
                if (record.SettlementId == settlementId)
                {
                    result.Add(record);
                }
            }

            return result;
        }

        public void Clear()
        {
            _records.Clear();
        }
    }
}
