using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace CasusBelli.Managers.Titles
{
    /// <summary>
    /// Classe utilitária para obter o nome de título de forma formatada
    /// para exibição no jogo (personagens, enciclopédia, etc.)
    /// </summary>
    public static class TitleDisplayName
    {
        /// <summary>
        /// Retorna o nome completo do título com o reino
        /// Ex: "o Ducado de Vlandia"
        /// </summary>
        public static string GetTitleDisplayName(FeudalTitle title, Kingdom kingdom = null)
        {
            if (title == null) return "";

            string rankName = TextHelper.GetRankName(title.TitleType);
            string kingdomName = kingdom?.Name?.ToString() ?? (title.deJure?.Clan?.Kingdom?.Name?.ToString() ?? "");

            if (!string.IsNullOrEmpty(kingdomName))
                return "o " + rankName + " de " + kingdomName;
            return "o " + rankName;
        }

        /// <summary>
        /// Retorna apenas o nome do rank (duque, conde, etc.)
        /// </summary>
        public static string GetRankName(FeudalTitle title)
        {
            if (title == null) return "Título";
            return TextHelper.GetRankName(title.TitleType);
        }

        /// <summary>
        /// Gera o prefixo do título para nomes de personagens
        /// Ex: "Duque Carlos" em vez de "Carlos, Duque"
        /// </summary>
        public static string GetCharacterTitlePrefix(FeudalTitle title)
        {
            if (title == null) return "";

            return TextHelper.GetTitlePrefix(title.TitleType, title.deJure);
        }

        /// <summary>
        /// Cria uma descrição detalhada do título para a enciclopédia
        /// </summary>
        public static string GetEncyclopediaDescription(FeudalTitle title, Settlement settlement, Kingdom holder)
        {
            if (title == null)
                return "Título desconhecido";

            string desc = "Título de " + TextHelper.GetRankName(title.TitleType);

            if (settlement != null)
                desc += "\n\nFeudo: " + settlement.Name;

            if (holder != null)
                desc += "\n\nDetentor: " + holder.Name;

            if (title.IsUsurped)
                desc += "\n\nStatus: USURPADO - Este título está sob disputa!";

            // Adicionar info do contrato
            if (title.Contract != null)
            {
                desc += $"\n\nGoverno: {title.Contract.Government.Name}";
                desc += $"\nSucessão: {title.Contract.Succession.Name}";
                desc += $"\nHerança: {title.Contract.Inheritance.Name}";
                desc += $"\nLei de Gênero: {title.Contract.GenderLaw.Name}";
            }

            return desc;
        }
    }
}