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
        }

        internal Kingdom Realm => _realm;
        internal bool IsDeFacto => _deFacto;

        [DataSourceProperty]
        public MBBindingList<KingdomHierarchyNodeVM> Roots => _roots;

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
            KingdomHierarchyTab.OpenDetails(_selectedTitle, () => RefreshTree(false));
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
        private HeroViewModel _imageIdentifier;
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
            ImageIdentifier = new HeroViewModel(CharacterViewModel.StanceTypes.EmphasizeFace);
            if (owner != null)
            {
                try
                {
                    ImageIdentifier.FillFrom(owner, -1, false, true);
                    ImageIdentifier.SetEquipment(
                        EquipmentIndex.ArmorItemEndSlot, default(EquipmentElement));
                    ImageIdentifier.SetEquipment(
                        EquipmentIndex.HorseHarness, default(EquipmentElement));
                    ImageIdentifier.SetEquipment(
                        EquipmentIndex.NumAllWeaponSlots, default(EquipmentElement));
                    IsHidden = ImageIdentifier.IsHidden;
                }
                catch
                {
                    // Tests and partially initialized campaign UI may not have
                    // CampaignUIHelper state yet. The prefab shows a silhouette.
                    ImageIdentifier.IsHidden = true;
                    IsHidden = true;
                }

                if (owner.Clan?.Banner != null)
                    ClanBanner = new BannerImageIdentifierVM(owner.Clan.Banner, true);
            }
            else
            {
                ImageIdentifier.IsHidden = true;
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
        public HeroViewModel ImageIdentifier
        {
            get => _imageIdentifier;
            private set
            {
                _imageIdentifier = value;
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
            _hierarchy.Select(_title);
        }

        public void ExecuteDetails()
        {
            _hierarchy.Select(_title);
            _hierarchy.ExecuteDetails();
        }
    }
}