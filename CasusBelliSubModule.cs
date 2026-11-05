#if CB_UIEX
using Bannerlord.UIExtenderEx;
#endif
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CasusBelli
{
    public class CasusBelliSubModule : MBSubModuleBase
    {
#if CB_UIEX
        private static readonly UIExtender Extender =
            UIExtender.Create("CasusBelli");
#endif

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

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
            InformationManager.DisplayMessage(
                new InformationMessage("Casus Belli carregado com sucesso!"));
        }

        protected override void OnGameStart(
            Game game,
            IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);

            if (game.GameType is Campaign &&
                gameStarter is CampaignGameStarter campaignStarter)
            {
                campaignStarter.AddBehavior(
                    new CasusBelliCampaignBehavior());

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

                // IA do jogo base passa a considerar o casus belli.
                // Se outro mod tambem troca o DiplomacyModel, so um vence.
                campaignStarter.AddModel(
                    new CasusBelliDiplomacyModel());
            }
        }
    }
}
