using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;


namespace CasusBelli
{
    // Faz a IA do jogo base levar o casus belli em conta ao decidir guerras
    // e pazes. Os fatores vem do perfil cultural de cada reino
    // (CasusBelliKingdomProfiles.cs).
    //
    // Guerra: nota original x fator
    //   - tem claim usavel contra o alvo -> perfil.WithClaimFactor
    //   - nao tem claim                  -> perfil.WithoutClaimFactor
    //   - ha tregua em vigor             -> perfil.TruceFactor (quase proibida)
    //   Depois, multiplica por: perfil.AiWarChanceMultiplier, temperamento
    //   do rei (Valor) e rancor (rei capturado pelo alvo nos ultimos 365 dias).
    // Paz: nota original x perfil.PeaceEagerness.
    //
    // A nota -10000000 do jogo base ("fraco demais") nunca e alterada.
    public class CasusBelliDiplomacyModel : DefaultDiplomacyModel
    {
        private const float HardBlockThreshold = -9000000f;
        private const float PeaceBlockedScore = -10000000f;

        public override float GetScoreOfDeclaringWar(
            IFaction factionDeclaresWar,
            IFaction factionDeclaredWar,
            Clan evaluatingClan,
            out TextObject reason,
            bool includeReason = false)
        {
            float score = base.GetScoreOfDeclaringWar(
                factionDeclaresWar,
                factionDeclaredWar,
                evaluatingClan,
                out reason,
                includeReason);

            if (score <= HardBlockThreshold)
            {
                return score;
            }

            Kingdom attacker = factionDeclaresWar as Kingdom;
            Kingdom defender = factionDeclaredWar as Kingdom;

            if (attacker == null || defender == null || Campaign.Current == null)
            {
                return score;
            }

            CasusBelliCampaignBehavior behavior =
                Campaign.Current.GetCampaignBehavior<
                    CasusBelliCampaignBehavior>();

            if (behavior == null)
            {
                return score;
            }

            KingdomProfile profile = KingdomProfiles.Get(attacker.StringId);
            float factor;

            if (behavior.IsTruceActive(attacker.StringId, defender.StringId))
            {
                // tregua manda: nao ha bonus que a anule
                return Scale(score, profile.TruceFactor);
            }

            if (behavior.HasUsableClaimAgainst(attacker, defender))
            {
                factor = profile.WithClaimFactor;
            }
            else
            {
                factor = profile.WithoutClaimFactor;
            }

            factor *= profile.AiWarChanceMultiplier;
            factor *= KingTemperament.WarChanceMultiplier(attacker.Leader);

            CasusBelliKingBehavior kings =
                Campaign.Current.GetCampaignBehavior<CasusBelliKingBehavior>();

            if (kings != null)
            {
                factor *= kings.GetGrievanceFactor(attacker, defender, profile);
            }

            return Scale(score, factor);
        }

        // Se der erro de assinatura aqui, abra DefaultDiplomacyModel no ILSpy
        // e copie a assinatura exata de GetScoreOfDeclaringPeace da sua versao.
        public override float GetScoreOfDeclaringPeace(
            IFaction factionDeclaresPeace,
            IFaction factionDeclaredPeace)
        {
            float score = base.GetScoreOfDeclaringPeace(
                factionDeclaresPeace,
                factionDeclaredPeace);

            Kingdom kingdom = factionDeclaresPeace as Kingdom;
            Kingdom other = factionDeclaredPeace as Kingdom;

            if (kingdom == null)
            {
                return score;
            }

            // Guerra registrada no mod: a paz so sai pelo WarScore
            // (menu de guerra e paz ou IA do mod), nunca pela votacao padrao.
            if (other != null && Campaign.Current != null)
            {
                CasusBelliCampaignBehavior behavior =
                    Campaign.Current.GetCampaignBehavior<
                        CasusBelliCampaignBehavior>();
                WarRecord war;

                if (behavior != null &&
                    behavior.TryGetActiveWarBetween(
                        kingdom.StringId,
                        other.StringId,
                        out war))
                {
                    return PeaceBlockedScore;
                }
            }

            return Scale(score, KingdomProfiles.Get(kingdom.StringId).PeaceEagerness);
        }

        // Fator maior que 1 sempre AUMENTA a vontade, tambem quando a nota
        // e negativa (por isso a divisao). Fator <= 0 e ignorado.
        private static float Scale(float score, float factor)
        {
            if (factor <= 0f)
            {
                return score;
            }

            return score >= 0f ? score * factor : score / factor;
        }
    }
}
