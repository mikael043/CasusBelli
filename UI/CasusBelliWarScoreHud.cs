// INTERFACE DE WARSCORE ESTILO CK2: icone clicavel no mapa
// (escudo dos dois reinos + "-38%") e janela de negociacao.
//
// Este arquivo tem os ViewModels (dados e comandos) usados pelas DUAS
// formas de colocar a interface na tela. Use UMA delas:
//
//  CB_UIEX (recomendado): usa o UIExtenderEx, como o mod Diplomacy. Os
//     widgets entram dentro da barra do mapa do proprio jogo, entao clique
//     e hover funcionam. Veja CasusBelliUiExtender.cs.
//
//  CB_GAUNTLET (antigo): camadas Gauntlet proprias. Nao recebeu cliques
//     na versao 1.4.8. Mantido so como referencia.
//
// Nao defina os dois simbolos ao mesmo tempo.
#if CB_GAUNTLET || CB_UIEX
using System;
using System.Collections.Generic;
#if CB_GAUNTLET
using SandBox.View.Map;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.ScreenSystem;
#endif
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;

namespace CasusBelli
{
    internal static class WarUiHelper
    {
        public static CasusBelliWarScoreBehavior GetWarScore()
        {
            return Campaign.Current != null
                ? Campaign.Current.GetCampaignBehavior<
                    CasusBelliWarScoreBehavior>()
                : null;
        }

        public static Kingdom GetPlayerKingdom()
        {
            return Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
        }

        // Cor do reino no formato "#RRGGBBAA" do Gauntlet.
        public static string Hex(Kingdom kingdom)
        {
            uint color = kingdom != null ? kingdom.Color : 0x808080u;

            return "#" + (color & 0x00FFFFFFu).ToString("X6") + "FF";
        }

        public static string ScoreColor(double score)
        {
            if (score > 5.0)
            {
                return "#5DBB63FF";
            }

            if (score < -5.0)
            {
                return "#D9534FFF";
            }

            return "#D4AF37FF";
        }

        public static void Message(string text)
        {
            InformationManager.DisplayMessage(new InformationMessage(text));
        }
    }

    // ---------------------------------------------------------------
    // ICONE NO MAPA
    // ---------------------------------------------------------------
    public class CasusBelliWarIconVM : ViewModel
    {
        private readonly Action _open;
        private readonly Action<bool> _hover;
        private readonly string _warId;
        private bool _isVisible;
        private string _percentText = "";
        private string _percentColor = "#D4AF37FF";
        private string _shieldColor = "#808080FF";
        private string _leftColor = "#808080FF";

        public CasusBelliWarIconVM(Action open, Action<bool> hover)
            : this(open, hover, "")
        {
        }

        public CasusBelliWarIconVM(
            Action open,
            Action<bool> hover,
            string warId)
        {
            _open = open;
            _hover = hover;
            _warId = warId ?? "";
        }

        private void Set<T>(ref T field, T value, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                OnPropertyChanged(name);
            }
        }

        [DataSourceProperty]
        public bool IsVisible { get { return _isVisible; } set { Set(ref _isVisible, value, "IsVisible"); } }
        [DataSourceProperty]
        public string PercentText { get { return _percentText; } set { Set(ref _percentText, value, "PercentText"); } }
        [DataSourceProperty]
        public string PercentColor { get { return _percentColor; } set { Set(ref _percentColor, value, "PercentColor"); } }
        [DataSourceProperty]
        public string ShieldColor { get { return _shieldColor; } set { Set(ref _shieldColor, value, "ShieldColor"); } }
        [DataSourceProperty]
        public string LeftColor { get { return _leftColor; } set { Set(ref _leftColor, value, "LeftColor"); } }

        public void ExecuteOpen()
        {
            CasusBelliWarScoreBehavior ws = WarUiHelper.GetWarScore();

            if (ws != null && !string.IsNullOrEmpty(_warId))
            {
                ws.SelectedWarId = _warId;
            }

            if (_open != null)
            {
                _open();
            }
        }

        // O mouse passa a "pertencer" ao icone so enquanto esta em cima dele.
        public void ExecuteHoverBegin()
        {
            if (_hover != null)
            {
                _hover(true);
            }
        }

        public void ExecuteHoverEnd()
        {
            if (_hover != null)
            {
                _hover(false);
            }
        }

        public void Refresh()
        {
            CasusBelliWarScoreBehavior ws = WarUiHelper.GetWarScore();
            Kingdom kingdom = WarUiHelper.GetPlayerKingdom();
            WarRecord war = null;

            if (ws != null && kingdom != null)
            {
                if (string.IsNullOrEmpty(_warId))
                {
                    war = ws.GetSelectedWar(kingdom);
                }
                else
                {
                    war = ws.GetActiveWarsOf(kingdom).Find(
                        item => item.WarId == _warId);
                }
            }

            if (war == null)
            {
                // Icone some: garante que o mapa volta a receber o mouse.
                if (IsVisible && _hover != null)
                {
                    _hover(false);
                }

                IsVisible = false;
                return;
            }

            double score = ws.ScoreFor(war, kingdom.StringId);

            PercentText = CasusBelliWarScoreBehavior.FormatPercent(score);
            PercentColor = WarUiHelper.ScoreColor(score);
            LeftColor = WarUiHelper.Hex(kingdom);
            ShieldColor = WarUiHelper.Hex(ws.GetEnemyKingdom(war, kingdom));
            IsVisible = true;
        }
    }

    // ---------------------------------------------------------------
    // JANELA DE NEGOCIACAO
    // ---------------------------------------------------------------
    public class CasusBelliOfferVM : ViewModel
    {
        private readonly Action<NegotiationOfferType> _execute;
        private readonly NegotiationOfferType _type;
        private readonly string _title;
        private readonly string _detail;
        private readonly bool _isEnabled;

        public CasusBelliOfferVM(
            NegotiationOffer offer,
            Action<NegotiationOfferType> execute)
        {
            _execute = execute;
            _type = offer.Type;
            _title = offer.Title;
            _detail = offer.IsEnabled ? offer.Detail : offer.DisabledReason;
            _isEnabled = offer.IsEnabled;
        }

        [DataSourceProperty]
        public string Title { get { return _title; } }

        [DataSourceProperty]
        public string Detail { get { return _detail; } }

        [DataSourceProperty]
        public bool IsEnabled { get { return _isEnabled; } }

        public void ExecuteClick()
        {
            _execute(_type);
        }
    }

    public class CasusBelliWarWindowVM : ViewModel
    {
        private readonly Action _close;

        public CasusBelliWarWindowVM(Action close)
        {
            _close = close;
            _offers = new MBBindingList<CasusBelliOfferVM>();
        }

        [DataSourceProperty]
        public MBBindingList<CasusBelliOfferVM> Offers { get { return _offers; } }
        private MBBindingList<CasusBelliOfferVM> _offers;

        private string _title = "";
        private string _playerName = "";
        private string _enemyName = "";
        private string _playerColor = "#808080FF";
        private string _enemyColor = "#808080FF";
        private string _scoreText = "";
        private string _scoreColor = "#D4AF37FF";
        private int _negFillWidth;
        private int _posFillWidth;
        private string _battlesText = "";
        private string _siegesText = "";
        private string _objectiveText = "";
        private string _prisonersText = "";
        private string _statusText = "";
        private string _demandsText = "";
        private string _enforceHint = "";
        private string _whiteHint = "";
        private string _surrenderHint = "";
        private bool _enforceEnabled;
        private bool _whiteEnabled;
        private bool _surrenderEnabled;
        private bool _hasMultipleWars;
        private bool _hasOffers;
        private bool _isOpen;

        private void Set<T>(ref T field, T value, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                OnPropertyChanged(name);
            }
        }

        [DataSourceProperty]
        public string Title { get { return _title; } set { Set(ref _title, value, "Title"); } }
        [DataSourceProperty]
        public string PlayerName { get { return _playerName; } set { Set(ref _playerName, value, "PlayerName"); } }
        [DataSourceProperty]
        public string EnemyName { get { return _enemyName; } set { Set(ref _enemyName, value, "EnemyName"); } }
        [DataSourceProperty]
        public string PlayerColor { get { return _playerColor; } set { Set(ref _playerColor, value, "PlayerColor"); } }
        [DataSourceProperty]
        public string EnemyColor { get { return _enemyColor; } set { Set(ref _enemyColor, value, "EnemyColor"); } }
        [DataSourceProperty]
        public string ScoreText { get { return _scoreText; } set { Set(ref _scoreText, value, "ScoreText"); } }
        [DataSourceProperty]
        public string ScoreColor { get { return _scoreColor; } set { Set(ref _scoreColor, value, "ScoreColor"); } }
        [DataSourceProperty]
        public int NegFillWidth { get { return _negFillWidth; } set { Set(ref _negFillWidth, value, "NegFillWidth"); } }
        [DataSourceProperty]
        public int PosFillWidth { get { return _posFillWidth; } set { Set(ref _posFillWidth, value, "PosFillWidth"); } }
        [DataSourceProperty]
        public string BattlesText { get { return _battlesText; } set { Set(ref _battlesText, value, "BattlesText"); } }
        [DataSourceProperty]
        public string SiegesText { get { return _siegesText; } set { Set(ref _siegesText, value, "SiegesText"); } }
        [DataSourceProperty]
        public string ObjectiveText { get { return _objectiveText; } set { Set(ref _objectiveText, value, "ObjectiveText"); } }
        [DataSourceProperty]
        public string PrisonersText { get { return _prisonersText; } set { Set(ref _prisonersText, value, "PrisonersText"); } }
        [DataSourceProperty]
        public string StatusText { get { return _statusText; } set { Set(ref _statusText, value, "StatusText"); } }
        [DataSourceProperty]
        public string DemandsText { get { return _demandsText; } set { Set(ref _demandsText, value, "DemandsText"); } }
        [DataSourceProperty]
        public string EnforceHint { get { return _enforceHint; } set { Set(ref _enforceHint, value, "EnforceHint"); } }
        [DataSourceProperty]
        public string WhiteHint { get { return _whiteHint; } set { Set(ref _whiteHint, value, "WhiteHint"); } }
        [DataSourceProperty]
        public string SurrenderHint { get { return _surrenderHint; } set { Set(ref _surrenderHint, value, "SurrenderHint"); } }
        [DataSourceProperty]
        public bool EnforceEnabled { get { return _enforceEnabled; } set { Set(ref _enforceEnabled, value, "EnforceEnabled"); } }
        [DataSourceProperty]
        public bool WhiteEnabled { get { return _whiteEnabled; } set { Set(ref _whiteEnabled, value, "WhiteEnabled"); } }
        [DataSourceProperty]
        public bool SurrenderEnabled { get { return _surrenderEnabled; } set { Set(ref _surrenderEnabled, value, "SurrenderEnabled"); } }
        [DataSourceProperty]
        public bool HasMultipleWars { get { return _hasMultipleWars; } set { Set(ref _hasMultipleWars, value, "HasMultipleWars"); } }
        [DataSourceProperty]
        public bool IsOpen { get { return _isOpen; } set { Set(ref _isOpen, value, "IsOpen"); } }
        [DataSourceProperty]
        public bool HasOffers { get { return _hasOffers; } set { Set(ref _hasOffers, value, "HasOffers"); } }

        private static string Line(string label, double value)
        {
            return label + ": " + CasusBelliWarScoreBehavior.FormatPercent(value);
        }

        public void Refresh()
        {
            CasusBelliWarScoreBehavior ws = WarUiHelper.GetWarScore();
            Kingdom kingdom = WarUiHelper.GetPlayerKingdom();
            WarRecord war = ws != null && kingdom != null
                ? ws.GetSelectedWar(kingdom)
                : null;

            if (war == null)
            {
                _close();
                return;
            }

            Kingdom enemy = ws.GetEnemyKingdom(war, kingdom);
            WarScoreBreakdown b = ws.GetBreakdown(war);
            bool isAttacker = kingdom.StringId == war.AttackerKingdomId;
            double sign = isAttacker ? 1.0 : -1.0;
            double total = b.Total * sign;

            Title = "Guerra contra " + (enemy != null ? enemy.Name.ToString() : "?");
            PlayerName = kingdom.Name.ToString();
            EnemyName = enemy != null ? enemy.Name.ToString() : "?";
            PlayerColor = WarUiHelper.Hex(kingdom);
            EnemyColor = WarUiHelper.Hex(enemy);

            ScoreText = CasusBelliWarScoreBehavior.FormatPercent(total);
            ScoreColor = WarUiHelper.ScoreColor(total);

            // metade da barra = 280 px
            NegFillWidth = (int)(Math.Max(0.0, -total) / 100.0 * 280.0);
            PosFillWidth = (int)(Math.Max(0.0, total) / 100.0 * 280.0);

            BattlesText = Line("Batalhas", b.Battles * sign);
            SiegesText = Line("Cercos", b.Sieges * sign) + "  " + Line("Saques", b.Raids * sign);
            ObjectiveText = war.CasusBelliType == CasusBelliCampaignBehavior.ClaimWarType
                ? Line("Objetivo do casus belli", b.Objective * sign)
                : "Objetivo do casus belli: nenhum";
            PrisonersText = Line("Prisioneiros", b.Prisoners * sign);

            bool youHoldKing = isAttacker
                ? b.AttackerHoldsEnemyKing
                : b.DefenderHoldsEnemyKing;
            bool theyHoldKing = isAttacker
                ? b.DefenderHoldsEnemyKing
                : b.AttackerHoldsEnemyKing;

            StatusText = "Duracao da guerra: " +
                ws.GetWarAgeDays(war).ToString("0") + " dias" +
                (youHoldKing ? "\nVoce mantem o soberano inimigo cativo."
                    : theyHoldKing ? "\nO inimigo mantem o seu soberano cativo."
                    : "");

            if (war.ArmisticeEnds >= 0) StatusText += $"\nTrégua em vigor: faltam {Math.Max(0, Math.Ceiling(war.ArmisticeEnds - CampaignTime.Now.ToDays))} dias.";
            bool isRuler = kingdom.Leader != null &&
                kingdom.Leader == Hero.MainHero;
            string reason;

            bool canEnforce = ws.CanEnforce(war, kingdom, out reason);
            EnforceHint = canEnforce ? "Disponivel" : reason;
            EnforceEnabled = isRuler && canEnforce;

            bool canWhite = ws.CanWhitePeace(war, kingdom, out reason);
            WhiteHint = canWhite ? "Disponivel" : reason;
            WhiteEnabled = isRuler && canWhite;

            bool canSurrender = ws.CanSurrender(war, kingdom, out reason);
            SurrenderHint = canSurrender ? "Disponivel" : reason;
            SurrenderEnabled = isRuler && canSurrender;

            if (!isRuler)
            {
                EnforceHint = "So o governante do reino negocia a paz.";
                WhiteHint = EnforceHint;
                SurrenderHint = EnforceHint;
            }

            DemandsText = "Se suas exigências forem aceitas: " + ws.PreviewDemands(war, kingdom) +
                "\nSe voce se render: " +
                (enemy != null ? ws.PreviewDemands(war, enemy) : "");

            HasMultipleWars = ws.GetActiveWarsOf(kingdom).Count > 1;
            RefreshOffers();
        }

        private void RefreshOffers()
        {
            Offers.Clear();

            CasusBelliKingBehavior kings = Campaign.Current != null
                ? Campaign.Current.GetCampaignBehavior<CasusBelliKingBehavior>()
                : null;
            KingCaptureRecord record;

            if (kings != null && kings.TryGetPlayerNegotiation(out record))
            {
                foreach (NegotiationOffer offer in kings.GetOffers(record))
                {
                    Offers.Add(new CasusBelliOfferVM(offer, OnOffer));
                }
            }

            HasOffers = Offers.Count > 0;
        }

        private void OnOffer(NegotiationOfferType type)
        {
            CasusBelliKingBehavior kings = Campaign.Current != null
                ? Campaign.Current.GetCampaignBehavior<CasusBelliKingBehavior>()
                : null;
            KingCaptureRecord record;

            if (kings == null || !kings.TryGetPlayerNegotiation(out record))
            {
                Refresh();
                return;
            }

            kings.PlayerOffer(record, type, result =>
            {
                WarUiHelper.Message("CB: " + result);
                Refresh();
            });
        }

        public void ExecuteEnforce()
        {
            Act(0);
        }

        public void ExecuteWhitePeace()
        {
            Act(1);
        }

        public void ExecuteSurrender()
        {
            Act(2);
        }

        private void Act(int kind)
        {
            CasusBelliWarScoreBehavior ws = WarUiHelper.GetWarScore();
            Kingdom kingdom = WarUiHelper.GetPlayerKingdom();
            WarRecord war = ws != null && kingdom != null
                ? ws.GetSelectedWar(kingdom)
                : null;

            if (war == null)
            {
                _close();
                return;
            }

            var enemy = ws.GetEnemyKingdom(war, kingdom);
            if (kingdom.Leader != Hero.MainHero || enemy == null) return;
            var terms = kind == 1 ? "Os dois reinos encerram a guerra com as posses atuais, sem indenização."
                : ws.PreviewDemands(war, kind == 0 ? kingdom : enemy);
            InformationManager.ShowInquiry(new InquiryData(
                kind == 0 ? "Apresentar exigências" : kind == 1 ? "Propor paz branca" : "Oferecer rendição",
                terms, true, true, "Confirmar", "Voltar", () =>
                {
                    if (kingdom.Leader == Hero.MainHero && WarUiHelper.GetPlayerKingdom() == kingdom)
                        ConfirmPeace(ws, war, kingdom, kind);
                }, () => { }));
        }

        private void ConfirmPeace(CasusBelliWarScoreBehavior ws, WarRecord war, Kingdom kingdom, int kind)
        {
            string result;
            if (kind == 0)
            {
                ws.TryEnforceDemands(war, kingdom, out result);
            }
            else if (kind == 1)
            {
                ws.TryWhitePeace(war, kingdom, out result);
            }
            else
            {
                ws.TrySurrender(war, kingdom, out result);
            }

            WarUiHelper.Message("CB: " + result);
            Refresh();
        }

        public void ExecuteNextWar()
        {
            CasusBelliWarScoreBehavior ws = WarUiHelper.GetWarScore();

            if (ws != null)
            {
                ws.CycleSelectedWar(WarUiHelper.GetPlayerKingdom());
            }

            Refresh();
        }

        public void ExecuteClose()
        {
            _close();
        }
    }

#if CB_GAUNTLET
    // ---------------------------------------------------------------
    // CAMADAS NA TELA DO MAPA
    // ---------------------------------------------------------------
    public class CasusBelliWarScoreHudBehavior : CampaignBehaviorBase
    {
        private MapScreen _screen;
        private MapScreen _failedScreen;
        private bool _hovering;

        // Posicao e tamanho do icone em unidades do prefab (CBWarIcon.xml).
        // Usados so no clique de reserva (veja OnTick).
        private const float IconLeft = 14f;
        private const float IconTop = 130f;
        private const float IconWidth = 96f;
        private const float IconHeight = 128f;

        private GauntletLayer _iconLayer;
        private object _iconMovie;
        private CasusBelliWarIconVM _iconVm;

        private GauntletLayer _windowLayer;
        private object _windowMovie;
        private CasusBelliWarWindowVM _windowVm;

        // ----- Battle Report (mostrado 1h apos fim da missao) -----
        private GauntletLayer _battleReportLayer;
        private object _battleReportMovie;
        private CasusBelliBattleReportVM _battleReportVm;

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

        // ----- Siege Report (mostrado 1h apos fim do cerco) -----
        private GauntletLayer _siegeReportLayer;
        private object _siegeReportMovie;
        private CasusBelliSiegeReportVM _siegeReportVm;

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

        public override void RegisterEvents()
        {
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(
                this,
                OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnTick(float dt)
        {
            MapScreen current = MapScreen.Instance;

            if (current != null && current != _screen && current != _failedScreen)
            {
                Detach();
                Attach(current);
            }

            // CLIQUE DE RESERVA: le o mouse direto, sem depender da entrada
            // da camada Gauntlet. Abre a janela quando o botao esquerdo e
            // solto sobre o icone (por hover ou pela posicao do cursor).
            if (_windowLayer == null &&
                _iconVm != null &&
                _iconVm.IsVisible &&
                TaleWorlds.InputSystem.Input.IsKeyReleased(
                    InputKey.LeftMouseButton) &&
                (_hovering || IsMouseOverIcon()))
            {
                OpenWindow();
            }

            // ESC fecha a janela de negociacao.
            if (_windowLayer != null &&
                (_windowLayer.Input.IsKeyReleased(InputKey.Escape) ||
                 TaleWorlds.InputSystem.Input.IsKeyReleased(InputKey.Escape)))
            {
                CloseWindow();
            }

            // Processa fila de relatorios de batalha (mostra 1h depois)
            ProcessBattleReportQueue();

            // Processa fila de relatorios de cerco (mostra 1h depois)
            ProcessSiegeReportQueue();
        }

        private static bool IsMouseOverIcon()
        {
            try
            {
                // Escala da UI do Gauntlet na resolucao automatica.
                float scale = Math.Min(
                    TaleWorlds.Engine.Screen.RealScreenResolutionWidth / 1920f,
                    TaleWorlds.Engine.Screen.RealScreenResolutionHeight / 1080f);

                Vec2 mouse = TaleWorlds.InputSystem.Input.MousePositionPixel;

                float left = IconLeft * scale;
                float top = IconTop * scale;

                return mouse.x >= left &&
                    mouse.x <= left + IconWidth * scale &&
                    mouse.y >= top &&
                    mouse.y <= top + IconHeight * scale;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void OnHourlyTick()
        {
            OnScoreChanged();
        }

        private void OnScoreChanged()
        {
            if (_iconVm != null)
            {
                _iconVm.Refresh();
            }

            if (_windowVm != null)
            {
                _windowVm.Refresh();
            }

            if (_battleReportVm != null)
            {
                _battleReportVm.Refresh();
            }
        }

        // Adiciona relatorio a fila para ser mostrado 1h depois
        public void QueueBattleReport(
            string settlementName,
            string heroName,
            string winnerKingdomName,
            int wounded,
            int killed,
            List<Hero> capturedLords,
            double warScoreChange,
            bool playerWon,
            bool playerLedWinningArmy,
            string enemyLeaderName)
        {
            double showAt = CampaignTime.Now.ToHours + 1.0; // 1 hora depois

            _battleReportQueue.Enqueue(new QueuedBattleReport
            {
                SettlementName = settlementName,
                HeroName = heroName,
                WinnerKingdomName = winnerKingdomName,
                Wounded = wounded,
                Killed = killed,
                CapturedLords = capturedLords ?? new List<Hero>(),
                WarScoreChange = warScoreChange,
                PlayerWon = playerWon,
                PlayerLedWinningArmy = playerLedWinningArmy,
                EnemyLeaderName = enemyLeaderName,
                ShowAtTime = showAt
            });
        }

        private void OnBattleReportReady(CasusBelliWarScoreBehavior.BattleReportData report)
        {
            Debug.Print("CasusBelli: OnBattleReportReady CHAMADO settlement=" +
                (report?.SettlementName ?? "null") + " hero=" + (report?.HeroName ?? "null") +
                " wounded=" + report?.Wounded + " killed=" + report?.Killed +
                " capturedLords=" + report?.CapturedLords?.Count +
                " playerWon=" + report?.PlayerWon);

            if (report == null || !UiEnabled)
            {
                Debug.Print("CasusBelli: OnBattleReportReady SAINDO cedo (report null ou UiEnabled=false)");
                return;
            }

            QueueBattleReport(
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
            Debug.Print("CasusBelli: Relatório enfileirado. Fila tamanho=" + _battleReportQueue.Count);
        }

        private void ProcessBattleReportQueue()
        {
            if (_battleReportQueue.Count == 0) return;

            double now = CampaignTime.Now.ToHours;

            Debug.Print("CasusBelli: ProcessBattleReportQueue INI now=" + now + " fila=" + _battleReportQueue.Count);

            while (_battleReportQueue.Count > 0)
            {
                var report = _battleReportQueue.Peek();

                if (now >= report.ShowAtTime)
                {
                    Debug.Print("CasusBelli: ProcessBattleReportQueue - EXIBINDO relatório settlement=" + report.SettlementName);
                    _battleReportQueue.Dequeue();
                    ShowBattleReport(report);
                }
                else
                {
                    Debug.Print("CasusBelli: ProcessBattleReportQueue - AGUARDANDO relatório settlement=" + report.SettlementName + " showAt=" + report.ShowAtTime + " agora=" + now);
                    break; // fila ordenada por tempo
                }
            }
        }

        private void ShowBattleReport(QueuedBattleReport report)
        {
            if (_screen == null || _battleReportLayer != null)
            {
                Debug.Print("CasusBelli: ShowBattleReport - SAINDO cedo screen=" + (_screen != null) + " layer=" + (_battleReportLayer != null));
                return;
            }

            try
            {
                Debug.Print("CasusBelli: ShowBattleReport - Abrindo janela para settlement=" + report.SettlementName);
                _battleReportVm = new CasusBelliBattleReportVM(CloseBattleReport);
                _battleReportVm.SetData(
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

                _battleReportLayer = new GauntletLayer("BattleReportLayer", 600, false);
                _battleReportMovie = _battleReportLayer.LoadMovie("CBBattleReport", _battleReportVm);

                _screen.AddLayer(_battleReportLayer);

                // Janela modal: bloqueia o mapa por baixo enquanto aberta.
                _battleReportLayer.InputRestrictions.SetInputRestrictions(
                    true,
                    InputUsageMask.All);
                _battleReportLayer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(_battleReportLayer);

                Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
                Debug.Print("CasusBelli: ShowBattleReport - Janela aberta com sucesso");
            }
            catch (Exception ex)
            {
                Debug.Print("CasusBelli: ShowBattleReport EXCEPTION: " + ex.GetType().Name + ": " + ex.Message);
                WarUiHelper.Message("CB: falha ao abrir relatorio de batalha: " + ex.GetType().Name + ": " + ex.Message);
                CloseBattleReport();
            }
        }

        private void CloseBattleReport()
        {
            Debug.Print("CasusBelli: CloseBattleReport - Fechando janela");
            if (_battleReportLayer == null)
            {
                return;
            }

            try
            {
                _battleReportLayer.InputRestrictions.ResetInputRestrictions();
                _battleReportLayer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(_battleReportLayer);

                if (_battleReportMovie != null)
                {
                    _battleReportLayer.ReleaseMovie((GauntletMovieIdentifier)_battleReportMovie);
                }

                if (_screen != null)
                {
                    _screen.RemoveLayer(_battleReportLayer);
                }
            }
            catch (Exception)
            {
            }

            _battleReportMovie = null;
            _battleReportLayer = null;
            _battleReportVm = null;
        }

        // ---- Siege Report methods ----

        private void OnSiegeReportReady(CasusBelliWarScoreBehavior.SiegeReportData report)
        {
            Debug.Print("CasusBelli: OnSiegeReportReady CHAMADO settlement=" +
                (report?.SettlementName ?? "null") + " winner=" + (report?.WinnerKingdomName ?? "null") +
                " playerWon=" + report?.PlayerWon);

            if (report == null || !UiEnabled)
            {
                Debug.Print("CasusBelli: OnSiegeReportReady SAINDO cedo (report null ou UiEnabled=false)");
                return;
            }

            QueueSiegeReport(
                report.SettlementName,
                report.WinnerKingdomName,
                report.LoserKingdomName,
                report.GovernorName,
                report.Wounded,
                report.Killed,
                report.WoundedDefender,
                report.KilledDefender,
                report.WarScoreChange,
                report.PlayerWon,
                report.PlayerInArmy);
            Debug.Print("CasusBelli: Relatório de cerco enfileirado. Fila tamanho=" + _siegeReportQueue.Count);
        }

        public void QueueSiegeReport(
            string settlementName,
            string winnerKingdomName,
            string loserKingdomName,
            string governorName,
            int woundedAttacker,
            int killedAttacker,
            int woundedDefender,
            int killedDefender,
            double warScoreChange,
            bool playerWon,
            bool playerInArmy)
        {
            double showAt = CampaignTime.Now.ToHours + 1.0; // 1 hora depois

            _siegeReportQueue.Enqueue(new QueuedSiegeReport
            {
                SettlementName = settlementName,
                WinnerKingdomName = winnerKingdomName,
                LoserKingdomName = loserKingdomName,
                GovernorName = governorName,
                Wounded = woundedAttacker,
                Killed = killedAttacker,
                WoundedDefender = woundedDefender,
                KilledDefender = killedDefender,
                WarScoreChange = warScoreChange,
                PlayerWon = playerWon,
                PlayerInArmy = playerInArmy,
                ShowAtTime = showAt
            });
        }

        private void ProcessSiegeReportQueue()
        {
            if (_siegeReportQueue.Count == 0) return;

            double now = CampaignTime.Now.ToHours;

            Debug.Print("CasusBelli: ProcessSiegeReportQueue INI now=" + now + " fila=" + _siegeReportQueue.Count);

            while (_siegeReportQueue.Count > 0)
            {
                var report = _siegeReportQueue.Peek();

                if (now >= report.ShowAtTime)
                {
                    Debug.Print("CasusBelli: ProcessSiegeReportQueue - EXIBINDO relatório settlement=" + report.SettlementName);
                    _siegeReportQueue.Dequeue();
                    ShowSiegeReport(report);
                }
                else
                {
                    Debug.Print("CasusBelli: ProcessSiegeReportQueue - AGUARDANDO relatório settlement=" + report.SettlementName + " showAt=" + report.ShowAtTime + " agora=" + now);
                    break; // fila ordenada por tempo
                }
            }
        }

        private void ShowSiegeReport(QueuedSiegeReport report)
        {
            if (_screen == null || _siegeReportLayer != null)
            {
                Debug.Print("CasusBelli: ShowSiegeReport - SAINDO cedo screen=" + (_screen != null) + " layer=" + (_siegeReportLayer != null));
                return;
            }

            try
            {
                Debug.Print("CasusBelli: ShowSiegeReport - Abrindo janela para settlement=" + report.SettlementName);
                _siegeReportVm = new CasusBelliSiegeReportVM(CloseSiegeReport);
                _siegeReportVm.SetData(
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

                _siegeReportLayer = new GauntletLayer("SiegeReportLayer", 600, false);
                _siegeReportMovie = _siegeReportLayer.LoadMovie("CBSiegeReport", _siegeReportVm);

                _screen.AddLayer(_siegeReportLayer);

                // Janela modal: bloqueia o mapa por baixo enquanto aberta.
                _siegeReportLayer.InputRestrictions.SetInputRestrictions(
                    true,
                    InputUsageMask.All);
                _siegeReportLayer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(_siegeReportLayer);

                Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
                Debug.Print("CasusBelli: ShowSiegeReport - Janela aberta com sucesso");
            }
            catch (Exception ex)
            {
                Debug.Print("CasusBelli: ShowSiegeReport EXCEPTION: " + ex.GetType().Name + ": " + ex.Message);
                WarUiHelper.Message("CB: falha ao abrir relatorio de cerco: " + ex.GetType().Name + ": " + ex.Message);
                CloseSiegeReport();
            }
        }

        private void CloseSiegeReport()
        {
            Debug.Print("CasusBelli: CloseSiegeReport - Fechando janela");
            if (_siegeReportLayer == null)
            {
                return;
            }

            try
            {
                _siegeReportLayer.InputRestrictions.ResetInputRestrictions();
                _siegeReportLayer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(_siegeReportLayer);

                if (_siegeReportMovie != null)
                {
                    _siegeReportLayer.ReleaseMovie((GauntletMovieIdentifier)_siegeReportMovie);
                }

                if (_screen != null)
                {
                    _screen.RemoveLayer(_siegeReportLayer);
                }
            }
            catch (Exception)
            {
            }

            _siegeReportMovie = null;
            _siegeReportLayer = null;
            _siegeReportVm = null;
        }

        private void Attach(MapScreen screen)
        {
            try
            {
                _iconVm = new CasusBelliWarIconVM(OpenWindow, SetIconHover);

                // Valores iniciais ANTES de carregar o filme: assim o icone
                // aparece certo mesmo se algum aviso de mudanca falhar.
                _iconVm.Refresh();

                // VERIFIQUE: o construtor de GauntletLayer muda entre
                // versoes (pode ser GauntletLayer(int, string, bool)).
                _iconLayer = new GauntletLayer("IconLayer", 450, false);

                // O icone NAO chama SetInputRestrictions: pedir "mouse
                // visivel" aqui brigava com o mapa (que esconde e trava o
                // cursor ao girar a camera com o botao direito).

                _iconMovie = _iconLayer.LoadMovie("CBWarIcon", _iconVm);
                screen.AddLayer(_iconLayer);

                _screen = screen;
                _failedScreen = null;
                CasusBelliWarScoreBehavior.Changed += OnScoreChanged;
                CasusBelliWarScoreBehavior.WindowOpener = OpenWindow;
                CasusBelliWarScoreBehavior.BattleReportReady += OnBattleReportReady;
                CasusBelliWarScoreBehavior.SiegeReportReady += OnSiegeReportReady;
            }
            catch (Exception ex)
            {
                // Antes este erro era engolido em silencio. Causas comuns:
                // CBWarIcon.xml fora de Modules/CasusBelli/GUI/Prefabs, ou
                // erro dentro do proprio XML.
                WarUiHelper.Message("CB: falha ao carregar a interface " +
                    "(CBWarIcon): " + ex.GetType().Name + ": " + ex.Message);
                Detach();
                _failedScreen = screen;
            }
        }

        private void Detach()
        {
            CasusBelliWarScoreBehavior.Changed -= OnScoreChanged;
            CasusBelliWarScoreBehavior.WindowOpener = null;
            CasusBelliWarScoreBehavior.BattleReportReady -= OnBattleReportReady;
            CasusBelliWarScoreBehavior.SiegeReportReady -= OnSiegeReportReady;

            CloseWindow();
            CloseBattleReport();
            CloseSiegeReport();

            try
            {
                if (_iconLayer != null && _iconMovie != null)
                {
                    _iconLayer.ReleaseMovie((GauntletMovieIdentifier)_iconMovie);
                }

                if (_screen != null && _iconLayer != null)
                {
                    _screen.RemoveLayer(_iconLayer);
                }
            }
            catch (Exception)
            {
            }

            _iconMovie = null;
            _iconLayer = null;
            _iconVm = null;
            _screen = null;
        }

        private void SetIconHover(bool hovering)
        {
            _hovering = hovering;
        }

        private void OpenWindow()
        {
            if (_screen == null || _windowLayer != null)
            {
                return;
            }

            CasusBelliWarScoreBehavior ws = WarUiHelper.GetWarScore();
            Kingdom kingdom = WarUiHelper.GetPlayerKingdom();

            if (ws == null || kingdom == null || ws.GetSelectedWar(kingdom) == null)
            {
                WarUiHelper.Message("CB: seu reino nao esta em guerra.");
                return;
            }

            SetIconHover(false);

            try
            {
                _windowVm = new CasusBelliWarWindowVM(CloseWindow);
                _windowVm.Refresh();
                _windowLayer = new GauntletLayer("IconLayer", 500, false);
                _windowMovie = _windowLayer.LoadMovie("CBWarWindow", _windowVm);

                // A camada precisa estar na tela ANTES de receber o foco,
                // senao o foco e ignorado e nada na janela responde.
                _screen.AddLayer(_windowLayer);

                // Janela modal: bloqueia o mapa por baixo enquanto aberta.
                _windowLayer.InputRestrictions.SetInputRestrictions(
                    true,
                    InputUsageMask.All);
                _windowLayer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(_windowLayer);

                // VERIFIQUE: pausa o tempo da campanha enquanto negocia.
                Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;

            }
            catch (Exception ex)
            {
                WarUiHelper.Message("CB: falha ao abrir a janela " +
                    "(CBWarWindow): " + ex.GetType().Name + ": " + ex.Message);
                CloseWindow();
            }
        }

        private void CloseWindow()
        {
            if (_windowLayer == null)
            {
                return;
            }

            try
            {
                _windowLayer.InputRestrictions.ResetInputRestrictions();
                _windowLayer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(_windowLayer);

                if (_windowMovie != null)
                {
                    _windowLayer.ReleaseMovie((GauntletMovieIdentifier)_windowMovie);
                }

                if (_screen != null)
                {
                    _screen.RemoveLayer(_windowLayer);
                }
            }
            catch (Exception)
            {
            }

            _windowMovie = null;
            _windowLayer = null;
            _windowVm = null;

            if (_iconVm != null)
            {
                _iconVm.Refresh();
            }
        }
    }
#endif
}
#endif