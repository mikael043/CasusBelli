using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace CasusBelli.Managers.Titles
{
    // Helper para textos de títulos
    public static class TextHelper
    {
        private static readonly Dictionary<TitleType, string> _titlePrefixesMale = new()
        {
            { TitleType.Empire, "Imperador" },
            { TitleType.Kingdom, "Rei" },
            { TitleType.Dukedom, "Duque" },
            { TitleType.County, "Conde" },
            { TitleType.Barony, "Barão" },
            { TitleType.Lordship, "Senhor" }
        };

        private static readonly Dictionary<TitleType, string> _titlePrefixesFemale = new()
        {
            { TitleType.Empire, "Imperatriz" },
            { TitleType.Kingdom, "Rainha" },
            { TitleType.Dukedom, "Duquesa" },
            { TitleType.County, "Condessa" },
            { TitleType.Barony, "Baronesa" },
            { TitleType.Lordship, "Senhora" }
        };

        private static readonly Dictionary<TitleType, string> _rankNames = new()
        {
            { TitleType.Empire, "Império" },
            { TitleType.Kingdom, "Reino" },
            { TitleType.Dukedom, "Ducado" },
            { TitleType.County, "Condado" },
            { TitleType.Barony, "Baronia" },
            { TitleType.Lordship, "Senhorio" }
        };

        // Obter prefixo do título baseado no tipo e gênero
        public static string GetTitlePrefix(TitleType type, CultureObject culture = null, bool isFemale = false)
        {
            if (type == TitleType.Kingdom || type == TitleType.Empire)
            {
                switch (culture?.StringId)
                {
                    case "vlandia": return isFemale ? "Rainha" : "Rei";
                    case "empire": return isFemale ? "Imperatriz" : "Imperador";
                    case "khuzait": return "Khan";
                    case "sturgia": return isFemale ? "Grã-Princesa" : "Grão-Príncipe";
                    case "aserai": return isFemale ? "Sultana" : "Sultão";
                    case "battania": return isFemale ? "Alta-Rainha" : "Alto-Rei";
                }
            }
            var dict = isFemale ? _titlePrefixesFemale : _titlePrefixesMale;
            return dict.TryGetValue(type, out var prefix) ? prefix : "Senhor";
        }

        // Obter prefixo do título (usa gênero do deJure)
        public static string GetTitlePrefix(TitleType type, bool isFemale)
        {
            var dict = isFemale ? _titlePrefixesFemale : _titlePrefixesMale;
            return dict.TryGetValue(type, out var prefix) ? prefix : "Senhor";
        }

        // Obter prefixo do título (compatibilidade)
        public static string GetTitlePrefix(TitleType type, Hero hero = null)
        {
            bool isFemale = hero != null && hero.IsFemale;
            return GetTitlePrefix(type, hero?.Clan?.Kingdom?.Culture ?? hero?.Clan?.Culture, isFemale);
        }

        // Obter nome do rank
        public static string GetRankName(TitleType type)
        {
            return _rankNames.TryGetValue(type, out var name) ? name : "Título";
        }

        // Obter rank com artigo
        public static string RankWithArticle(TitleType rank, string kingdomId)
        {
            string rankName = GetRankName(rank);
            string kingdomName = GetKingdomName(kingdomId);

            if (!string.IsNullOrEmpty(kingdomName))
                return $"{rankName} de {kingdomName}";
            return $"{rankName}";
        }

        // Obter rank padrão para um assentamento
        public static TitleType GetDefaultRank(Settlement settlement)
        {
            if (settlement.IsTown)
                return TitleType.County;
            else if (settlement.IsCastle)
                return TitleType.Barony;
            else
                return TitleType.Lordship;
        }

        // Obter nome do reino
        private static string GetKingdomName(string kingdomId)
        {
            if (string.IsNullOrEmpty(kingdomId))
                return "";

            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom != null && kingdom.StringId == kingdomId)
                {
                    return kingdom.Name.ToString();
                }
            }

            return kingdomId;
        }

        // Formatar nome completo do título
        public static TextObject FormatFullName(TitleType type, TextObject name)
        {
            return new TextObject("{=!}{TITLE} de {NAME}")
                .SetTextVariable("TITLE", GetRankName(type))
                .SetTextVariable("NAME", name);
        }

        // Obter prefixo para nome de personagem (ex: "Duke Carlos")
        public static string GetCharacterTitlePrefix(TitleType type, bool isFemale = false)
        {
            var dict = isFemale ? _titlePrefixesFemale : _titlePrefixesMale;
            return dict.TryGetValue(type, out var prefix) ? prefix : "";
        }
    }
}