using System;
using System.Linq;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace CasusBelli.UI.Titles
{
    // Data + commands for the prefab-backed hierarchy screen.
    // No Gauntlet widgets are created here.
    internal sealed class KingdomHierarchyVM : ViewModel
    {
        private readonly Func<Kingdom> _defaultKingdom;
        private readonly MBBindingList<KingdomHierarchyNodeVM> _roots =
            new MBBindingList<KingdomHierarchyNodeVM>();
        private readonly KingdomHierarchyDetailsVM _details;

        private Kingdom _realm;
        private FeudalHierarchyTree _tree;
        private FeudalTitle _selectedTitle;
        private BannerImageIdentifierVM _realmBanner;
        private string _realmName = "";
        private string _countsText = "";
        private string _selectedText = "";
        private bool _isOpen;
        private bool _deFacto;

        internal KingdomHierarchyVM(Func<Kingdom> defaultKingdom)
        {
            _defaultKingdom = defaultKingdom ?? (() => null);
            _details = new KingdomHierarchyDetailsVM(this);
        }

        internal Kingdom Realm => _realm;
        internal bool IsDeFacto => _deFacto;

        [DataSourceProperty]
        public MBBindingList<KingdomHierarchyNodeVM> Roots => _roots;

        [DataSourceProperty]
        public KingdomHierarchyDetailsVM Details => _details;

        [DataSourceProperty]
        public BannerImageIdentifierVM RealmBanner
        {
            get => _realmBanner;
            private set
            {
                if (_realmBanner == value) return;
                _realmBanner = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string RealmName
        {
            get => _realmName;
            private set
            {
                if (_realmName == value) return;
                _realmName = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string CountsText
        {
            get => _countsText;
            private set
            {
                if (_countsText == value) return;
                _countsText = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string SelectedText
        {
            get => _selectedText;
            private set
            {
                if (_selectedText == value) return;
                _selectedText = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsOpen
        {
            get => _isOpen;
            private set
            {
                if (_isOpen == value) return;
                _isOpen = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool CanDeFacto => !_deFacto;

        [DataSourceProperty]
        public bool CanDeJure => _deFacto;

        [DataSourceProperty]
        public bool HasSelection => _selectedTitle != null;

        public void ExecuteOpen()
        {
            Kingdom kingdom = _defaultKingdom();
            if (kingdom != null)
                SetRealm(kingdom, true);
            else if (_realm == null)
                return;

            IsOpen = true;
        }

        internal void Close()
        {
            _details.Close();
            IsOpen = false;
        }

        internal void SetDefaultKingdom(Kingdom kingdom)
        {
            if (IsOpen || kingdom == null || kingdom == _realm) return;
            SetRealm(kingdom, true);
        }

        public void ExecuteDeFacto()
        {
            if (_deFacto) return;
            _deFacto = true;
            OnPropertyChanged(nameof(CanDeFacto));
            OnPropertyChanged(nameof(CanDeJure));
            RefreshTree(true);
        }

        public void ExecuteDeJure()
        {
            if (!_deFacto) return;
            _deFacto = false;
            OnPropertyChanged(nameof(CanDeFacto));
            OnPropertyChanged(nameof(CanDeJure));
            RefreshTree(true);
        }

        public void ExecuteChangeRealm()
        {
            var options = Kingdom.All
                .Where(k => k != null && !k.IsEliminated)
                .OrderBy(k => k.Name.ToString())
                .Select(k => new InquiryElement(k, k.Name.ToString(), null))
                .ToList();

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Escolher reino",
                "Selecione o reino para visualizar a hierarquia feudal.",
                options, true, 1, 1, "Abrir", "Voltar",
                choices =>
                {
                    if (choices.Count == 1 && choices[0].Identifier is Kingdom kingdom)
                        SetRealm(kingdom, true);
                }, null, isSeachAvailable: true));
        }

        public void ExecuteHierarchy()
        {
            RefreshTree(false);
        }

        public void ExecutePeers()
        {
            if (_realm != null)
                RealmPeers.Show(_realm, () => { });
        }

        public void ExecuteCouncil()
        {
            if (_realm != null)
                RoyalCouncilMenu.Open(_realm, () => { });
        }

        public void ExecutePolitics()
        {
            if (_realm != null)
                PoliticsMenu.Open(_realm);
        }

        public void ExecuteDisputes()
        {
            if (_realm != null)
                DisputeMenu.Open(_realm, () => { });
        }

        public void ExecuteDetails()
        {
            if (_selectedTitle == null) return;
            _details.Open(_selectedTitle);
        }

        public void ExecuteClaim()
        {
            if (_selectedTitle == null) return;
            KingdomHierarchyTab.ConfirmClaim(_selectedTitle, () => RefreshTree(false));
        }

        internal Hero Owner(FeudalTitle title)
        {
            if (title == null) return null;
            Hero owner = _deFacto ? title.DeFactoHolder : title.deJure;
            if (owner == null && title.TitleType <= TitleType.Kingdom)
                owner = _realm?.Leader;
            return owner;
        }

        internal void Select(FeudalTitle title)
        {
            if (title == null) return;
            _selectedTitle = title;
            UpdateSelection();
            OnPropertyChanged(nameof(HasSelection));
        }

        internal void SelectAndOpenDetails(FeudalTitle title)
        {
            Select(title);
            _details.Open(title);
        }

        internal void RefreshAfterTitleAction(FeudalTitle title)
        {
            RefreshTree(false);
            if (title != null) _details.Open(title);
        }

        private void SetRealm(Kingdom kingdom, bool resetSelection)
        {
            if (kingdom == null) return;
            _realm = kingdom;
            RealmName = kingdom.Name?.ToString() ?? "";
            RealmBanner = kingdom.Banner != null
                ? new BannerImageIdentifierVM(kingdom.Banner, true)
                : null;
            RefreshTree(resetSelection);
        }

        private void RefreshTree(bool resetSelection)
        {
            _roots.Clear();

            var manager = TitleManager.Instance;
            if (manager == null || _realm == null)
            {
                _tree = null;
                CountsText = "";
                _selectedTitle = null;
                UpdateSelection();
                return;
            }

            _tree = FeudalHierarchyTree.Build(
                _realm, _deFacto, manager.GetSovereignTitleList(_realm), manager.AllTitles);

            if (resetSelection || _selectedTitle == null || !_tree.Titles.Contains(_selectedTitle))
                _selectedTitle = _tree.Roots.FirstOrDefault();

            foreach (var root in _tree.Roots)
                _roots.Add(new KingdomHierarchyNodeVM(root, this, _tree));

            var titles = _tree.Titles.ToList();
            CountsText =
                $"{titles.Count(t => t.TitleType == TitleType.Dukedom)} ducados  " +
                $"{titles.Count(t => t.TitleType == TitleType.County)} condados  " +
                $"{titles.Count(t => t.TitleType == TitleType.Barony)} baronias  " +
                $"{titles.Count(t => t.TitleType == TitleType.Lordship)} senhorios";

            UpdateSelection();
            OnPropertyChanged(nameof(HasSelection));
            if (_details.IsOpen && _selectedTitle != null)
                _details.Open(_selectedTitle);
        }

        private void UpdateSelection()
        {
            SelectedText = _selectedTitle == null
                ? "Selecione um título"
                : $"{_selectedTitle.FullName}  |  Titular {(_deFacto ? "atual" : "de jure")}: " +
                  $"{Owner(_selectedTitle)?.Name?.ToString() ?? "Vago"}";
        }
    }

    internal sealed class KingdomHierarchyNodeVM : ViewModel
    {
        private readonly FeudalTitle _title;
        private readonly KingdomHierarchyVM _hierarchy;
        private readonly MBBindingList<KingdomHierarchyNodeVM> _branch =
            new MBBindingList<KingdomHierarchyNodeVM>();
        private CharacterImageIdentifierVM _portraitIdentifier;
        private BannerImageIdentifierVM _clanBanner;
        private bool _isHidden;

        internal KingdomHierarchyNodeVM(
            FeudalTitle title,
            KingdomHierarchyVM hierarchy,
            FeudalHierarchyTree tree)
        {
            _title = title;
            _hierarchy = hierarchy;

            Hero owner = hierarchy.Owner(title);
            if (owner?.CharacterObject != null)
            {
                try
                {
                    PortraitIdentifier = new CharacterImageIdentifierVM(
                        CharacterCode.CreateFrom(owner.CharacterObject));
                    IsHidden = PortraitIdentifier == null || !PortraitIdentifier.IsValid;
                }
                catch
                {
                    IsHidden = true;
                }

                if (owner.Clan?.Banner != null)
                    ClanBanner = new BannerImageIdentifierVM(owner.Clan.Banner, true);
            }
            else
            {
                IsHidden = true;
            }

            foreach (var child in tree.ChildrenOf(title))
                _branch.Add(new KingdomHierarchyNodeVM(child, hierarchy, tree));
        }

        [DataSourceProperty]
        public string StringId => _title?.StringId ?? "";

        [DataSourceProperty]
        public string NameText => _title?.FullName?.ToString() ?? _title?.StringId ?? "Título";

        [DataSourceProperty]
        public string HolderText => _hierarchy.Owner(_title)?.Name?.ToString() ?? "Vago";

        [DataSourceProperty]
        public CharacterImageIdentifierVM PortraitIdentifier
        {
            get => _portraitIdentifier;
            private set
            {
                _portraitIdentifier = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public BannerImageIdentifierVM ClanBanner
        {
            get => _clanBanner;
            private set
            {
                _clanBanner = value;
                OnPropertyChangedWithValue(value);
                OnPropertyChanged(nameof(HasClanBanner));
            }
        }

        [DataSourceProperty]
        public bool HasClanBanner => _clanBanner != null;

        [DataSourceProperty]
        public bool IsHidden
        {
            get => _isHidden;
            private set
            {
                if (_isHidden == value) return;
                _isHidden = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public MBBindingList<KingdomHierarchyNodeVM> Branch => _branch;

        public void ExecuteSelect()
        {
            _hierarchy.SelectAndOpenDetails(_title);
        }

        public void ExecuteDetails()
        {
            _hierarchy.SelectAndOpenDetails(_title);
        }
    }

    internal sealed class KingdomHierarchyDetailsVM : ViewModel
    {
        private readonly KingdomHierarchyVM _hierarchy;
        private FeudalTitle _title;
        private Hero _owner;
        private HeroViewModel _character;
        private BannerImageIdentifierVM _clanBanner;
        private bool _isOpen;
        private bool _characterHidden = true;
        private string _titleName = "";
        private string _rankText = "";
        private string _holderName = "";
        private string _deJureText = "";
        private string _deFactoText = "";
        private string _lawsText = "";
        private string _claimText = "";
        private bool _canClaim;
        private bool _canDucalPolitics;

        internal KingdomHierarchyDetailsVM(KingdomHierarchyVM hierarchy)
        {
            _hierarchy = hierarchy;
            Character = new HeroViewModel(CharacterViewModel.StanceTypes.EmphasizeFace);
            Character.IsHidden = true;
        }

        [DataSourceProperty]
        public bool IsOpen
        {
            get => _isOpen;
            private set
            {
                if (_isOpen == value) return;
                _isOpen = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public HeroViewModel Character
        {
            get => _character;
            private set
            {
                _character = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public BannerImageIdentifierVM ClanBanner
        {
            get => _clanBanner;
            private set
            {
                _clanBanner = value;
                OnPropertyChangedWithValue(value);
                OnPropertyChanged(nameof(HasClanBanner));
            }
        }

        [DataSourceProperty]
        public bool HasClanBanner => _clanBanner != null;

        [DataSourceProperty]
        public bool CharacterHidden
        {
            get => _characterHidden;
            private set
            {
                if (_characterHidden == value) return;
                _characterHidden = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string TitleName
        {
            get => _titleName;
            private set { _titleName = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public string RankText
        {
            get => _rankText;
            private set { _rankText = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public string HolderName
        {
            get => _holderName;
            private set { _holderName = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public string DeJureText
        {
            get => _deJureText;
            private set { _deJureText = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public string DeFactoText
        {
            get => _deFactoText;
            private set { _deFactoText = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public string LawsText
        {
            get => _lawsText;
            private set { _lawsText = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public string ClaimText
        {
            get => _claimText;
            private set { _claimText = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public bool CanClaim
        {
            get => _canClaim;
            private set
            {
                if (_canClaim == value) return;
                _canClaim = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool CanDucalPolitics
        {
            get => _canDucalPolitics;
            private set
            {
                if (_canDucalPolitics == value) return;
                _canDucalPolitics = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool HasHolder => _owner != null;

        internal void Open(FeudalTitle title)
        {
            if (title == null) return;
            _title = title;
            RefreshValuesForTitle();
            IsOpen = true;
        }

        internal void Close()
        {
            IsOpen = false;
        }

        public void ExecuteClose()
        {
            Close();
        }

        public void ExecuteFeudalActions()
        {
            if (_title == null) return;
            FeudalActionsMenu.Open(_title, RefreshAfterAction);
        }

        public void ExecuteClaim()
        {
            if (_title == null) return;
            KingdomHierarchyTab.ConfirmClaim(_title, RefreshAfterAction);
        }

        public void ExecuteSupport()
        {
            if (!CanDucalPolitics || _title == null) return;
            DisputeMenu.PreviewDucalSupport(_title, Hero.MainHero, RefreshAfterAction);
        }

        public void ExecuteDucalDispute()
        {
            if (!CanDucalPolitics || _title == null) return;
            DisputeMenu.OpenDucal(_title, RefreshAfterAction);
        }

        public void ExecuteEncyclopedia()
        {
            if (_owner == null || Campaign.Current?.EncyclopediaManager == null) return;
            Campaign.Current.EncyclopediaManager.GoToLink(_owner.EncyclopediaLink);
        }

        private void RefreshAfterAction()
        {
            if (_title == null) return;
            _hierarchy.RefreshAfterTitleAction(_title);
        }

        private void RefreshValuesForTitle()
        {
            _owner = _hierarchy.Owner(_title);
            TitleName = _title.FullName?.ToString() ?? _title.StringId ?? "Título";
            RankText = TextHelper.GetRankName(_title.TitleType);
            HolderName = _owner?.Name?.ToString() ?? "Título vago";
            DeJureText = "De jure: " + (_title.deJure?.Name?.ToString() ?? "Vago");
            DeFactoText = "De facto: " + (_title.DeFactoHolder?.Name?.ToString() ?? "Vago");
            LawsText =
                "Herança: " + RealmLaw.Translate(_title.Contract?.Inheritance?.StringId) +
                "   •   Sucessão: " + RealmLaw.Translate(_title.Contract?.Succession?.StringId) +
                "   •   Gênero: " + RealmLaw.Translate(_title.Contract?.GenderLaw?.StringId);

            var claim = TitleManager.Instance?.GetModel()?.GetClaimAction(_title, Hero.MainHero);
            CanClaim = claim != null && claim.Possible;
            ClaimText = CanClaim
                ? $"Custo da reivindicação: {claim.Gold:0} ouro, {claim.Influence:0} influência, {claim.Renown:0} renome"
                : "Reivindicação: " + (claim?.Reason?.ToString() ?? "indisponível");

            CanDucalPolitics =
                _title.TitleType == TitleType.Dukedom &&
                _title.deJure != null &&
                _title.deJure != Hero.MainHero;

            Character = new HeroViewModel(CharacterViewModel.StanceTypes.EmphasizeFace);
            CharacterHidden = true;
            if (_owner != null)
            {
                try
                {
                    Character.FillFrom(_owner, -1, false, true);
                    Character.SetEquipment(
                        EquipmentIndex.ArmorItemEndSlot, default(EquipmentElement));
                    Character.SetEquipment(
                        EquipmentIndex.HorseHarness, default(EquipmentElement));
                    Character.SetEquipment(
                        EquipmentIndex.NumAllWeaponSlots, default(EquipmentElement));
                    CharacterHidden = Character.IsHidden;
                }
                catch
                {
                    Character.IsHidden = true;
                    CharacterHidden = true;
                }
            }
            else
            {
                Character.IsHidden = true;
            }

            ClanBanner = _owner?.Clan?.Banner != null
                ? new BannerImageIdentifierVM(_owner.Clan.Banner, true)
                : null;
            OnPropertyChanged(nameof(HasHolder));
        }
    }
}