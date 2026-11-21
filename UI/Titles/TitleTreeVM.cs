using System;
using System.Collections.Generic;
using CasusBelli.Managers.Titles;
using CasusBelli.UI.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace CasusBelli.UI.Titles
{
    // ViewModel para a árvore de títulos (hierarquia feuda)
    public class TitleTreeVM : ViewModel
    {
        private MBBindingList<TitleElementVM> _rootTitles;
        private FeudalTitle _selectedTitle;
        private string _searchText;
        private Kingdom _selectedKingdom;
        private bool _showVacantTitles = true;
        private bool _showDemesneView = false;

        public TitleTreeVM()
        {
            RootTitles = new MBBindingList<TitleElementVM>();
            RefreshTitles();
        }

        public override void RefreshValues()
        {
            base.RefreshValues();
            RefreshTitles();
        }

        [DataSourceProperty]
        public MBBindingList<TitleElementVM> RootTitles
        {
            get => _rootTitles;
            set
            {
                _rootTitles = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public FeudalTitle SelectedTitle
        {
            get => _selectedTitle;
            set
            {
                _selectedTitle = value;
                OnPropertyChangedWithValue(value);
                OnPropertyChanged(nameof(SelectedTitleName));
                OnPropertyChanged(nameof(SelectedTitleFief));
            }
        }

        [DataSourceProperty]
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                    RefreshTitles();
                }
            }
        }

        [DataSourceProperty]
        public Kingdom SelectedKingdom
        {
            get => _selectedKingdom;
            set
            {
                _selectedKingdom = value;
                OnPropertyChanged(nameof(SelectedKingdom));
                ApplyFilters();
            }
        }

        [DataSourceProperty]
        public bool ShowVacantTitles
        {
            get => _showVacantTitles;
            set
            {
                if (_showVacantTitles != value)
                {
                    _showVacantTitles = value;
                    OnPropertyChanged(nameof(ShowVacantTitles));
                    RefreshTitles();
                }
            }
        }

        [DataSourceProperty]
        public bool ShowDemesneView
        {
            get => _showDemesneView;
            set
            {
                _showDemesneView = value;
                OnPropertyChanged(nameof(ShowDemesneView));
            }
        }

        public string SelectedTitleName => SelectedTitle?.FullName?.ToString() ?? "Nenhum título selecionado";
        public string SelectedTitleFief => SelectedTitle?.Fief?.Name?.ToString() ?? "";

        public void RefreshTitles()
        {
            var manager = TitleManager.Instance;
            if (manager == null) return;

            RootTitles.Clear();

            // Filter by kingdom or show all
            var titles = manager.AllTitles;

            // Group by sovereigns (sovereign titles)
            var sovereignTitles = new List<FeudalTitle>();
            var otherTitles = new List<FeudalTitle>();

            foreach (var title in titles)
            {
                if (title == null) continue;

                // Filter by search text
                if (!string.IsNullOrEmpty(_searchText))
                {
                    var titleName = title.FullName?.ToString();
                    if (!string.IsNullOrEmpty(titleName) && titleName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) == -1)
                    {
                        continue;
                    }
                }

                // Filter vacant titles
                if (!ShowVacantTitles && title.deJure == null)
                {
                    continue;
                }

                if (title.IsSovereignLevel)
                {
                    sovereignTitles.Add(title);
                }
                else
                {
                    otherTitles.Add(title);
                }
            }

            // Add sovereign titles and their vassals
            foreach (var sovereign in sovereignTitles)
            {
                var element = new TitleElementVM(sovereign, null);
                RootTitles.Add(element);
            }
        }

        public void OpenDemesneView(FeudalTitle title)
        {
            if (title == null) return;

            ShowDemesneView = true;
            SelectedTitle = title;
            // Mostrar hierarquia detalhada
        }

        public void CloseDemesneView()
        {
            ShowDemesneView = false;
            SelectedTitle = null;
        }

        public void FilterByKingdom(Kingdom kingdom)
        {
            SelectedKingdom = kingdom;
        }

        public void ApplyFilters()
        {
            RefreshTitles();
        }

        // Command to close the window
        public void CloseWindow()
        {
            Campaign.Current?.GetCampaignBehavior<TitleUIBehavior>()?.Close();
        }
    }
}