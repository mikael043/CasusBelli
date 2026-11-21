using System.Collections.Generic;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace CasusBelli.UI.Titles
{
    // ViewModel principal para a hierarquia de títulos (tela de domínios)
    public class DemesneHierarchyVM : ViewModel
    {
        private MBBindingList<TitleElementVM> _titles;
        private int _population;

        public DemesneHierarchyVM(Kingdom kingdom = null)
        {
            _titles = new MBBindingList<TitleElementVM>();

            if (kingdom == null)
                kingdom = Hero.MainHero.Clan.Kingdom;

            if (kingdom != null)
            {
                RefreshTitles(kingdom);
            }
        }

        public void RefreshTitles(Kingdom kingdom)
        {
            _titles.Clear();

            var sovereignTitles = TitleManager.Instance.GetSovereignTitleList(kingdom);

            foreach (var sovereign in sovereignTitles)
            {
                if (sovereign.TitleType <= TitleType.Kingdom)
                {
                    _titles.Add(new TitleElementVM(sovereign, this));
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<TitleElementVM> Titles
        {
            get => _titles;
            set
            {
                if (value != _titles)
                {
                    _titles = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public int Population
        {
            get => _population;
            set
            {
                if (value != _population)
                {
                    _population = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }
    }
}