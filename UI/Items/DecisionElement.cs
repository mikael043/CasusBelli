using System;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CasusBelli.UI.Items
{
    public class DecisionElement : ViewModel
    {
        private Action<bool> _booleanAction;
        private bool _booleanValue;
        public bool Show { get; set; }
        public bool Enabled { get; set; }
        public int OptionTypeID { get; set; }
        private HintViewModel _hint;
        public string Description { get; set; }
        public bool IsDiscrete { get; set; }

        [DataSourceProperty]
        public string ButtonName { get; set; }

        public Action OnPressAction { get; set; }

        public bool OptionValueAsBoolean
        {
            get => _booleanValue;
            set
            {
                if (value != _booleanValue)
                {
                    _booleanValue = value;
                    OnPropertyChanged();
                    _booleanAction?.Invoke(value);
                }
            }
        }

        [DataSourceProperty]
        public HintViewModel Hint
        {
            get => _hint;
            set
            {
                if (value != _hint)
                {
                    _hint = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        public DecisionElement SetAsBooleanOption(string desc, bool initialValue, Action<bool> onChange, TextObject hintText)
        {
            Hint = new HintViewModel(hintText);
            OptionTypeID = 1;
            Description = desc;
            _booleanValue = initialValue;
            _booleanAction = onChange;
            Show = true;
            Enabled = true;
            return this;
        }

        public DecisionElement SetAsButtonOption(string buttonName, Action onPress, TextObject hintText = null)
        {
            OptionTypeID = 3;
            ButtonName = buttonName;
            OnPressAction = onPress;
            Hint = new HintViewModel(hintText);
            Show = true;
            Enabled = true;
            return this;
        }

        public void OnPress()
        {
            OnPressAction?.Invoke();
        }
    }
}