#if CB_HARMONY
using HarmonyLib;

#endif
#if CB_UIEX
using Bannerlord.UIExtenderEx;

#endif
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.TwoDimension;
using TaleWorlds.Engine;

namespace CasusBelli
{
    public class CasusBelliSubModule : MBSubModuleBase
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

#if CB_UIEX
        private static readonly UIExtender Extender =
            UIExtender.Create("CasusBelli");
#endif

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
#if CB_HARMONY
            try
            {
                var harmony = new HarmonyLib.Harmony("com.casusbelli.patch");
                harmony.PatchAll(typeof(CasusBelliSubModule).Assembly);
                LogToFile("CasusBelli: Patches do Harmony aplicados com sucesso.");
            }
            catch (System.Exception ex)
            {
                LogToFile("CasusBelli: Erro ao aplicar patches do Harmony: " + ex.Message);
            }
#endif
#if CB_UIEX
            // Procura [PrefabExtension] e [ViewModelMixin] neste assembly
            // (CasusBelliUiExtender.cs) e liga a interface do WarScore.
            Extender.Register(typeof(CasusBelliSubModule).Assembly);
            Extender.Enable();
#endif
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            LogToFile("CasusBelli: OnBeforeInitialModuleScreenSetAsRoot");
            InformationManager.DisplayMessage(
                new InformationMessage("Casus Belli carregado com sucesso!"));

        }

        protected override void OnGameStart(
            Game game,
            IGameStarter gameStarter)
        {
            LogToFile("CasusBelli: OnGameStart gameType=" + (game?.GameType?.ToString() ?? "null"));
            base.OnGameStart(game, gameStarter);

            if (game.GameType is Campaign &&
                gameStarter is CampaignGameStarter campaignStarter)
            {
                LogToFile("CasusBelli: Adicionando behaviors...");
                campaignStarter.AddBehavior(
                    new CasusBelliCampaignBehavior());

                Managers.Titles.TitleManager.BeginCampaign();

                // Title system - BannerKings equivalent feudal titles
                campaignStarter.AddBehavior(
                    new Managers.Titles.TitleCampaignBehavior());
                campaignStarter.AddBehavior(new RoyalCouncil());
                campaignStarter.AddBehavior(new ArmySummonsBehavior());
                campaignStarter.AddBehavior(new RealmPeers());
                campaignStarter.AddBehavior(new TitleDisputeBehavior());

                // Captura do soberano, negociacao e rancor.
                // Para desligar: remova esta linha (e a opcao de menu
                // de negociacao deixa de aparecer sozinha).
                campaignStarter.AddBehavior(
                    new CasusBelliKingBehavior());

                // WarScore e paz (estilo CK2). Substitui a paz padrao do jogo
                // nas guerras registradas pelo mod.
                campaignStarter.AddBehavior(
                    new CasusBelliWarScoreBehavior());

#if CB_GAUNTLET
                // Botao "WS:nn%" na tela do mapa (opcional, veja o
                // cabecalho de CasusBelliWarScoreHud.cs).
                campaignStarter.AddBehavior(
                    new CasusBelliWarScoreHudBehavior());
#endif

                // Menu jogavel (reivindicacoes, guerra e paz, negociacao).
                campaignStarter.AddBehavior(
                    new CasusBelliMenuBehavior());

                // UI do sistema de títulos (BannerKings equivalent)
                campaignStarter.AddBehavior(
                    new TitleUIBehavior());

                // IA do jogo base passa a considerar o casus belli.
                // Se outro mod tambem troca o DiplomacyModel, so um vence.
                campaignStarter.AddModel(
                    new CasusBelliDiplomacyModel());

                LogToFile("CasusBelli: Behaviors adicionados com sucesso");
            }
        }
    }
}
