namespace CasusBelli
{
    public enum ClaimSource
    {
        // Derivado do estado de jure (de jure != de facto). Nunca é guardado.
        DeJure = 0,

        // Criado por fabricação (custo + tempo).
        Fabricated = 1,

        // Concedido por evento, acordo ou história.
        Granted = 2
    }

    public class ClaimRecord
    {
        public string ClaimId { get; set; }
        public string SettlementId { get; set; }
        public string ClaimantKingdomId { get; set; }
        public ClaimSource Source { get; set; }

        // 1 a 100. De jure = 100; fabricado = menor.
        public int Strength { get; set; }

        public double CreatedOnDay { get; set; }

        // <= 0 significa permanente.
        public double ExpiresOnDay { get; set; }

        public bool IsUsed { get; set; }

        public ClaimRecord()
        {
            ClaimId = "";
            SettlementId = "";
            ClaimantKingdomId = "";
            Source = ClaimSource.Fabricated;
            Strength = 50;
            CreatedOnDay = 0.0;
            ExpiresOnDay = 0.0;
            IsUsed = false;
        }

        public bool IsActive(double nowDays)
        {
            return !IsUsed &&
                (ExpiresOnDay <= 0.0 || nowDays < ExpiresOnDay);
        }
    }
}
