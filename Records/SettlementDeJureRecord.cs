namespace CasusBelli
{
    public class SettlementDeJureRecord
    {
        public string SettlementId { get; set; }
        public string DeJureKingdomId { get; set; }
        public string DeFactoKingdomId { get; set; }
        public double OccupationStartedOnDay { get; set; }
        public double ActiveUsurpationStartedOnDay { get; set; }

        public SettlementDeJureRecord()
        {
            SettlementId = "";
            DeJureKingdomId = "";
            DeFactoKingdomId = "";
            OccupationStartedOnDay = 0.0;
            ActiveUsurpationStartedOnDay = -1.0;
        }
    }
}
