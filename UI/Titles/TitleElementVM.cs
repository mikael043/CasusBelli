using CasusBelli.Managers.Titles;
using TaleWorlds.Library;

namespace CasusBelli.UI.Titles
{
    // Elemento de título na árvore hierárquica
    public class TitleElementVM : ViewModel
    {
        private MBBindingList<TitleElementVM> _branch;
        private TitleVM _title;

        public TitleElementVM(FeudalTitle title, DemesneHierarchyVM hierarchy)
        {
            Branch = new MBBindingList<TitleElementVM>();
            Title = new TitleVM(title);

            if (title.Fief != null)
            {
                // Adicionar população se houver sistema de população
            }

            if (title.Vassals != null)
            {
                foreach (var vassal in title.Vassals)
                {
                    Branch.Add(new TitleElementVM(vassal, hierarchy));

                    if (vassal.Fief != null)
                    {
                        // Adicionar população das vilas
                    }
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<TitleElementVM> Branch
        {
            get => _branch;
            set
            {
                if (value != _branch)
                {
                    _branch = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public TitleVM Title
        {
            get => _title;
            set
            {
                if (value != _title)
                {
                    _title = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        public override void RefreshValues()
        {
            base.RefreshValues();
            Branch.ApplyActionOnAllItems(x => x.RefreshValues());
            Title.RefreshValues();
        }
    }
}