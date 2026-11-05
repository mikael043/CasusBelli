using System.Collections.Generic;

namespace CasusBelli
{
    public class SettlementDeJureRegistry
    {
        private readonly List<SettlementDeJureRecord> _records =
            new List<SettlementDeJureRecord>();

        public IEnumerable<SettlementDeJureRecord> AllRecords
        {
            get { return _records; }
        }

        public bool AddRecord(SettlementDeJureRecord record)
        {
            if (record == null ||
                string.IsNullOrWhiteSpace(record.SettlementId) ||
                string.IsNullOrWhiteSpace(record.DeJureKingdomId))
            {
                return false;
            }

            SettlementDeJureRecord existing;

            if (TryGetRecord(record.SettlementId, out existing))
            {
                return false;
            }

            _records.Add(record);
            return true;
        }

        public bool TryGetRecord(
            string settlementId,
            out SettlementDeJureRecord record)
        {
            record = null;

            if (string.IsNullOrWhiteSpace(settlementId))
            {
                return false;
            }

            foreach (SettlementDeJureRecord existing in _records)
            {
                if (existing.SettlementId == settlementId)
                {
                    record = existing;
                    return true;
                }
            }

            return false;
        }

        public void Clear()
        {
            _records.Clear();
        }
    }
}
