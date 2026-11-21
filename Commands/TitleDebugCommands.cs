using System.Collections.Generic;
using System.Text;
using CasusBelli.Managers.Titles;
using CasusBelli.Managers.Titles.Governments;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Core;

namespace CasusBelli
{
    // Comandos de debug para o sistema de títulos feudais.
    public static class TitleDebugCommands
    {
        // Lista todos os títulos conhecidos.
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "list_titles", "casus_belli")]
        public static string ListTitles(List<string> args)
        {
            CasusBelliCampaignBehavior behavior;
            string message;

            if (!TryPrepare(out behavior, out message))
            {
                return message;
            }

            var titleManager = TitleManager.Instance;
            int count = 0;
            var result = new StringBuilder();

            foreach (var title in titleManager.AllTitles)
            {
                count++;
                Settlement settlement = title.Fief;
                string settlementName = settlement != null
                    ? settlement.Name.ToString()
                    : title.StringId ?? "Unknown";

                result.Append($"  {settlementName} ({title.StringId}): {title.TitleType} (de jure: {title.deJure?.Name?.ToString() ?? "Vacant"})\n");
            }

            return count == 0
                ? "CB: nenhum título registrado."
                : $"CB: títulos ({count}):\n{result}";
        }

        // Força a concessão de um título a um feudo específico.
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "grant_title", "casus_belli")]
        public static string GrantTitle(List<string> args)
        {
            CasusBelliCampaignBehavior behavior;
            string message;

            if (!TryPrepare(out behavior, out message))
            {
                return message;
            }

            if (args == null || args.Count < 2)
            {
                return "Uso: casus_belli.grant_title <id_feudo> <id_heroi>";
            }

            string settlementId = args[0];
            string heroId = args[1];

            Settlement settlement = Settlement.Find(settlementId);

            if (settlement == null)
            {
                return "CB: feudo não encontrado: " + settlementId;
            }

            Hero hero = Hero.FindFirst(x => x.StringId == heroId);
            if (hero == null)
            {
                return "CB: herói não encontrado: " + heroId;
            }

            var titleManager = TitleManager.Instance;
            var title = titleManager.GetTitle(settlement);

            if (title == null)
            {
                // Criar título se não existir
                var contract = new FeudalContract(
                    DefaultGovernments.Instance.Feudal,
                    DefaultSuccessions.Instance.Hereditary,
                    DefaultInheritances.Instance.Primogeniture,
                    DefaultGenderLaws.Instance.AgnaticCognatic);
                title = titleManager.CreateTitle(settlement, hero.Clan.Kingdom, contract);
            }

            if (title != null)
            {
                titleManager.GrantTitle(new TitleAction(ActionType.Grant, title, hero), hero);
                return $"CB: título concedido a {settlement.Name} ({title.TitleType}) para {hero.Name}";
            }

            return "CB: falhou ao conceder título.";
        }

        // Mostra o título do feudo onde o jogador está (se for fortificação).
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "player_title", "casus_belli")]
        public static string PlayerTitle(List<string> args)
        {
            if (!TryPrepare(out _, out string message))
            {
                return message;
            }

            if (Clan.PlayerClan == null)
            {
                return "CB: nenhum clã de jogador.";
            }

            // Encontrar um feudo fortificado controlado pelo clã do jogador
            Settlement playerSettlement = null;
            foreach (Settlement s in Settlement.All)
            {
                if (s.OwnerClan == Clan.PlayerClan && s.IsFortification)
                {
                    playerSettlement = s;
                    break;
                }
            }

            if (playerSettlement == null)
            {
                return "CB: o seu reino não possui um feudo jogável.";
            }

            var titleManager = TitleManager.Instance;
            var title = titleManager.GetTitle(playerSettlement);

            if (title == null)
            {
                return $"CB: nenhum título registrado para {playerSettlement.Name}.";
            }

            return $"CB: título de {playerSettlement.Name}: {title.TitleType} (de jure: {title.deJure?.Name?.ToString() ?? "Vacant"}, de facto: {title.DeFactoHolder?.Name?.ToString() ?? "Vacant"})";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("claims_em_andamento", "casus_belli")]
        public static string PendingClaims(List<string> args)
        {
            if (Campaign.Current == null || Hero.MainHero == null) return "Abra uma campanha.";
            var result = new StringBuilder();
            foreach (var title in TitleManager.Instance.AllTitles)
            {
                if (title.OngoingClaims.TryGetValue(Hero.MainHero, out var deadline))
                    result.AppendLine($"{title.StringId} — {title.FullName}: {System.Math.Ceiling(deadline.RemainingDaysFromNow):0} dias.");
            }
            return result.Length == 0 ? "Você não tem reivindicações em andamento." : result.ToString();
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("adiantar_claim", "casus_belli")]
        public static string AdvanceClaim(List<string> args)
        {
            if (args == null || args.Count != 2 || !int.TryParse(args[1], out int days) || days <= 0)
                return "Uso: casus_belli.adiantar_claim <id_do_título> <dias_positivos>";
            return ChangeClaimDeadline(args[0], days);
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("concluir_claim", "casus_belli")]
        public static string FinishClaim(List<string> args)
        {
            if (args == null || args.Count != 1) return "Uso: casus_belli.concluir_claim <id_do_título>";
            return ChangeClaimDeadline(args[0], null);
        }

        private static string ChangeClaimDeadline(string id, int? days)
        {
            if (Campaign.Current == null || Hero.MainHero == null) return "Abra uma campanha.";
            var title = TitleManager.Instance.GetTitleByStringId(id);
            if (title == null) return "Título desconhecido: " + id;
            if (!title.AdvanceClaim(Hero.MainHero, days))
                return "Você não tem uma reivindicação em andamento sobre " + title.FullName + ".";
            if (title.OngoingClaims.TryGetValue(Hero.MainHero, out var deadline))
                return $"{title.FullName}: faltam {System.Math.Ceiling(deadline.RemainingDaysFromNow):0} dias.";
            return "Reivindicação madura: " + title.FullName + ".";
        }

        private static bool TryPrepare(
            out CasusBelliCampaignBehavior behavior,
            out string message)
        {
            behavior = null;
            message = "";

            if (Campaign.Current == null)
            {
                message = "CB: nenhuma campanha ativa.";
                return false;
            }

            behavior = Campaign.Current.GetCampaignBehavior<CasusBelliCampaignBehavior>();

            if (behavior == null)
            {
                message = "CB: comportamento CasusBelliCampaignBehavior não encontrado.";
                return false;
            }

            return true;
        }
    }
}