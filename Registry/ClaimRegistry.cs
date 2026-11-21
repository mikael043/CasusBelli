using System.Collections.Generic;

namespace CasusBelli
{
    public class ClaimRegistry
    {
        private const string DeJureIdPrefix = "dejure_";

        private readonly List<ClaimRecord> _records =
            new List<ClaimRecord>();

        public IEnumerable<ClaimRecord> AllRecords
        {
            get { return _records; }
        }

        // Guarda apenas claims adquiridos (fabricados ou concedidos).
        // O claim de jure NAO e guardado: ele e derivado do
        // SettlementDeJureRegistry em TryGetUsableClaim.
        public bool AddRecord(ClaimRecord record, double nowDays)
        {
            if (record == null ||
                string.IsNullOrWhiteSpace(record.SettlementId) ||
                string.IsNullOrWhiteSpace(record.ClaimantKingdomId))
            {
                return false;
            }

            if (record.Source == ClaimSource.DeJure)
            {
                return false;
            }

            ClaimRecord existing;

            if (TryGetStoredClaim(
                record.ClaimantKingdomId,
                record.SettlementId,
                nowDays,
                out existing))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(record.ClaimId))
            {
                record.ClaimId = GenerateClaimId();
            }
            else if (FindById(record.ClaimId) != null)
            {
                return false;
            }

            _records.Add(record);
            return true;
        }

        // Usado ao CARREGAR o save: nao consulta o tempo da campanha
        // e nao descarta claims expirados (a limpeza diaria faz isso).
        public bool AddLoadedRecord(ClaimRecord record)
        {
            if (record == null ||
                string.IsNullOrWhiteSpace(record.ClaimId) ||
                string.IsNullOrWhiteSpace(record.SettlementId) ||
                string.IsNullOrWhiteSpace(record.ClaimantKingdomId) ||
                record.Source == ClaimSource.DeJure ||
                FindById(record.ClaimId) != null)
            {
                return false;
            }

            _records.Add(record);
            return true;
        }

        // Melhor claim guardado e ativo do reino sobre o feudo.
        public bool TryGetStoredClaim(
            string kingdomId,
            string settlementId,
            double nowDays,
            out ClaimRecord claim)
        {
            claim = null;

            if (string.IsNullOrWhiteSpace(kingdomId) ||
                string.IsNullOrWhiteSpace(settlementId))
            {
                return false;
            }

            foreach (ClaimRecord record in _records)
            {
                if (record.ClaimantKingdomId != kingdomId ||
                    record.SettlementId != settlementId ||
                    !record.IsActive(nowDays))
                {
                    continue;
                }

                if (claim == null || record.Strength > claim.Strength)
                {
                    claim = record;
                }
            }

            return claim != null;
        }

        // Claim que o reino pode usar agora: primeiro o de jure
        // (derivado), depois os guardados.
        public bool TryGetUsableClaim(
            string kingdomId,
            string settlementId,
            double nowDays,
            SettlementDeJureRegistry deJureRegistry,
            out ClaimRecord claim)
        {
            claim = null;

            if (string.IsNullOrWhiteSpace(kingdomId) ||
                string.IsNullOrWhiteSpace(settlementId))
            {
                return false;
            }

            SettlementDeJureRecord deJure;

            if (deJureRegistry != null &&
                deJureRegistry.TryGetRecord(settlementId, out deJure) &&
                IsDeJureClaim(deJure, kingdomId))
            {
                claim = CreateDeJureClaim(deJure);
                return true;
            }

            return TryGetStoredClaim(
                kingdomId,
                settlementId,
                nowDays,
                out claim);
        }

        // Todos os claims usaveis do reino (de jure + guardados).
        public List<ClaimRecord> GetUsableClaimsForKingdom(
            string kingdomId,
            double nowDays,
            SettlementDeJureRegistry deJureRegistry)
        {
            List<ClaimRecord> result = new List<ClaimRecord>();

            if (string.IsNullOrWhiteSpace(kingdomId))
            {
                return result;
            }

            HashSet<string> alreadyListed = new HashSet<string>();

            if (deJureRegistry != null)
            {
                foreach (SettlementDeJureRecord deJure in
                    deJureRegistry.AllRecords)
                {
                    if (IsDeJureClaim(deJure, kingdomId))
                    {
                        result.Add(CreateDeJureClaim(deJure));
                        alreadyListed.Add(deJure.SettlementId);
                    }
                }
            }

            foreach (ClaimRecord record in _records)
            {
                if (record.ClaimantKingdomId != kingdomId ||
                    !record.IsActive(nowDays) ||
                    alreadyListed.Contains(record.SettlementId))
                {
                    continue;
                }

                result.Add(record);
            }

            return result;
        }

        // Marca um claim guardado como usado (ex.: guerra vencida).
        // Claims de jure sao derivados e nao podem ser consumidos.
        public bool MarkUsed(string claimId)
        {
            ClaimRecord record = FindById(claimId);

            if (record == null)
            {
                return false;
            }

            record.IsUsed = true;
            return true;
        }

        // Remove claims inativos ha mais de keepDays dias.
        public int RemoveExpired(double nowDays, double keepDays)
        {
            return _records.RemoveAll(record =>
            {
                if (record.IsActive(nowDays))
                {
                    return false;
                }

                double endedOn = record.ExpiresOnDay > 0.0
                    ? record.ExpiresOnDay
                    : record.CreatedOnDay;

                return nowDays > endedOn + keepDays;
            });
        }

        public void Clear()
        {
            _records.Clear();
        }

        private static bool IsDeJureClaim(
            SettlementDeJureRecord record,
            string kingdomId)
        {
            return record != null &&
                record.DeJureKingdomId == kingdomId &&
                record.DeFactoKingdomId != kingdomId;
        }

        private static ClaimRecord CreateDeJureClaim(
            SettlementDeJureRecord deJure)
        {
            ClaimRecord claim = new ClaimRecord();
            claim.ClaimId = DeJureIdPrefix + deJure.SettlementId;
            claim.SettlementId = deJure.SettlementId;
            claim.ClaimantKingdomId = deJure.DeJureKingdomId;
            claim.Source = ClaimSource.DeJure;
            claim.Strength = 100;
            claim.ExpiresOnDay = 0.0;
            return claim;
        }

        private ClaimRecord FindById(string claimId)
        {
            if (string.IsNullOrWhiteSpace(claimId))
            {
                return null;
            }

            foreach (ClaimRecord record in _records)
            {
                if (record.ClaimId == claimId)
                {
                    return record;
                }
            }

            return null;
        }

        private string GenerateClaimId()
        {
            int max = 0;

            foreach (ClaimRecord record in _records)
            {
                if (record.ClaimId != null &&
                    record.ClaimId.StartsWith("claim_"))
                {
                    int number;

                    if (int.TryParse(
                        record.ClaimId.Substring(6),
                        out number) && number > max)
                    {
                        max = number;
                    }
                }
            }

            return "claim_" + (max + 1);
        }
    }
}
