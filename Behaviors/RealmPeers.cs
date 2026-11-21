using System.Collections.Generic;
using System.Linq;
using CasusBelli.Managers.Titles;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli
{
    public sealed class RealmPeers : CampaignBehaviorBase
    {
        internal const int Cost = 250;
        internal static RealmPeers Current => Campaign.Current?.GetCampaignBehavior<RealmPeers>();
        private List<string> _grants = new List<string>();
        private static string Key(Clan c) => c.Kingdom.StringId + "|" + c.StringId;
        internal void Grant(Clan c) { if (!_grants.Contains(Key(c))) _grants.Add(Key(c)); }

        internal static int Rank(Clan c)
        {
            if (c?.Kingdom == null || c.IsEliminated || c.IsUnderMercenaryService) return 0;
            if (c == c.Kingdom.RulingClan || c.Fiefs.Count > 0 || Current?._grants.Contains(Key(c)) == true) return 2;
            return TitleManager.Instance.GetAllDeJure(c).Any(t => t.TitleType != TitleType.Lordship) ? 2 : 1;
        }
        public override void SyncData(IDataStore store)
        {
            store.SyncData("CasusBelli_PeerGrants_v1", ref _grants);
            _grants ??= new List<string>();
        }
        public override void RegisterEvents() => CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, Daily);
        private void Daily()
        {
            if ((int)CampaignTime.Now.ToDays % 7 != 0) return;
            foreach (var c in Clan.All)
                if (c != Clan.PlayerClan && c.Kingdom != null && c.Influence >= Cost && MBRandom.RandomFloat < 0.05f) Petition(c);
        }
        private static bool CanPetition(Clan c) => Rank(c) == 1 && c.Influence >= Cost &&
            !c.Kingdom.UnresolvedDecisions.OfType<PeerageDecision>().Any(d => d.ProposerClan == c);
        private static void Petition(Clan c)
        {
            if (CanPetition(c)) c.Kingdom.AddDecision(new PeerageDecision(c), false);
        }
        internal static void Show(Kingdom realm, System.Action back)
        {
            var names = new[] { "Sem paridade", "Paridade menor", "Paridade plena" };
            var text = "Pares menores votam. Pares plenos também podem propor decisões e concorrer à concessão de feudos.\n\n" +
                string.Join("\n", realm.Clans.Where(c => !c.IsEliminated).OrderByDescending(Rank)
                    .Select(c => c.Name + " — " + names[Rank(c)]));
            var player = Clan.PlayerClan;
            bool request = player?.Kingdom == realm && CanPetition(player);
            if (player?.Kingdom == realm && Rank(player) == 1) text += $"\n\nA petição custa {Cost} de influência e depende de votação.";
            InformationManager.ShowInquiry(new InquiryData("Pares do reino", text, true, request,
                request ? "Pedir paridade plena" : "Voltar", "Voltar", () => { if (request) Petition(player); else back(); }, back));
        }
    }

    [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.AddDecision))]
    internal static class PeerProposalPatch
    {
        private static bool Prefix(KingdomDecision __0)
        {
            if (__0 == null || RealmPeers.Current == null || __0 is PeerageDecision ||
                __0 is KingSelectionKingdomDecision || __0 is SettlementClaimantDecision) return true;
            if (RealmPeers.Rank(__0.ProposerClan) == 2) return true;
            if (__0.ProposerClan == Clan.PlayerClan)
                InformationManager.DisplayMessage(new InformationMessage("É preciso ter paridade plena para apresentar essa proposta."));
            return false;
        }
    }

    [HarmonyPatch(typeof(SettlementClaimantDecision), nameof(SettlementClaimantDecision.DetermineInitialCandidates))]
    internal static class PeerFiefPatch
    {
        private static void Postfix(ref IEnumerable<DecisionOutcome> __result)
        {
            if (RealmPeers.Current != null)
                __result = __result.Where(o => !(o is SettlementClaimantDecision.ClanAsDecisionOutcome candidate) ||
                    RealmPeers.Rank(candidate.Clan) == 2);
        }
    }
}
