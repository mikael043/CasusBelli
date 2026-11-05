namespace CasusBelli
{
   
    public class SettlementOwnershipRecord
    {
        public string RecordId { get; set; }
        public string SettlementId { get; set; }
        public string OldOwnerHeroId { get; set; }
        public string NewOwnerHeroId { get; set; }
        public string OldOwnerClanId { get; set; }
        public string NewOwnerClanId { get; set; }
        public string OldOwnerKingdomId { get; set; }
        public string NewOwnerKingdomId { get; set; }
        public string CapturerHeroId { get; set; }
        public string ChangeReason { get; set; }
        public string CampaignTime { get; set; }

        public SettlementOwnershipRecord()
        {
            RecordId = "";
            SettlementId = "";
            OldOwnerHeroId = "";
            NewOwnerHeroId = "";
            OldOwnerClanId = "";
            NewOwnerClanId = "";
            OldOwnerKingdomId = "";
            NewOwnerKingdomId = "";
            CapturerHeroId = "";
            ChangeReason = "";
            CampaignTime = "";
        }
    }
}
