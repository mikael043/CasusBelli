namespace CasusBelli
{
    // Membros publicos usados pelo CasusBelliKingBehavior.
    // Classe partial: basta adicionar este arquivo ao projeto.
    public partial class CasusBelliCampaignBehavior
    {
        // Versao publica do tipo de casus belli das guerras de claim.
        public const string ClaimWarType = ClaimWarCasusBelliType;

        // Guerra ATIVA entre dois reinos (em qualquer direcao).
        // Se houver mais de uma ficha ativa, devolve a mais recente.
        public bool TryGetActiveWarBetween(
            string kingdomId1,
            string kingdomId2,
            out WarRecord found)
        {
            found = null;

            foreach (WarRecord war in _warRegistry.AllWars)
            {
                if (war == null || !war.IsActive)
                {
                    continue;
                }

                bool samePair =
                    (war.AttackerKingdomId == kingdomId1 &&
                     war.DefenderKingdomId == kingdomId2) ||
                    (war.AttackerKingdomId == kingdomId2 &&
                     war.DefenderKingdomId == kingdomId1);

                if (!samePair)
                {
                    continue;
                }

                if (found == null || war.StartedOnDay > found.StartedOnDay)
                {
                    found = war;
                }
            }

            return found != null;
        }
    }
}
