using CasusBelli.Managers.Titles;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CasusBelli.UI.Titles
{
    // Item para seleção de títulos (enciclopédia, diálogos, etc.)
    public class TitleSelectorItem
    {
        private readonly FeudalTitle _title;

        public TitleSelectorItem(FeudalTitle title)
        {
            _title = title;
        }

        public FeudalTitle Title => _title;

        public string Name => _title.FullName.ToString();

        public string TypeName => TextHelper.GetRankName(_title.TitleType);

        public string FiefName => _title.Fief?.Name?.ToString() ?? "Unlanded";

        public string HolderName => _title.deJure?.Name?.ToString() ?? "Vacant";

        public string DeFactoHolderName => _title.DeFactoHolder?.Name?.ToString() ?? "None";

        public bool IsUsurped => _title.IsUsurped;

        public bool IsSovereign => _title.IsSovereignLevel;

        public override string ToString()
        {
            return Name;
        }
    }
}