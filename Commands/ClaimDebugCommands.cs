using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CasusBelli
{
    public static class ClaimDebugCommands
    {
        // ---------------- claims ----------------

        [CommandLineFunctionality.CommandLineArgumentFunction(
            "declare_claim_war", "casus_belli")]
        public static string DeclareClaimWar(List<string> args)
        {
            CasusBelliCampaignBehavior behavior;
            string message;

            if (!TryPrepare(out behavior, out message))
            {
                return message;
            }

            if (args == null || args.Count < 2 || args.Count > 3)
            {
                return "Uso: casus_belli.declare_claim_war " +
                    "<id_do_reino_atacante> <id_do_feudo> [force]";
            }

            bool force = args.Count == 3 && args[2] == "force";
            string result;

            bool declared = behavior.TryDeclareClaimWar(
                args[0],
                args[1],
                force,
                out result);

            return (declared ? "CB: " : "CB: falhou - ") + result;
        }

        [CommandLineFunctionality.CommandLineArgumentFunction(
            "wars", "casus_belli")]
        public static string Wars(List<string> args)
        {
            CasusBelliCampaignBehavior behavior;
            string message;

            if (!TryPrepare(out behavior, out message))
            {
                return message;
            }

            StringBuilder result = new StringBuilder();
            int count = 0;

            foreach (WarRecord war in behavior.GetWarRegistry().AllWars)
            {
                if (war == null)
                {
                    continue;
                }

                count++;
                result.Append(war.WarId);
                result.Append(war.IsActive ? " | ATIVA" : " | encerrada");
                result.Append(" | ");
                result.Append(war.AttackerKingdomId);
                result.Append(" -> ");
                result.Append(war.DefenderKingdomId);
                result.Append(" | Tipo: ");
                result.Append(war.CasusBelliType);
                result.Append(" | Alvo: ");
                result.Append(string.IsNullOrEmpty(war.TargetSettlementId)
                    ? "(nenhum)"
                    : war.TargetSettlementId);
                result.Append(" | Inicio: ");
                result.Append(war.StartedOnDay < 0.0
                    ? "?"
                    : war.StartedOnDay.ToString("0"));
                result.Append(" | Fim: ");
                result.Append(war.EndedOnDay < 0.0
                    ? "-"
                    : war.EndedOnDay.ToString("0"));
                result.Append(" | Objetivo: ");
                result.Append(war.ObjectiveAchievedOnDay < 0.0
                    ? "-"
                    : "dia " + war.ObjectiveAchievedOnDay.ToString("0"));
                result.Append(" | Resultado: ");
                result.Append(string.IsNullOrEmpty(war.Outcome)
                    ? "-"
                    : war.Outcome);
                result.Append("\n");
            }

            if (count == 0)
            {
                return "CB: nenhuma ficha de guerra registrada.";
            }

            result.Insert(0, "CB: fichas de guerra: " + count + "\n");
            return result.ToString();
        }

        [CommandLineFunctionality.CommandLineArgumentFunction(
            "truce", "casus_belli")]
        public static string Truce(List<string> args)
        {
            CasusBelliCampaignBehavior behavior;
            string message;

            if (!TryPrepare(out behavior, out message))
            {
                return message;
            }

            if (args == null || args.Count != 2)
            {
                return "Uso: casus_belli.truce <id_do_reino> <id_do_reino>";
            }

            bool active = behavior.IsTruceActive(args[0], args[1]);

            return "CB: tregua entre " + args[0] + " e " + args[1] + ": " +
                (active ? "EM VIGOR" : "nao ha");
        }

        // ---------------- fabricacao ----------------

        [CommandLineFunctionality.CommandLineArgumentFunction(
            "status", "casus_belli")]
        public static string Status(List<string> args)
        {
            CasusBelliCampaignBehavior behavior;
            string message;

            if (!TryPrepare(out behavior, out message))
            {
                return message;
            }

            if (args == null || args.Count < 1 || args.Count > 2)
            {
                return "Uso: casus_belli.status <id_do_feudo> [id_do_reino]";
            }

            Settlement settlement = Settlement.Find(args[0]);

            if (settlement == null)
            {
                return "CB: feudo nao encontrado: " + args[0];
            }

            Kingdom viewer = null;

            if (args.Count == 2)
            {
                foreach (Kingdom kingdom in Kingdom.All)
                {
                    if (kingdom.StringId == args[1])
                    {
                        viewer = kingdom;
                    }
                }
            }
            else if (Clan.PlayerClan != null)
            {
                viewer = Clan.PlayerClan.Kingdom;
            }

            return "CB: " + behavior.BuildSettlementStatus(settlement, viewer);
        }

        // Roda a IA uma vez agora, com chance de 100% (so para teste).
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "run_ai", "casus_belli")]
        public static string RunAi(List<string> args)
        {
            CasusBelliCampaignBehavior behavior;
            string message;

            if (!TryPrepare(out behavior, out message))
            {
                return message;
            }

            behavior.RunAiForDebug();

            return "CB: IA executada uma vez. Veja casus_belli.projects e " +
                "casus_belli.wars.";
        }

        // Nota que a IA do jogo base da para "A declarar guerra a B", ja com
        // o fator do casus belli. Teste: rode, depois use add_claim contra um
        // feudo de B e rode de novo: a nota deve mudar (x3 se positiva).
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "score", "casus_belli")]
        public static string Score(List<string> args)
        {
            CasusBelliCampaignBehavior behavior;
            string message;

            if (!TryPrepare(out behavior, out message))
            {
                return message;
            }

            if (args == null || args.Count != 2)
            {
                return "Uso: casus_belli.score <reino_atacante> " +
                    "<reino_defensor>";
            }

            Kingdom attacker = FindKingdom(args[0]);
            Kingdom defender = FindKingdom(args[1]);

            if (attacker == null || defender == null)
            {
                return "CB: reino nao encontrado.";
            }

            TextObject reason;

            float score =
                Campaign.Current.Models.DiplomacyModel.GetScoreOfDeclaringWar(
                    attacker,
                    defender,
                    attacker.RulingClan,
                    out reason,
                    false);

            string kind;

            if (behavior.IsTruceActive(attacker.StringId, defender.StringId))
            {
                kind = "TREGUA (fator 0.05)";
            }
            else if (behavior.HasUsableClaimAgainst(attacker, defender))
            {
                kind = "COM claim (fator 1.5)";
            }
            else
            {
                kind = "SEM claim (fator 0.5)";
            }

            return "CB: nota de guerra " + attacker.StringId + " -> " +
                defender.StringId + ": " +
                score.ToString("0.##", CultureInfo.InvariantCulture) +
                " | " + kind;
        }

        // Quantas guerras de cada tipo e com cada resultado. E o placar para
        // saber se a IA esta usando o casus belli.
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "war_stats", "casus_belli")]
        public static string WarStats(List<string> args)
        {
            CasusBelliCampaignBehavior behavior;
            string message;

            if (!TryPrepare(out behavior, out message))
            {
                return message;
            }

            Dictionary<string, int> byType = new Dictionary<string, int>();
            Dictionary<string, int> byOutcome = new Dictionary<string, int>();
            int active = 0;
            int total = 0;

            foreach (WarRecord war in behavior.GetWarRegistry().AllWars)
            {
                if (war == null)
                {
                    continue;
                }

                total++;

                if (war.IsActive)
                {
                    active++;
                }

                string type = string.IsNullOrEmpty(war.CasusBelliType)
                    ? "(vazio)"
                    : war.CasusBelliType;
                string outcome = string.IsNullOrEmpty(war.Outcome)
                    ? "(em andamento)"
                    : war.Outcome;

                int value;
                byType.TryGetValue(type, out value);
                byType[type] = value + 1;

                byOutcome.TryGetValue(outcome, out value);
                byOutcome[outcome] = value + 1;
            }

            StringBuilder result = new StringBuilder();
            result.Append("CB: guerras registradas: " + total +
                " (ativas: " + active + ")\n");

            result.Append("Por tipo:\n");
            foreach (KeyValuePair<string, int> pair in byType)
            {
                result.Append("  " + pair.Key + ": " + pair.Value + "\n");
            }

            result.Append("Por resultado:\n");
            foreach (KeyValuePair<string, int> pair in byOutcome)
            {
                result.Append("  " + pair.Key + ": " + pair.Value + "\n");
            }

            return result.ToString();
        }

        // ---------------- apoio ----------------

        private static bool TryPrepare(
            out CasusBelliCampaignBehavior behavior,
            out string message)
        {
            behavior = null;
            message = "";

            string error = "";

            if (!CampaignCheats.CheckCheatUsage(ref error))
            {
                message = error;
                return false;
            }

            if (Campaign.Current == null)
            {
                message = "CB: nenhuma campanha ativa.";
                return false;
            }

            behavior =
                Campaign.Current.GetCampaignBehavior<
                    CasusBelliCampaignBehavior>();

            if (behavior == null)
            {
                message = "CB: comportamento da campanha nao encontrado.";
                return false;
            }

            return true;
        }

        private static Kingdom FindKingdom(string kingdomId)
        {
            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom.StringId == kingdomId)
                {
                    return kingdom;
                }
            }

            return null;
        }

        private static bool KingdomExists(string kingdomId)
        {
            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom.StringId == kingdomId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
