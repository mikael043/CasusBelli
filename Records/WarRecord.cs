namespace CasusBelli
{
    public class WarRecord
    {
        public string WarId { get; set; }

        public string AttackerKingdomId { get; set; }

        public string DefenderKingdomId { get; set; }

        public string CasusBelliType { get; set; }

        public string TargetSettlementId { get; set; }

        public bool IsActive { get; set; }

        // Dia em que a guerra comecou (-1 = desconhecido, ex.: guerra
        // que ja existia antes do mod).
        public double StartedOnDay { get; set; }

        // Dia em que a paz foi feita (-1 = guerra ainda ativa).
        public double EndedOnDay { get; set; }

        // Dia em que o atacante conquistou o feudo-alvo (-1 = nao conquistou).
        public double ObjectiveAchievedOnDay { get; set; }

        // "" enquanto ativa; "VICTORY", "WHITE_PEACE" ou "PEACE" depois.
        public string Outcome { get; set; }

        // Claim usado para declarar a guerra (so guerras CLAIM).
        public string ClaimId { get; set; }

        public WarRecord()
        {
            WarId = "";
            AttackerKingdomId = "";
            DefenderKingdomId = "";
            CasusBelliType = "";
            TargetSettlementId = "";
            IsActive = true;
            StartedOnDay = -1.0;
            EndedOnDay = -1.0;
            ObjectiveAchievedOnDay = -1.0;
            Outcome = "";
            ClaimId = "";
        }
    }
}
