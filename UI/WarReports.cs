using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli
{
    // ---------------------------------------------------------------
    // RELATORIO DE BATALHA (mostrado 1h apos fim da missao)
    // ---------------------------------------------------------------
    public class CasusBelliBattleReportVM : ViewModel
    {
        private readonly Action _close;

        public CasusBelliBattleReportVM(Action close)
        {
            _close = close;
        }

        private string _title = "";
        private string _description = "";
        private string _casualtiesText = "";
        private string _prisonersText = "";
        private string _warScoreText = "";
        private string _warScoreColor = "#D4AF37FF";
        private bool _isVisible;

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
        public string Description { get { return _description; } set { Set(ref _description, value, "Description"); } }
        [DataSourceProperty]
        public string CasualtiesText { get { return _casualtiesText; } set { Set(ref _casualtiesText, value, "CasualtiesText"); } }
        [DataSourceProperty]
        public string PrisonersText { get { return _prisonersText; } set { Set(ref _prisonersText, value, "PrisonersText"); } }
        [DataSourceProperty]
        public string WarScoreText { get { return _warScoreText; } set { Set(ref _warScoreText, value, "WarScoreText"); } }
        [DataSourceProperty]
        public string WarScoreColor { get { return _warScoreColor; } set { Set(ref _warScoreColor, value, "WarScoreColor"); } }
        [DataSourceProperty]
        public bool IsVisible { get { return _isVisible; } set { Set(ref _isVisible, value, "IsVisible"); } }

        public void SetData(
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
            Title = "Relatório de Batalha";

            string locText = settlementName != ""
                ? "Batalha de " + settlementName
                : "Batalha";
            string resultText = playerWon ? "Vitória" : "Derrota";

            string whoWe;
            string whoThey;

            Kingdom playerKingdom = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
            bool isPlayerRuler = playerKingdom != null && playerKingdom.Leader == Hero.MainHero;

            if (playerWon)
            {
                if (playerLedWinningArmy)
                {
                    // Jogador liderou o exército vencedor
                    whoWe = "O exército comandado por você";
                }
                else if (isPlayerRuler)
                {
                    // Jogador é governante, mas vassalo liderou
                    whoWe = "O exército de seu vassalo";
                }
                else
                {
                    // Jogador não é governante e não estava na batalha
                    whoWe = "As forças de seu reino";
                }

                whoThey = !string.IsNullOrEmpty(enemyLeaderName)
                    ? "o exército de " + enemyLeaderName + " do(a) " + heroName
                    : "as forças do(a) " + heroName;

                string actionText = "derrotou";

                Description = locText + "\n\n" + resultText + " - " +
                    whoWe + " " + actionText + " " + whoThey;
            }
            else
            {
                // Derrota
                if (playerLedWinningArmy)
                {
                    // Jogador liderou mas perdeu (improvável mas possível)
                    whoWe = "O exército comandado por você";
                }
                else if (isPlayerRuler)
                {
                    whoWe = "O exército de seu vassalo";
                }
                else
                {
                    whoWe = "As forças de seu reino";
                }

                whoThey = !string.IsNullOrEmpty(enemyLeaderName)
                    ? "o exército de " + enemyLeaderName + " do(a) " + heroName
                    : "as forças do(a) " + heroName;

                string actionText = "foi derrotado por";

                Description = locText + "\n\n" + resultText + " - " +
                    whoWe + " " + actionText + " " + whoThey;
            }

            CasualtiesText = "No total foram " + wounded + " feridos e " + killed + " mortos";

            if (capturedLords != null && capturedLords.Count > 0)
            {
                string prisoners = "";
                foreach (Hero h in capturedLords)
                {
                    if (!string.IsNullOrEmpty(prisoners)) prisoners += ", ";
                    prisoners += h.Name.ToString();
                }
                PrisonersText = "Prisioneiros lordes capturados: " + prisoners;
            }
            else
            {
                PrisonersText = "Nenhum lorde capturado";
            }

            string sign = warScoreChange >= 0 ? "+" : "";
            WarScoreText = "WarScore " + sign + warScoreChange.ToString("0.#") + "%";
            WarScoreColor = WarUiHelper.ScoreColor(warScoreChange);
            IsVisible = true;
        }

        public void ExecuteClose()
        {
            _close();
            IsVisible = false;
        }
    }

    // ---------------------------------------------------------------
    // RELATORIO DE CERCO (mostrado 1h apos fim do cerco)
    // ---------------------------------------------------------------
    public class CasusBelliSiegeReportVM : ViewModel
    {
        private readonly Action _close;

        public CasusBelliSiegeReportVM(Action close)
        {
            _close = close;
        }

        private string _title = "";
        private string _description = "";
        private string _warScoreText = "";
        private string _warScoreColor = "#D4AF37FF";
        private string _casualtiesText = "";
        private bool _isVisible;

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
        public string Description { get { return _description; } set { Set(ref _description, value, "Description"); } }
        [DataSourceProperty]
        public string WarScoreText { get { return _warScoreText; } set { Set(ref _warScoreText, value, "WarScoreText"); } }
        [DataSourceProperty]
        public string WarScoreColor { get { return _warScoreColor; } set { Set(ref _warScoreColor, value, "WarScoreColor"); } }
        [DataSourceProperty]
        public string CasualtiesText { get { return _casualtiesText; } set { Set(ref _casualtiesText, value, "CasualtiesText"); } }
        [DataSourceProperty]
        public bool IsVisible { get { return _isVisible; } set { Set(ref _isVisible, value, "IsVisible"); } }

        public void SetData(
            string settlementName,
            string governorName,
            int woundedAttacker,
            int killedAttacker,
            int woundedDefender,
            int killedDefender,
            string winnerKingdomName,
            string loserKingdomName,
            double warScoreChange,
            bool playerWon,
            bool playerInArmy)
        {
            Title = "Relatório de Cerco";

            string settlementType = settlementName.Contains("Castelo") || settlementName.Contains("Castle") || settlementName.Contains("Fortaleza") ? "castelo" : "cidade";
            string resultText = playerWon ? "Vitória" : "Derrota";

            string whoWe;

            if (playerInArmy)
            {
                whoWe = "O exército comandado por você";
            }
            else
            {
                whoWe = "Sua companhia pessoal";
            }

            string actionText = playerWon
                ? "conquistou"
                : "perdeu";

            string theyText = !string.IsNullOrEmpty(governorName)
                ? "o " + settlementType + " de " + loserKingdomName + " (defendido por " + governorName + ")"
                : "o " + settlementType + " de " + loserKingdomName;

            Description = "Cerco de " + settlementName + "\n\n" + resultText + " - " +
                whoWe + " " + actionText + " " + theyText;

            // Baixas por lado
            CasualtiesText = "Atacante: " + woundedAttacker + " feridos e " + killedAttacker + " mortos\n" +
                             "Defensor: " + woundedDefender + " feridos e " + killedDefender + " mortos";

            string sign = warScoreChange >= 0 ? "+" : "";
            WarScoreText = "WarScore " + sign + warScoreChange.ToString("0.#") + "%";
            WarScoreColor = WarUiHelper.ScoreColor(warScoreChange);
            IsVisible = true;
        }

        public void ExecuteClose()
        {
            _close();
            IsVisible = false;
        }
    }
}