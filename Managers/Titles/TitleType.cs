using TaleWorlds.SaveSystem;

namespace CasusBelli.Managers.Titles
{
    // Tipos de título feudal, do mais alto para o mais baixo
    // O valor numérico importa: menor = mais alto na hierarquia
    public enum TitleType
    {
        Empire = 0,
        Kingdom = 1,
        Dukedom = 2,
        County = 3,
        Barony = 4,
        Lordship = 5
    }

    // Tipos de reivindicação (claim) sobre um título
    public enum ClaimType
    {
        None,
        Ongoing,        // Em processo de fabricação (amadurece em ~1 ano)
        Fabricated,     // Claim fabricado e maduro
        PreviousOwner,  // Ex-dono legal
        DeFacto,        // Detentor de facto atual
        ClanSplit       // Resultado de divisão de clã
    }

    // Tipos de ação que podem ser tomadas sobre um título
    public enum ActionType
    {
        Usurp,      // Usurpar título (requer claim maduro ou controle de terra)
        Revoke,     // Revogar título de um vassalo
        Grant,      // Conceder título a alguém
        Destroy,    // Destruir título
        Create,     // Criar título novo (quando não há dono legal)
        Claim,      // Iniciar fabricação de claim
        Found       // Fundar reino/império a partir de títulos existentes
    }
}