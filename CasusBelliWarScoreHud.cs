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
        private bool _isVisible;
        private string _percentText = "";
        private string _percentColor = "#D4AF37FF";
        private string _shieldColor = "#808080FF";
        private string _leftColor = "#808080FF";

        public CasusBelliWarIconVM(Action open, Action<bool> hover)
        {
            _open = open;
            _hover = hover;
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
            WarRecord war = ws != null && kingdom != null
                ? ws.GetSelectedWar(kingdom)
                : null;

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
            Offers = new MBBindingList<CasusBelliOfferVM>();
        }

        [DataSourceProperty]
        public MBBindingList<CasusBelliOfferVM> Offers { get; private set; }

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
            SiegesText = Line("Cercos", b.Sieges * sign);
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

            DemandsText = "Se voce forcar: " + ws.PreviewDemands(war, kingdom) +
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

            string result;
            kings.ExecuteOffer(record, type, out result);
            WarUiHelper.Message("CB: " + result);
            Refresh();
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

            string result;

            if (kind == 0)
            {
                ws.TryEnforceDemands(war, kingdom, out result);
            }
            else if (kind == 1)
            {
                ws.TryWhitePeace(war, out result);
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

            CloseWindow();

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
