using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace CasusBelli
{
    internal static class RoyalSeat
    {
        internal static Settlement Get(Kingdom realm)
        {
            if (realm?.Leader == null) return null;
            string id = RealmPolitics.Current?.Read("court|" + realm.StringId);
            var seat = string.IsNullOrEmpty(id) ? realm.InitialHomeSettlement : Settlement.Find(id);
            return seat?.OwnerClan?.Kingdom == realm && seat.IsTown ? seat : Select(realm);
        }
        internal static Settlement Select(Kingdom realm) => realm.Fiefs.Where(t => t.IsTown)
            .OrderByDescending(t => t.OwnerClan == realm.RulingClan)
            .ThenByDescending(t => t.Settlement == realm.Leader.HomeSettlement)
            .ThenByDescending(t => t.Prosperity).ThenBy(t => t.Settlement.StringId).FirstOrDefault()?.Settlement;
        internal static void Move(Kingdom realm)
        {
            if (realm?.Leader == null) return;
            RealmPolitics.Current?.Write("court|" + realm.StringId,Select(realm)?.StringId);
        }
    }
}
