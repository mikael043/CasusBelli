#if CB_UIEX
using System;
using System.Collections.Generic;
using System.IO;
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
        public override InsertType Type
        {
            get { return InsertType.Append; }
        }

        [PrefabExtensionText]
        public string Content
        {
            get
            {
                // Diagnostico: casus_belli.ws_ui off desliga a interface
                // do mapa para isolar o tremer da camera.
                if (!CasusBelliWarScoreBehavior.UiEnabled)
                {
                    return "";
                }

                return @"
<!-- Inserted as a sibling of InfoBarWidget to avoid resizing the map bar. -->
<Widget DataSource=""{MapInfo}"" DoNotAcceptEvents=""true""
        WidthSizePolicy=""StretchToParent""
        HeightSizePolicy=""StretchToParent"">
  <Children>
<ListPanel DataSource=""{CbIcons}"" DoNotAcceptEvents=""false""
           WidthSizePolicy=""CoverChildren"" HeightSizePolicy=""Fixed""
           SuggestedHeight=""90"" HorizontalAlignment=""Left"" VerticalAlignment=""Top""
           MarginLeft=""16"" MarginTop=""16""
           StackLayout.LayoutMethod=""HorizontalLeftToRight"">
  <ItemTemplate>
    <Widget IsVisible=""@IsVisible"" DoNotAcceptEvents=""false""
            WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
            SuggestedWidth=""72"" SuggestedHeight=""72"" MarginRight=""12"">
      <Children>
  
    <ButtonWidget DoNotPassEventsToChildren=""true"" UpdateChildrenStates=""true""
                  WidthSizePolicy=""StretchToParent""
                  HeightSizePolicy=""StretchToParent""
                  Command.Click=""ExecuteOpen"">
                 <Children>

                  <Widget DoNotAcceptEvents=""true""
                          WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                          Sprite=""CbWar\Swords"" />
                  <Widget DoNotAcceptEvents=""true""
                          WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                          Sprite=""CbWar\ShieldLeft"" Color=""@LeftColor"" />
                  <Widget DoNotAcceptEvents=""true""
                          WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                          Sprite=""CbWar\ShieldRight"" Color=""@ShieldColor"" />
                  <Widget DoNotAcceptEvents=""true""
                          WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                          Sprite=""CbWar\ShieldOutline"" />

                  <!-- TEXTO QUE FICA POR CIMA DO BOTÃO MODULAR -->
                  <TextWidget WidthSizePolicy=""StretchToParent""
                    HeightSizePolicy=""Fixed"" SuggestedHeight=""22""
                    VerticalAlignment=""Bottom"" MarginBottom=""-18""
                    Brush.FontSize=""18"" Brush.FontColor=""#F4E1C1FF""
                    Brush.TextHorizontalAlignment=""Center"" DoNotAcceptEvents=""true""
                    Text=""@PercentText"" />
      </Children>
    </ButtonWidget>
      </Children>
    </Widget>
  </ItemTemplate>
</ListPanel>
<!-- Disabled windows are removed from hit testing as well as rendering. -->
<Widget DataSource=""{CbWindow}"" IsVisible=""@IsOpen"" IsEnabled=""@IsOpen""
        DoNotAcceptEvents=""true""
        WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
        SuggestedWidth=""640"" SuggestedHeight=""510""
        HorizontalAlignment=""Center"" VerticalAlignment=""Center"">
  <Children>
    <Widget DoNotAcceptEvents=""true""
            WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
            Sprite=""BlankWhiteSquare_9"" Color=""#18130EF5"" />
    <ListPanel DoNotAcceptEvents=""true""
               WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
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
                                    Text=""Forçar exigências"" /></Children>
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
</Widget>
  <!-- Battle Report window (mostrado 1h apos fim da missao) -->
  <Widget DataSource=""{CbBattleReport}"" IsVisible=""@IsVisible"" IsEnabled=""@IsVisible""
          DoNotAcceptEvents=""true""
          WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
          SuggestedWidth=""720"" SuggestedHeight=""480""
          HorizontalAlignment=""Center"" VerticalAlignment=""Center"">
    <Children>
      <Widget DoNotAcceptEvents=""true""
              WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
              Sprite=""BlankWhiteSquare_9"" Color=""#18130EF5"" />
      <ListPanel DoNotAcceptEvents=""true""
                 WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                 MarginLeft=""32"" MarginRight=""32"" MarginTop=""28"" MarginBottom=""28""
                 StackLayout.LayoutMethod=""VerticalTopToBottom"">
        <Children>
          <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                      SuggestedHeight=""42"" Brush.FontSize=""32"" Brush.FontColor=""#D4AF37FF""
                      Brush.TextHorizontalAlignment=""Center"" Text=""@Title"" />
          <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                      SuggestedHeight=""60"" Brush.FontSize=""20"" Brush.FontColor=""#F4E1C1FF""
                      Brush.TextHorizontalAlignment=""Center"" Text=""@Description"" />
          <Widget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed"" SuggestedHeight=""1""
                  Sprite=""BlankWhiteSquare_9"" Color=""#C9A24AFF"" MarginTop=""8"" MarginBottom=""8"" />
          <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                      SuggestedHeight=""32"" Brush.FontSize=""20"" Brush.FontColor=""#F4E1C1FF""
                      Brush.TextHorizontalAlignment=""Center"" Text=""@CasualtiesText"" />
          <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                      SuggestedHeight=""32"" Brush.FontSize=""20"" Brush.FontColor=""#F4E1C1FF""
                      Brush.TextHorizontalAlignment=""Center"" Text=""@PrisonersText"" />
          <Widget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed"" SuggestedHeight=""1""
                  Sprite=""BlankWhiteSquare_9"" Color=""#C9A24AFF"" MarginTop=""12"" MarginBottom=""12"" />
          <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                      SuggestedHeight=""48"" Brush.FontSize=""36""
                      Brush.FontColor=""@WarScoreColor""
                      Brush.TextHorizontalAlignment=""Center"" Text=""@WarScoreText"" />
          <ButtonWidget DoNotPassEventsToChildren=""true"" UpdateChildrenStates=""true""
                        WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                        SuggestedWidth=""180"" SuggestedHeight=""42""
                        HorizontalAlignment=""Center"" MarginTop=""24""
                        Brush=""ButtonBrush1"" Command.Click=""ExecuteClose"">
            <Children>
              <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                          Brush.TextHorizontalAlignment=""Center"" Brush.TextVerticalAlignment=""Center""
                          Text=""Fechar"" />
            </Children>
          </ButtonWidget>
        </Children>
      </ListPanel>
    </Children>
  </Widget>
  <!-- Siege Report window (mostrado 1h apos fim do Cerco) -->
  <Widget DataSource=""{CbSiegeReport}"" IsVisible=""@IsVisible"" IsEnabled=""@IsVisible""
          DoNotAcceptEvents=""true""
          WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
          SuggestedWidth=""720"" SuggestedHeight=""480""
          HorizontalAlignment=""Center"" VerticalAlignment=""Center"">
    <Children>
      <Widget DoNotAcceptEvents=""true""
              WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
              Sprite=""BlankWhiteSquare_9"" Color=""#18130EF5"" />
      <ListPanel DoNotAcceptEvents=""true""
                 WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                 MarginLeft=""32"" MarginRight=""32"" MarginTop=""28"" MarginBottom=""28""
                 StackLayout.LayoutMethod=""VerticalTopToBottom"">
        <Children>
          <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                      SuggestedHeight=""42"" Brush.FontSize=""32"" Brush.FontColor=""#D4AF37FF""
                      Brush.TextHorizontalAlignment=""Center"" Text=""@Title"" />
          <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                      SuggestedHeight=""60"" Brush.FontSize=""20"" Brush.FontColor=""#F4E1C1FF""
                      Brush.TextHorizontalAlignment=""Center"" Text=""@Description"" />
          <Widget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed"" SuggestedHeight=""1""
                  Sprite=""BlankWhiteSquare_9"" Color=""#C9A24AFF"" MarginTop=""8"" MarginBottom=""8"" />
          <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                      SuggestedHeight=""32"" Brush.FontSize=""18"" Brush.FontColor=""#F4E1C1FF""
                      Brush.TextHorizontalAlignment=""Center"" Text=""@CasualtiesText"" />
          <Widget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed"" SuggestedHeight=""1""
                  Sprite=""BlankWhiteSquare_9"" Color=""#C9A24AFF"" MarginTop=""8"" MarginBottom=""8"" />
          <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                      SuggestedHeight=""48"" Brush.FontSize=""36""
                      Brush.FontColor=""@WarScoreColor""
                      Brush.TextHorizontalAlignment=""Center"" Text=""@WarScoreText"" />
          <ButtonWidget DoNotPassEventsToChildren=""true"" UpdateChildrenStates=""true""
                        WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                        SuggestedWidth=""180"" SuggestedHeight=""42""
                        HorizontalAlignment=""Center"" MarginTop=""24""
                        Brush=""ButtonBrush1"" Command.Click=""ExecuteClose"">
            <Children>
              <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                          Brush.TextHorizontalAlignment=""Center"" Brush.TextVerticalAlignment=""Center""
                          Text=""Fechar"" />
            </Children>
          </ButtonWidget>
        </Children>
      </ListPanel>
    </Children>
  </Widget>
  </Children>
</Widget>";
            }
        }

    }

        [ViewModelMixin]
    internal sealed class CasusBelliMapInfoMixin : BaseViewModelMixin<MapInfoVM>
    {
        private readonly MBBindingList<CasusBelliWarIconVM> _icons;
        private readonly CasusBelliWarWindowVM _window;
        private readonly CasusBelliBattleReportVM _battleReport;
        private readonly CasusBelliSiegeReportVM _siegeReport;

        private class QueuedBattleReport
        {
            public string SettlementName;
            public string HeroName;
            public string WinnerKingdomName;
            public int Wounded;
            public int Killed;
            public List<Hero> CapturedLords;
            public double WarScoreChange;
            public bool PlayerWon;
            public bool PlayerLedWinningArmy;
            public string EnemyLeaderName;
            public double ShowAtTime; // campaign time em horas
        }
        private readonly Queue<QueuedBattleReport> _battleReportQueue = new Queue<QueuedBattleReport>();

        private class QueuedSiegeReport
        {
            public string SettlementName;
            public string WinnerKingdomName;
            public string LoserKingdomName;
            public string GovernorName;
            public int Wounded;
            public int Killed;
            public int WoundedDefender;
            public int KilledDefender;
            public double WarScoreChange;
            public bool PlayerWon;
            public bool PlayerInArmy;
            public double ShowAtTime; // campaign time em horas
        }
        private readonly Queue<QueuedSiegeReport> _siegeReportQueue = new Queue<QueuedSiegeReport>();

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

        public CasusBelliMapInfoMixin(MapInfoVM vm) : base(vm)
        {
            LogToFile("CasusBelliMapInfoMixin CONSTRUTOR");
            _icons = new MBBindingList<CasusBelliWarIconVM>();
            _window = new CasusBelliWarWindowVM(CloseWindow);
            _battleReport = new CasusBelliBattleReportVM(CloseBattleReport);
            _siegeReport = new CasusBelliSiegeReportVM(CloseSiegeReport);

            CasusBelliWarScoreBehavior.Changed += OnScoreChanged;
            CasusBelliWarScoreBehavior.WindowOpener = OpenWindow;
            CasusBelliWarScoreBehavior.BattleReportReady += OnBattleReportReady;
            CasusBelliWarScoreBehavior.SiegeReportReady += OnSiegeReportReady;
            CasusBelliWarScoreBehavior.UiLoaded = true;
            LogToFile("CasusBelliMapInfoMixin - BattleReportReady e SiegeReportReady subscritos");
            RefreshIcons();
        }

        [DataSourceProperty]
        public MBBindingList<CasusBelliWarIconVM> CbIcons
        {
            get { return _icons; }
        }

        [DataSourceProperty]
        public CasusBelliWarWindowVM CbWindow { get { return _window; } }

        [DataSourceProperty]
        public CasusBelliBattleReportVM CbBattleReport { get { return _battleReport; } }

        [DataSourceProperty]
        public CasusBelliSiegeReportVM CbSiegeReport { get { return _siegeReport; } }

        public override void OnRefresh()
        {
            base.OnRefresh();
            LogToFile("CasusBelli: CasusBelliMapInfoMixin OnRefresh");
            RefreshIcons();
            ProcessBattleReportQueue();
            ProcessSiegeReportQueue();
        }

        public override void OnFinalize()
        {
            CasusBelliWarScoreBehavior.Changed -= OnScoreChanged;
            CasusBelliWarScoreBehavior.BattleReportReady -= OnBattleReportReady;
            CasusBelliWarScoreBehavior.SiegeReportReady -= OnSiegeReportReady;

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
            RefreshIcons();

            if (_window.IsOpen)
            {
                _window.Refresh();
            }

            // Processa fila de relatorios de batalha (chamado via RaiseChanged no HourlyTickEvent e TickEvent)
            ProcessBattleReportQueue();
            ProcessSiegeReportQueue();
        }

        private void OnBattleReportReady(BattleReportData report)
        {
            LogToFile("CasusBelli: OnBattleReportReady CHAMADO settlement=" +
                (report?.SettlementName ?? "null") + " hero=" + (report?.HeroName ?? "null") +
                " wounded=" + report?.Wounded + " killed=" + report?.Killed +
                " capturedLords=" + report?.CapturedLords?.Count +
                " playerWon=" + report?.PlayerWon);

            if (report == null || !CasusBelliWarScoreBehavior.UiEnabled)
            {
                LogToFile("CasusBelli: OnBattleReportReady SAINDO cedo (report null ou UiEnabled=false)");
                return;
            }

            _battleReportQueue.Enqueue(new QueuedBattleReport
            {
                SettlementName = report.SettlementName,
                HeroName = report.HeroName,
                WinnerKingdomName = report.WinnerKingdomName,
                Wounded = report.Wounded,
                Killed = report.Killed,
                CapturedLords = report.CapturedLords ?? new List<Hero>(),
                WarScoreChange = report.WarScoreChange,
                PlayerWon = report.PlayerWon,
                PlayerLedWinningArmy = report.PlayerInArmy,
                EnemyLeaderName = report.EnemyLeaderName,
                ShowAtTime = CampaignTime.Now.ToHours + 1.0
            });
            LogToFile("CasusBelli: Relatório enfileirado. Fila tamanho=" + _battleReportQueue.Count);
        }

        private void ProcessBattleReportQueue()
        {
            if (_battleReportQueue.Count == 0) return;

            double now = CampaignTime.Now.ToHours;

            LogToFile("CasusBelli: ProcessBattleReportQueue INI now=" + now + " fila=" + _battleReportQueue.Count);

            while (_battleReportQueue.Count > 0)
            {
                var report = _battleReportQueue.Peek();

                if (now >= report.ShowAtTime)
                {
                    LogToFile("CasusBelli: ProcessBattleReportQueue - EXIBINDO relatório settlement=" + report.SettlementName);
                    _battleReportQueue.Dequeue();
                    ShowBattleReport(report);
                }
                else
                {
                    LogToFile("CasusBelli: ProcessBattleReportQueue - AGUARDANDO relatório settlement=" + report.SettlementName + " showAt=" + report.ShowAtTime + " agora=" + now);
                    break; // fila ordenada por tempo
                }
            }
        }

        private void ShowBattleReport(QueuedBattleReport report)
        {
            if (_battleReport.IsVisible)
            {
                LogToFile("CasusBelli: ShowBattleReport - Janela JÁ VISÍVEL, ignorando");
                return;
            }

            try
            {
                LogToFile("CasusBelli: ShowBattleReport - Abrindo janela para settlement=" + report.SettlementName);
                _battleReport.SetData(
                    report.SettlementName,
                    report.HeroName,
                    report.WinnerKingdomName,
                    report.Wounded,
                    report.Killed,
                    report.CapturedLords,
                    report.WarScoreChange,
                    report.PlayerWon,
                    report.PlayerLedWinningArmy,
                    report.EnemyLeaderName);

                _battleReport.IsVisible = true;
                Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
            }
            catch (Exception ex)
            {
                WarUiHelper.Message("CB: falha ao abrir relatorio de batalha: " + ex.GetType().Name + ": " + ex.Message);
                _battleReport.IsVisible = false;
            }
        }

        private void CloseBattleReport()
        {
            LogToFile("CasusBelli: CloseBattleReport - Fechando janela");
            _battleReport.IsVisible = false;
        }

        private void OnSiegeReportReady(SiegeReportData report)
        {
            LogToFile("CasusBelli: OnSiegeReportReady CHAMADO settlement=" +
                (report?.SettlementName ?? "null") + " winner=" + (report?.WinnerKingdomName ?? "null") +
                " playerWon=" + report?.PlayerWon);

            if (report == null || !CasusBelliWarScoreBehavior.UiEnabled)
            {
                LogToFile("CasusBelli: OnSiegeReportReady SAINDO cedo (report null ou UiEnabled=false)");
                return;
            }

            _siegeReportQueue.Enqueue(new QueuedSiegeReport
            {
                SettlementName = report.SettlementName,
                WinnerKingdomName = report.WinnerKingdomName,
                LoserKingdomName = report.LoserKingdomName,
                GovernorName = report.GovernorName,
                Wounded = report.Wounded,
                Killed = report.Killed,
                WoundedDefender = report.WoundedDefender,
                KilledDefender = report.KilledDefender,
                WarScoreChange = report.WarScoreChange,
                PlayerWon = report.PlayerWon,
                PlayerInArmy = report.PlayerInArmy,
                ShowAtTime = CampaignTime.Now.ToHours + 1.0
            });
            LogToFile("CasusBelli: Relatório de cerco enfileirado. Fila tamanho=" + _siegeReportQueue.Count);
        }

        private void ProcessSiegeReportQueue()
        {
            if (_siegeReportQueue.Count == 0) return;

            double now = CampaignTime.Now.ToHours;

            LogToFile("CasusBelli: ProcessSiegeReportQueue INI now=" + now + " fila=" + _siegeReportQueue.Count);

            while (_siegeReportQueue.Count > 0)
            {
                var report = _siegeReportQueue.Peek();

                if (now >= report.ShowAtTime)
                {
                    LogToFile("CasusBelli: ProcessSiegeReportQueue - EXIBINDO relatório settlement=" + report.SettlementName);
                    _siegeReportQueue.Dequeue();
                    ShowSiegeReport(report);
                }
                else
                {
                    LogToFile("CasusBelli: ProcessSiegeReportQueue - AGUARDANDO relatório settlement=" + report.SettlementName + " showAt=" + report.ShowAtTime + " agora=" + now);
                    break; // fila ordenada por tempo
                }
            }
        }

        private void ShowSiegeReport(QueuedSiegeReport report)
        {
            if (_siegeReport.IsVisible)
            {
                LogToFile("CasusBelli: ShowSiegeReport - Janela JÁ VISÍVEL, ignorando");
                return;
            }

            try
            {
                LogToFile("CasusBelli: ShowSiegeReport - Abrindo janela para settlement=" + report.SettlementName);
                _siegeReport.SetData(
                    report.SettlementName,
                    report.GovernorName,
                    report.Wounded,
                    report.Killed,
                    report.WoundedDefender,
                    report.KilledDefender,
                    report.WinnerKingdomName,
                    report.LoserKingdomName,
                    report.WarScoreChange,
                    report.PlayerWon,
                    report.PlayerInArmy);

                _siegeReport.IsVisible = true;
                Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
            }
            catch (Exception ex)
            {
                WarUiHelper.Message("CB: falha ao abrir relatorio de cerco: " + ex.GetType().Name + ": " + ex.Message);
                _siegeReport.IsVisible = false;
            }
        }

        private void CloseSiegeReport()
        {
            LogToFile("CasusBelli: CloseSiegeReport - Fechando janela");
            _siegeReport.IsVisible = false;
        }

        private void OpenWindow()
        {
            CasusBelliWarScoreBehavior warScore = WarUiHelper.GetWarScore();
            Kingdom kingdom = WarUiHelper.GetPlayerKingdom();

            if (warScore == null || kingdom == null ||
                warScore.GetSelectedWar(kingdom) == null)
            {
                WarUiHelper.Message("CB: seu reino não está em guerra.");
                return;
            }

            _window.IsOpen = true;
            _window.Refresh();
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
        }

        private void CloseWindow()
        {
            _window.IsOpen = false;
            RefreshIcons();
        }

        private void RefreshIcons()
        {
            _icons.Clear();

            CasusBelliWarScoreBehavior warScore = WarUiHelper.GetWarScore();
            Kingdom kingdom = WarUiHelper.GetPlayerKingdom();

            if (warScore == null || kingdom == null)
            {
                return;
            }

            foreach (WarRecord war in warScore.GetActiveWarsOf(kingdom))
            {
                CasusBelliWarIconVM icon = new CasusBelliWarIconVM(
                    OpenWindow,
                    null,
                    war.WarId);
                icon.Refresh();
                _icons.Add(icon);
            }
        }
    }
}
#endif
 