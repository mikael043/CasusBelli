#if CB_UIEX
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Library;

namespace CasusBelli
{
    // MapInfoBarWidget e o DataSource MapInfo existem no MapBar atual.
    [PrefabExtension(
        "MapBar",
        "descendant::MapInfoBarWidget[@Id='InfoBarWidget']")]
    internal sealed class CasusBelliMapBarPatch : PrefabExtensionInsertPatch
    {
        // ------------------------------------------------------------
        // SPRITES DO ESCUDO (atlas gerado pelo Modding Kit)
        // ------------------------------------------------------------
        // O nome do sprite e o caminho DENTRO da pasta da categoria, sem a
        // extensao e SEM o nome da categoria. Exemplo: o arquivo
        //   GUI/SpriteParts/ui_CbWarLayers/Espada.png   ->  "Espada"
        // Confira os nomes exatos em <Modulo>/GUI/CasusBelliSpriteData.xml
        // (campo <Name> de cada <SpritePart>) e copie para ca.
        private const string SpriteSwords = "Espada";
        private const string SpriteLeft = "Escudo-Esquerda";   // mascara BRANCA
        private const string SpriteRight = "Escudo_Direita";   // mascara BRANCA
        private const string SpriteOutline = "Escudo_Contorno";

        // true: desenha um escudo de blocos coloridos POR BAIXO dos sprites.
        // Serve de teste: se voce ve o escudo de blocos mas nao os sprites,
        // o icone esta funcionando e o problema e so o atlas/nome do sprite.
        // Depois que os sprites aparecerem, coloque false.
        private const bool ShowFallbackShield = true;

        public override InsertType Type
        {
            get { return InsertType.Append; }
        }

        [PrefabExtensionText]
        public string Content
        {
            get
            {
                string icon = IconXml
                    .Replace("__SWORDS__", SpriteSwords)
                    .Replace("__LEFT__", SpriteLeft)
                    .Replace("__RIGHT__", SpriteRight)
                    .Replace("__OUTLINE__", SpriteOutline)
                    .Replace("__FALLBACK__", ShowFallbackShield ? FallbackXml : "");

                return Wrapper.Replace("__ICON__", icon).Replace("__WINDOW__", WindowXml);
            }
        }

        // Este container ocupa a tela toda, mas NAO pode consumir cliques do mapa.
        private const string Wrapper = @"
<Widget DataSource=""{MapInfo}"" DoNotAcceptEvents=""true""
        WidthSizePolicy=""StretchToParent""
        HeightSizePolicy=""StretchToParent"">
  <Children>
__ICON__
__WINDOW__
  </Children>
</Widget>";

        private const string IconXml = @"<Widget DataSource=""{CbIcon}"" IsVisible=""@IsVisible""
        WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
        SuggestedWidth=""72"" SuggestedHeight=""72""
        HorizontalAlignment=""Left"" VerticalAlignment=""Top""
        MarginLeft=""16"" MarginTop=""16"">
  <Children>
    <ButtonWidget DoNotPassEventsToChildren=""true"" UpdateChildrenStates=""true""
                  WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                  Command.Click=""ExecuteOpen"">
      <Children>
__FALLBACK__
        <Widget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                Sprite=""__SWORDS__"" />
        <Widget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                Sprite=""__LEFT__"" Color=""@LeftColor"" />
        <Widget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                Sprite=""__RIGHT__"" Color=""@ShieldColor"" />
        <Widget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                Sprite=""__OUTLINE__"" />
        <!-- placa da porcentagem -->
        <Widget WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                SuggestedWidth=""64"" SuggestedHeight=""22""
                HorizontalAlignment=""Center"" VerticalAlignment=""Bottom""
                MarginBottom=""-22""
                Sprite=""BlankWhiteSquare_9"" Color=""#1A1511E6"" />
        <Widget WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                SuggestedWidth=""64"" SuggestedHeight=""3""
                HorizontalAlignment=""Center"" VerticalAlignment=""Bottom""
                MarginBottom=""-22""
                Sprite=""BlankWhiteSquare_9"" Color=""@PercentColor"" />
        <TextWidget WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                    SuggestedWidth=""64"" SuggestedHeight=""20""
                    HorizontalAlignment=""Center"" VerticalAlignment=""Bottom""
                    MarginBottom=""-21""
                    Brush.FontSize=""18"" Brush.FontColor=""#F4E1C1FF""
                    Brush.TextHorizontalAlignment=""Center""
                    Brush.TextVerticalAlignment=""Center""
                    Text=""@PercentText"" />
      </Children>
    </ButtonWidget>
  </Children>
</Widget>";

        // Escudo de blocos (fundo de teste). Cabe dentro do icone de 72x72.
        private const string FallbackXml = @"        <Widget WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                SuggestedWidth=""44"" SuggestedHeight=""52""
                HorizontalAlignment=""Center"" VerticalAlignment=""Center""
                Sprite=""BlankWhiteSquare_9"" Color=""#C9A24AFF"" />
        <Widget WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                SuggestedWidth=""20"" SuggestedHeight=""46""
                HorizontalAlignment=""Center"" VerticalAlignment=""Center""
                MarginRight=""20""
                Sprite=""BlankWhiteSquare_9"" Color=""@LeftColor"" />
        <Widget WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                SuggestedWidth=""20"" SuggestedHeight=""46""
                HorizontalAlignment=""Center"" VerticalAlignment=""Center""
                MarginLeft=""20""
                Sprite=""BlankWhiteSquare_9"" Color=""@ShieldColor"" />";

        private const string WindowXml = @"<Widget DataSource=""{CbWindow}"" IsVisible=""@IsOpen""
        WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
        SuggestedWidth=""640"" SuggestedHeight=""510""
        HorizontalAlignment=""Center"" VerticalAlignment=""Center"">
  <Children>
    <Widget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
            Sprite=""BlankWhiteSquare_9"" Color=""#18130EF5"" />
    <ListPanel WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
               MarginLeft=""28"" MarginRight=""28"" MarginTop=""24"" MarginBottom=""20""
               StackLayout.LayoutMethod=""VerticalTopToBottom"">
      <Children>
        <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                    SuggestedHeight=""38"" Brush.FontSize=""28"" Brush.FontColor=""#D4AF37FF""
                    Brush.TextHorizontalAlignment=""Center"" Text=""@Title"" />
        <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                    SuggestedHeight=""42"" Brush.FontSize=""28"" Brush.FontColor=""#F4E1C1FF""
                    Brush.TextHorizontalAlignment=""Center"" Text=""@ScoreText"" />
        <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                    SuggestedHeight=""26"" Brush.FontSize=""18"" Brush.FontColor=""#F4E1C1FF"" Text=""@BattlesText"" />
        <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                    SuggestedHeight=""26"" Brush.FontSize=""18"" Brush.FontColor=""#F4E1C1FF"" Text=""@SiegesText"" />
        <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                    SuggestedHeight=""26"" Brush.FontSize=""18"" Brush.FontColor=""#F4E1C1FF"" Text=""@ObjectiveText"" />
        <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                    SuggestedHeight=""26"" Brush.FontSize=""18"" Brush.FontColor=""#F4E1C1FF"" Text=""@PrisonersText"" />
        <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                    SuggestedHeight=""50"" Brush.FontSize=""16"" Brush.FontColor=""#BFAE8AFF"" Text=""@StatusText"" />
        <Widget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed"" SuggestedHeight=""1""
                Sprite=""BlankWhiteSquare_9"" Color=""#C9A24AFF"" />
        <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                    SuggestedHeight=""54"" MarginTop=""8"" Brush.FontSize=""16""
                    Brush.FontColor=""#BFAE8AFF"" Text=""@DemandsText"" />
        <ListPanel WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                   SuggestedHeight=""60"" MarginTop=""8""
                   StackLayout.LayoutMethod=""HorizontalLeftToRight"">
          <Children>
            <ButtonWidget DoNotPassEventsToChildren=""true"" UpdateChildrenStates=""true""
                          WidthSizePolicy=""Fixed"" HeightSizePolicy=""StretchToParent""
                          SuggestedWidth=""184"" MarginRight=""8"" Brush=""ButtonBrush1""
                          IsEnabled=""@EnforceEnabled"" Command.Click=""ExecuteEnforce"">
              <Children><TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                                    Brush.TextHorizontalAlignment=""Center"" Brush.TextVerticalAlignment=""Center""
                                    Text=""Forcar exigencias"" /></Children>
            </ButtonWidget>
            <ButtonWidget DoNotPassEventsToChildren=""true"" UpdateChildrenStates=""true""
                          WidthSizePolicy=""Fixed"" HeightSizePolicy=""StretchToParent""
                          SuggestedWidth=""184"" MarginRight=""8"" Brush=""ButtonBrush1""
                          IsEnabled=""@WhiteEnabled"" Command.Click=""ExecuteWhitePeace"">
              <Children><TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                                    Brush.TextHorizontalAlignment=""Center"" Brush.TextVerticalAlignment=""Center""
                                    Text=""Paz branca"" /></Children>
            </ButtonWidget>
            <ButtonWidget DoNotPassEventsToChildren=""true"" UpdateChildrenStates=""true""
                          WidthSizePolicy=""Fixed"" HeightSizePolicy=""StretchToParent""
                          SuggestedWidth=""184"" Brush=""ButtonBrush1""
                          IsEnabled=""@SurrenderEnabled"" Command.Click=""ExecuteSurrender"">
              <Children><TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                                    Brush.TextHorizontalAlignment=""Center"" Brush.TextVerticalAlignment=""Center""
                                    Text=""Render-se"" /></Children>
            </ButtonWidget>
          </Children>
        </ListPanel>
        <ButtonWidget DoNotPassEventsToChildren=""true"" UpdateChildrenStates=""true""
                      WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed"" SuggestedWidth=""180""
                      SuggestedHeight=""42"" HorizontalAlignment=""Center"" MarginTop=""10""
                      Brush=""ButtonBrush1"" Command.Click=""ExecuteClose"">
          <Children><TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                                Brush.TextHorizontalAlignment=""Center"" Brush.TextVerticalAlignment=""Center""
                                Text=""Fechar"" /></Children>
        </ButtonWidget>
      </Children>
    </ListPanel>
  </Children>
</Widget>";
    }

    [ViewModelMixin]
    internal sealed class CasusBelliMapInfoMixin : BaseViewModelMixin<MapInfoVM>
    {
        private readonly CasusBelliWarIconVM _icon;
        private readonly CasusBelliWarWindowVM _window;

        public CasusBelliMapInfoMixin(MapInfoVM vm) : base(vm)
        {
            _icon = new CasusBelliWarIconVM(OpenWindow, null);
            _window = new CasusBelliWarWindowVM(CloseWindow);

            CasusBelliWarScoreBehavior.Changed += OnScoreChanged;
            CasusBelliWarScoreBehavior.WindowOpener = OpenWindow;
            CasusBelliWarScoreBehavior.UiLoaded = true;
            _icon.Refresh();
        }

        [DataSourceProperty]
        public CasusBelliWarIconVM CbIcon { get { return _icon; } }

        [DataSourceProperty]
        public CasusBelliWarWindowVM CbWindow { get { return _window; } }

        public override void OnRefresh()
        {
            base.OnRefresh();
            _icon.Refresh();
        }

        public override void OnFinalize()
        {
            CasusBelliWarScoreBehavior.Changed -= OnScoreChanged;

            if (CasusBelliWarScoreBehavior.WindowOpener ==
                (System.Action)OpenWindow)
            {
                CasusBelliWarScoreBehavior.WindowOpener = null;
            }

            CasusBelliWarScoreBehavior.UiLoaded = false;
            base.OnFinalize();
        }

        private void OnScoreChanged()
        {
            _icon.Refresh();

            if (_window.IsOpen)
            {
                _window.Refresh();
            }
        }

        private void OpenWindow()
        {
            CasusBelliWarScoreBehavior warScore = WarUiHelper.GetWarScore();
            Kingdom kingdom = WarUiHelper.GetPlayerKingdom();

            if (warScore == null || kingdom == null ||
                warScore.GetSelectedWar(kingdom) == null)
            {
                WarUiHelper.Message("CB: seu reino nao esta em guerra.");
                return;
            }

            _window.IsOpen = true;
            _window.Refresh();
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
        }

        private void CloseWindow()
        {
            _window.IsOpen = false;
            _icon.Refresh();
        }
    }
}
#endif
