using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Library;

namespace CasusBelli
{
    // Comandos de teste do WarScore.
    public static class WarScoreDebugCommands
    {
        private static void LogToFile(string message)
        {
            try
            {
                string logPath = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                    "Mount and Blade II Bannerlord",
                    "Logs",
                    "casusbelli_debug.log");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath));
                System.IO.File.AppendAllText(logPath, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + System.Environment.NewLine);
            }
            catch (System.Exception)
            {
                // Silencioso
            }
        }

        // Lista as guerras ativas com o WarScore decomposto.
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "warscore", "casus_belli")]
        public static string WarScore(List<string> args)
        {
            LogToFile("WarScore comando CHAMADO");
            CasusBelliCampaignBehavior main;
            CasusBelliWarScoreBehavior ws;
            string message;

            if (!TryPrepare(out main, out ws, out message))
            {
                return message;
            }

            StringBuilder result = new StringBuilder();
            int count = 0;

            foreach (WarRecord war in main.GetWarRegistry().AllWars)
            {
                if (war == null || !war.IsActive)
                {
                    continue;
                }

                if (args != null && args.Count == 1 &&
                    war.AttackerKingdomId != args[0] &&
                    war.DefenderKingdomId != args[0])
                {
                    continue;
                }

                WarScoreBreakdown b = ws.GetBreakdown(war);
                count++;

                result.Append(war.WarId + " | " + war.AttackerKingdomId +
                    " x " + war.DefenderKingdomId +
                    " | atacante: " +
                    CasusBelliWarScoreBehavior.FormatPercent(b.Total) +
                    " (batalhas " + b.Battles.ToString("0.#") +
                    ", cercos " + b.Sieges.ToString("0.#") +
                    ", objetivo " + b.Objective.ToString("0.#") +
                    ", prisioneiros " + b.Prisoners.ToString("0.#") + ")" +
                    " | dias: " + ws.GetWarAgeDays(war).ToString("0") + "\n");
            }

            return count == 0
                ? "CB: nenhuma guerra ativa."
                : "CB: WarScore (perspectiva do atacante)\n" + result;
        }

        // Soma pontos a uma guerra (perspectiva do atacante; negativo
        // favorece o defensor). Tipo: battle ou siege.
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "add_ws", "casus_belli")]
        public static string AddWs(List<string> args)
        {
            CasusBelliCampaignBehavior main;
            CasusBelliWarScoreBehavior ws;
            string message;

            if (!TryPrepare(out main, out ws, out message))
            {
                return message;
            }

            double value;

            if (args == null || args.Count != 3 ||
                (args[1] != "battle" && args[1] != "siege") ||
                !double.TryParse(
                    args[2],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                return "Uso: casus_belli.add_ws <id_da_guerra> " +
                    "<battle|siege> <valor>";
            }

            return ws.DebugAdd(args[0], args[1], value)
                ? "CB: pontos somados. Veja casus_belli.warscore."
                : "CB: guerra nao encontrada: " + args[0];
        }

        // Abre o menu de guerra e paz (precisa estar no mapa, sem menu).
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "peace_menu", "casus_belli")]
        public static string PeaceMenu(List<string> args)
        {
            CasusBelliCampaignBehavior main;
            CasusBelliWarScoreBehavior ws;
            string message;

            if (!TryPrepare(out main, out ws, out message))
            {
                return message;
            }

            CasusBelliMenuBehavior.OpenPeaceMenuFromMap();
            return "CB: abrindo o menu de guerra e paz.";
        }

        // Roda agora a rotina diaria do WarScore (IA, avisos, limpeza).
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "run_peace_ai", "casus_belli")]
        public static string RunPeaceAi(List<string> args)
        {
            CasusBelliCampaignBehavior main;
            CasusBelliWarScoreBehavior ws;
            string message;

            if (!TryPrepare(out main, out ws, out message))
            {
                return message;
            }

            ws.RunDailyForDebug();
            return "CB: rotina diaria do WarScore executada.";
        }

        // Liga/desliga a interface do mapa (icone WS e janela).
        // Usado para diagnostico do tremer da camera: com isso desligado
        // a camara nao tremer, a causa e a UI do mod.
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "ws_ui", "casus_belli")]
        public static string WsUi(List<string> args)
        {
            string error = "";

            if (!CampaignCheats.CheckCheatUsage(ref error))
            {
                return error;
            }

            if (Campaign.Current == null)
            {
                return "CB: nenhuma campanha ativa.";
            }

            if (args != null && args.Count >= 1)
            {
                string mode = args[0].ToLowerInvariant();

                if (mode == "off")
                {
                    CasusBelliWarScoreBehavior.UiEnabled = false;
                    return "CB: interface do mapa DESLIGADA. " +
                        "Use casus_belli.ws_ui on para reativar.";
                }

                if (mode == "on")
                {
                    CasusBelliWarScoreBehavior.UiEnabled = true;
                    return "CB: interface do mapa LIGADA.";
                }
            }

            return "Uso: casus_belli.ws_ui on|off " +
                "(atual=" + CasusBelliWarScoreBehavior.UiEnabled + ")";
        }

        // Diagnostico da interface: diz se o botao WS esta carregado e,
        // se estiver, abre a janela de negociacao.
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "ws_window", "casus_belli")]
        public static string WsWindow(List<string> args)
        {
            CasusBelliCampaignBehavior main;
            CasusBelliWarScoreBehavior ws;
            string message;

            if (!TryPrepare(out main, out ws, out message))
            {
                return message;
            }

            Kingdom kingdom = Clan.PlayerClan != null
                ? Clan.PlayerClan.Kingdom
                : null;

            if (kingdom == null)
            {
                return "CB: seu cla nao esta em um reino.";
            }

            int wars = ws.GetActiveWarsOf(kingdom).Count;

            if (wars == 0)
            {
                return "CB: seu reino nao tem guerra registrada pelo mod " +
                    "(o icone so aparece em guerra). Use casus_belli.wars.";
            }

            if (CasusBelliWarScoreBehavior.WindowOpener == null)
            {
                return "CB: guerras ativas: " + wars + ", mas a interface " +
                    "NAO foi criada (UiLoaded=" +
                    CasusBelliWarScoreBehavior.UiLoaded + "). Verifique: " +
                    "simbolo CB_UIEX definido, modulo Bannerlord.UIExtenderEx " +
                    "ativo no launcher, e se o XPath/tipo MapInfoVM batem " +
                    "(ligue DumpXML no UIExtenderEx e veja o MapBar).";
            }

            CasusBelliWarScoreBehavior.WindowOpener();
            return "CB: abrindo a janela de negociacao.";
        }

        // Comando de teste: dispara um relatorio de batalha fake para verificar
        // se a interface esta funcionando. Uso: casus_belli.ws_test_report
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "ws_test_report", "casus_belli")]
        public static string WsTestReport(List<string> args)
        {
            LogToFile("CasusBelli: ws_test_report comando CHAMADO");
            CasusBelliCampaignBehavior main;
            CasusBelliWarScoreBehavior ws;
            string message;

            if (!TryPrepare(out main, out ws, out message))
            {
                return message;
            }

            // Cria um relatorio de teste
            BattleReportData testReport =
                new BattleReportData
            {
                SettlementName = "Castelo de Teste",
                HeroName = "Lorde Inimigo",
                Wounded = 150,
                Killed = 80,
                CapturedLords = new List<Hero>(),
                WarScoreChange = 12.5,
                PlayerWon = true
            };

            LogToFile("CasusBelli: ws_test_report disparando BattleReportReady manualmente");
            ws.TriggerTestBattleReport(testReport);
            return "CB: relatorio de teste disparado. Deve aparecer no log.";
        }

        private static bool TryPrepare(
            out CasusBelliCampaignBehavior main,
            out CasusBelliWarScoreBehavior ws,
            out string message)
        {
            main = null;
            ws = null;
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

            main = Campaign.Current.GetCampaignBehavior<
                CasusBelliCampaignBehavior>();
            ws = Campaign.Current.GetCampaignBehavior<
                CasusBelliWarScoreBehavior>();

            if (main == null || ws == null)
            {
                message = "CB: behavior do WarScore nao encontrado.";
                return false;
            }

            return true;
        }
    }
}
