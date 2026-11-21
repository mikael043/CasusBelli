using System.Collections.Generic;
using CasusBelli.Managers.Titles;
using CasusBelli.UI.Items;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CasusBelli.UI.Titles
{
    // ViewModel principal para um título (equivalente ao TitleVM do BannerKings)
    public class TitleVM : ViewModel
    {
        private MBBindingList<DecisionElement> _decisions;
        private BasicTooltipViewModel _hint;
        private HeroViewModel _imageIdentifier;
        private readonly FeudalTitle _title;
        private bool _isHidden;

        public TitleVM(FeudalTitle title)
        {
            _title = title;
            _decisions = new MBBindingList<DecisionElement>();
            ImageIdentifier = new HeroViewModel(CharacterViewModel.StanceTypes.EmphasizeFace);
            RefreshValues();
        }

        [DataSourceProperty]
        public string NameText => _title.FullName.ToString();

        public override void RefreshValues()
        {
            base.RefreshValues();
            Decisions.Clear();

            if (_title != null)
            {
                if (_title.deJure != null)
                    ImageIdentifier.FillFrom(_title.deJure, -1, false, true);
                else
                {
                    ImageIdentifier.FillFrom(Hero.MainHero, -1, false, true);
                    ImageIdentifier.IsHidden = true;
                }

                ImageIdentifier.SetEquipment(EquipmentIndex.ArmorItemEndSlot, default(EquipmentElement));
                ImageIdentifier.SetEquipment(EquipmentIndex.HorseHarness, default(EquipmentElement));
                ImageIdentifier.SetEquipment(EquipmentIndex.NumAllWeaponSlots, default(EquipmentElement));

                IsHidden = _title.deJure == null || ImageIdentifier.IsHidden;

                var actions = new List<TitleAction>();
                var model = TitleManager.Instance.GetModel();

                if (_title.deJure == null)
                {
                    var createAction = model.GetAction(ActionType.Create, _title, Hero.MainHero);
                    if (createAction.Possible)
                    {
                        var createButton = new DecisionElement().SetAsButtonOption(
                            new TextObject("{=BKCreateTitle}Create").ToString(),
                            () => UIHelper.ShowActionPopup(createAction, this),
                            new TextObject("{=BKCreateTitleHint}Creating a title sets you as its legal holder. The title's laws will match those of your current primary title."));
                        createButton.Enabled = createAction.Possible;
                        Decisions.Add(createButton);
                    }

                    actions.Add(createAction);
                }
                else
                {
                    var usurpData = model.GetAction(ActionType.Usurp, _title, Hero.MainHero);

                    // Verificar se está no mesmo reino
                    bool holderInRealm = _title.deJure != null && _title.deJure != Hero.MainHero
                        && _title.deJure.Clan?.Kingdom != null
                        && _title.deJure.Clan.Kingdom == Hero.MainHero.Clan?.Kingdom;

                    // Se no mesmo reino e tem claim válido, pode usurpar
                    if (holderInRealm && usurpData.Possible)
                    {
                        var pressButton = new DecisionElement().SetAsButtonOption(
                            new TextObject("{=BKPressClaim}Press Claim").ToString(),
                            () => PressClaim(_title),
                            new TextObject("{=BKPressClaimHint}Press your claim before the realm. Your peers take sides; muster enough support and the title changes hands."));
                        pressButton.Enabled = true;
                        Decisions.Add(pressButton);
                    }
                    else if (usurpData.Possible || _title.GetHeroClaim(Hero.MainHero) != ClaimType.None)
                    {
                        var usurpButton = new DecisionElement().SetAsButtonOption(
                            new TextObject("{=BKUsurp}Usurp").ToString(),
                            () => UIHelper.ShowActionPopup(usurpData, this));
                        usurpButton.Enabled = usurpData.Possible;
                        Decisions.Add(usurpButton);
                    }

                    var claimAction = model.GetAction(ActionType.Claim, _title, Hero.MainHero);
                    if (claimAction.Possible)
                    {
                        var claimButton = new DecisionElement().SetAsButtonOption(
                            new TextObject("{=BKClaim}Claim").ToString(),
                            () => UIHelper.ShowActionPopup(claimAction, this));
                        claimButton.Enabled = claimAction.Possible;
                        Decisions.Add(claimButton);
                    }

                    var grantData = model.GetAction(ActionType.Grant, _title, Hero.MainHero);
                    if (grantData.Possible)
                    {
                        var grantButton = new DecisionElement().SetAsButtonOption(
                            new TextObject("{=BKGrant}Grant").ToString(),
                            () => UIHelper.ShowActionPopup(grantData, this));
                        grantButton.Enabled = grantData.Possible;
                        Decisions.Add(grantButton);
                    }

                    var revokeData = model.GetAction(ActionType.Revoke, _title, Hero.MainHero);
                    if (revokeData.Possible)
                    {
                        var revokeButton = new DecisionElement().SetAsButtonOption(
                            new TextObject("{=BKRevoke}Revoke").ToString(),
                            () => UIHelper.ShowActionPopup(revokeData, this));
                        revokeButton.Enabled = revokeData.Possible;
                        Decisions.Add(revokeButton);
                    }

                    if (_title.deJure != Hero.MainHero)
                    {
                        actions.Add(usurpData);
                        actions.Add(claimAction);
                        actions.Add(revokeData);
                    }
                    else
                    {
                        actions.Add(grantData);
                    }
                }

                Hint = new BasicTooltipViewModel(() => UIHelper.GetTitleTooltip(_title, actions));
            }
        }

        private void PressClaim(FeudalTitle pressedTitle)
        {
            var kingdom = Hero.MainHero?.Clan?.Kingdom;
            if (kingdom == null || pressedTitle?.deJure == null) return;

            // Integrar com sistema de dilemas/diplomacia do CasusBelli
            // Por enquanto, usurpação direta
            var action = TitleManager.Instance.GetModel().GetAction(ActionType.Usurp, pressedTitle, Hero.MainHero);
            if (action.Possible)
            {
                action.TakeAction(null);

                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=BKPressClaimMsg}You press your claim on {TITLE}.")
                        .SetTextVariable("TITLE", pressedTitle.FullName).ToString(),
                    Color.FromUint(4278255360)));
            }
        }

        public void ExecuteLink()
        {
            if (_title.deJure != null)
            {
                Campaign.Current.EncyclopediaManager.GoToLink(_title.deJure.EncyclopediaLink);
            }
        }

        [DataSourceProperty]
        public bool IsHidden
        {
            get => _isHidden;
            set
            {
                if (value != _isHidden)
                {
                    _isHidden = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<DecisionElement> Decisions
        {
            get => _decisions;
            set
            {
                if (value != _decisions)
                {
                    _decisions = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public BasicTooltipViewModel Hint
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

        [DataSourceProperty]
        public HeroViewModel ImageIdentifier
        {
            get => _imageIdentifier;
            set
            {
                if (value != _imageIdentifier)
                {
                    _imageIdentifier = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }
    }
}