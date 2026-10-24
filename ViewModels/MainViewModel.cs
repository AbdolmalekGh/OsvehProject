using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using QuranTracker.Helpers;
using QuranTracker.Models;
using QuranTracker.Services;

namespace QuranTracker.ViewModels
{
    public enum AppTab { Pages, Juzs, Juz30, Khatms, History, Settings }
    public enum OverlayState { None, Edit, Confirm }
    public enum EditMode { Single, Multiple }
    public enum Juz30Mode { Suras, Pages }
    public enum SortType { QuranicOrder, AscendingReadCount, DescendingReadCount }

    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class JuzDisplay
    {
        public int JuzNumber { get; set; }
        public int MinCount { get; set; }
        public int MaxCount { get; set; }
    }

    public class SortOption
    {
        public SortType Type { get; set; }
        public string Title { get; set; }
    }

    public class MainViewModel : ObservableObject
    {
        public readonly QuranService _quranService;

        public ObservableCollection<QuranPage> Pages { get; set; }
        public ObservableCollection<JuzDisplay> Juzs { get; set; }
        public ObservableCollection<QuranPage> Juz30Pages { get; set; }
        public ObservableCollection<QuranSura> Suras { get; set; }
        public ObservableCollection<KhatmRecord> KhatmRecords { get; set; }
        public ObservableCollection<HistoryLog> Histories { get; set; }

        public List<SortOption> SortOptions { get; } = new List<SortOption>
        {
            new SortOption { Type = SortType.QuranicOrder, Title = "ترتیب قرآنی" },
            new SortOption { Type = SortType.AscendingReadCount, Title = "کمترین تکرار" },
            new SortOption { Type = SortType.DescendingReadCount, Title = "بیشترین تکرار" }
        };

        private AppTab _currentTab = AppTab.Pages;
        public int CurrentTabIndex
        {
            get => (int)_currentTab;
            set
            {
                _currentTab = (AppTab)value;
                OnPropertyChanged();
                UpdateJuz30Visibilities();
                OnPropertyChanged(nameof(CurrentTabName));
                OnPropertyChanged(nameof(IsSuraDropdownVisible));
            }
        }

        public string CurrentTabName => _currentTab switch
        {
            AppTab.Pages => "صفحات قرآن",
            AppTab.Juzs => "جزء‌های قرآن",
            AppTab.Juz30 => "جزء سی‌ام",
            AppTab.Khatms => "ختم‌ها و اذکار",
            AppTab.History => "تاریخچه تغییرات",
            AppTab.Settings => "تنظیمات",
            _ => ""
        };

        private SortOption _selectedSortOption;
        public SortOption SelectedSortOption
        {
            get => _selectedSortOption;
            set { _selectedSortOption = value; OnPropertyChanged(); ApplySorting(); }
        }

        private Juz30Mode _juz30CurrentMode = Juz30Mode.Suras;
        public Juz30Mode Juz30CurrentMode
        {
            get => _juz30CurrentMode;
            set { _juz30CurrentMode = value; OnPropertyChanged(); UpdateJuz30Visibilities(); OnPropertyChanged(nameof(IsSuraDropdownVisible)); }
        }

        private bool _isJuz30SurasVisible = true;
        public bool IsJuz30SurasVisible { get => _isJuz30SurasVisible; set { _isJuz30SurasVisible = value; OnPropertyChanged(); } }

        private bool _isJuz30PagesVisible = false;
        public bool IsJuz30PagesVisible { get => _isJuz30PagesVisible; set { _isJuz30PagesVisible = value; OnPropertyChanged(); } }

        public bool IsSuraDropdownVisible => (_currentTab == AppTab.Juz30) && (_juz30CurrentMode == Juz30Mode.Suras);

        private OverlayState _currentOverlay = OverlayState.None;
        public OverlayState CurrentOverlay
        {
            get => _currentOverlay;
            set
            {
                _currentOverlay = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMainViewEnabled));
                OnPropertyChanged(nameof(IsEditOverlayVisible));
                OnPropertyChanged(nameof(IsConfirmOverlayVisible));
                if (value != OverlayState.Edit) IsHelpListVisible = false;
            }
        }

        public bool IsMainViewEnabled => CurrentOverlay == OverlayState.None;
        public bool IsEditOverlayVisible => CurrentOverlay == OverlayState.Edit;
        public bool IsConfirmOverlayVisible => CurrentOverlay == OverlayState.Confirm;

        private bool _isHelpListVisible;
        public bool IsHelpListVisible { get => _isHelpListVisible; set { _isHelpListVisible = value; OnPropertyChanged(); } }

        private string _helpListTitle;
        public string HelpListTitle { get => _helpListTitle; set { _helpListTitle = value; OnPropertyChanged(); } }

        private ObservableCollection<object> _helpListItems;
        public ObservableCollection<object> HelpListItems { get => _helpListItems; set { _helpListItems = value; OnPropertyChanged(); } }

        private EditMode _currentEditMode = EditMode.Single;
        public EditMode CurrentEditMode
        {
            get => _currentEditMode;
            set { _currentEditMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsMultipleMode)); }
        }

        public bool IsMultipleMode => CurrentEditMode == EditMode.Multiple;

        private QuranSura _selectedStartSura;
        public QuranSura SelectedStartSura
        {
            get => _selectedStartSura;
            set { _selectedStartSura = value; OnPropertyChanged(); if (value != null && IsSuraDropdownVisible) StartInput = value.SuraNumber.ToString(); }
        }

        private QuranSura _selectedEndSura;
        public QuranSura SelectedEndSura
        {
            get => _selectedEndSura;
            set { _selectedEndSura = value; OnPropertyChanged(); if (value != null && IsSuraDropdownVisible) EndInput = value.SuraNumber.ToString(); }
        }

        private string _editTitle;
        public string EditTitle { get => _editTitle; set { _editTitle = value; OnPropertyChanged(); } }

        private string _startInput = "";
        public string StartInput
        {
            get => _startInput;
            set { _startInput = value; OnPropertyChanged(); SyncSuraFromInput(value, true); UpdateCurrentItemCount(); }
        }

        private string _endInput = "";
        public string EndInput
        {
            get => _endInput;
            set { _endInput = value; OnPropertyChanged(); SyncSuraFromInput(value, false); }
        }

        private void SyncSuraFromInput(string input, bool isStart)
        {
            if (IsSuraDropdownVisible && int.TryParse(input, out int suraNum))
            {
                var sura = Suras?.FirstOrDefault(s => s.SuraNumber == suraNum);
                if (sura != null)
                {
                    if (isStart) _selectedStartSura = sura;
                    else _selectedEndSura = sura;
                    OnPropertyChanged(isStart ? nameof(SelectedStartSura) : nameof(SelectedEndSura));
                }
            }
        }

        private void UpdateCurrentItemCount()
        {
            if (int.TryParse(StartInput, out int id))
                CurrentItemCount = _quranService.GetItemCurrentCount(_currentTab, Juz30CurrentMode, id);
            else
                CurrentItemCount = 0;
        }

        private string _addCountInput = "";
        public string AddCountInput { get => _addCountInput; set { _addCountInput = value; OnPropertyChanged(); } }

        private string _subCountInput = "";
        public string SubCountInput { get => _subCountInput; set { _subCountInput = value; OnPropertyChanged(); } }

        private int _currentItemCount;
        public int CurrentItemCount { get => _currentItemCount; set { _currentItemCount = value; OnPropertyChanged(); } }

        private string _errorMessage = "";
        public string ErrorMessage { get => _errorMessage; set { _errorMessage = value; OnPropertyChanged(); } }

        private string _confirmationMessage = "";
        public string ConfirmationMessage { get => _confirmationMessage; set { _confirmationMessage = value; OnPropertyChanged(); } }

        private int _fullQuranKhatmCount;
        public int FullQuranKhatmCount { get => _fullQuranKhatmCount; set { _fullQuranKhatmCount = value; OnPropertyChanged(); } }

        public RelayCommand F1KeyCommand { get; }
        public RelayCommandWithParameter ChangeTabCommand { get; }
        public RelayCommand OpenEditMultipleCommand { get; }
        public RelayCommandWithParameter OpenEditSingleCommand { get; }
        public RelayCommand ToggleJuz30ModeCommand { get; }
        public RelayCommand CycleSortCommand { get; }
        public RelayCommand EnterKeyCommand { get; }
        public RelayCommand EscKeyCommand { get; }
        public RelayCommandWithParameter EditSelectedItemCommand { get; }

        public MainViewModel(QuranService quranService)
        {
            _quranService = quranService ?? throw new ArgumentNullException(nameof(quranService));
            SelectedSortOption = SortOptions[0];

            ChangeTabCommand = new RelayCommandWithParameter(p => { if (IsMainViewEnabled) CurrentTabIndex = int.Parse(p.ToString()); });

            OpenEditMultipleCommand = new RelayCommand(() => OpenEdit(EditMode.Multiple));
            OpenEditSingleCommand = new RelayCommandWithParameter(param => OpenEdit(EditMode.Single, param != null && int.TryParse(param.ToString(), out int id) ? id : null));

            F1KeyCommand = new RelayCommand(() =>
            {
                if (CurrentOverlay == OverlayState.Edit)
                {
                    IsHelpListVisible = !IsHelpListVisible;
                    if (IsHelpListVisible)
                    {
                        if (_currentTab == AppTab.Juz30)
                        {
                            HelpListTitle = "لیست سوره‌های جزء ۳۰";
                            HelpListItems = new ObservableCollection<object>(Suras.Select(s => new { کد = s.SuraNumber, نام = s.Name }));
                        }
                        else if (_currentTab == AppTab.Khatms)
                        {
                            HelpListTitle = "لیست شناسه‌های ختم";
                            HelpListItems = new ObservableCollection<object>(KhatmRecords.Select(k => new { شناسه = k.Id, نام = k.Name }));
                        }
                        else
                        {
                            HelpListTitle = "راهنما برای این بخش وجود ندارد";
                            HelpListItems = new ObservableCollection<object>();
                        }
                    }
                }
                else if (IsMainViewEnabled)
                {
                    CurrentTabIndex = 0;
                }
            });

            ToggleJuz30ModeCommand = new RelayCommand(() =>
            {
                if (_currentTab == AppTab.Juz30) Juz30CurrentMode = Juz30CurrentMode == Juz30Mode.Suras ? Juz30Mode.Pages : Juz30Mode.Suras;
            });

            CycleSortCommand = new RelayCommand(() =>
            {
                int nextIndex = (SortOptions.IndexOf(SelectedSortOption) + 1) % SortOptions.Count;
                SelectedSortOption = SortOptions[nextIndex];
            });

            EnterKeyCommand = new RelayCommand(ExecuteEnter);
            EscKeyCommand = new RelayCommand(ExecuteEsc);
            EditSelectedItemCommand = new RelayCommandWithParameter(ExecuteEditSelected);

            LoadData();
        }

        private void UpdateJuz30Visibilities()
        {
            IsJuz30SurasVisible = (_currentTab == AppTab.Juz30) && (_juz30CurrentMode == Juz30Mode.Suras);
            IsJuz30PagesVisible = (_currentTab == AppTab.Juz30) && (_juz30CurrentMode == Juz30Mode.Pages);
        }

        public void LoadData()
        {
            Pages = new ObservableCollection<QuranPage>(_quranService.GetPages());
            Juz30Pages = new ObservableCollection<QuranPage>(_quranService.GetJuz30Pages());
            Suras = new ObservableCollection<QuranSura>(_quranService.GetSuras());
            KhatmRecords = new ObservableCollection<KhatmRecord>(_quranService.GetKhatmRecords());
            Histories = new ObservableCollection<HistoryLog>(_quranService.GetHistory());

            var juzList = new ObservableCollection<JuzDisplay>();
            for (int i = 1; i <= 30; i++)
            {
                var stats = _quranService.GetJuzReadStats(i);
                juzList.Add(new JuzDisplay { JuzNumber = i, MinCount = stats.Min, MaxCount = stats.Max });
            }
            Juzs = juzList;

            FullQuranKhatmCount = Pages.Any() ? Pages.Min(p => p.ReadCount) : 0;
            ApplySorting();
            OnPropertyChanged(string.Empty);
        }

        private void ApplySorting()
        {
            if (Pages == null || SelectedSortOption == null) return;

            switch (SelectedSortOption.Type)
            {
                case SortType.QuranicOrder:
                    Pages = new ObservableCollection<QuranPage>(Pages.OrderBy(p => p.PageNumber));
                    Suras = new ObservableCollection<QuranSura>(Suras.OrderBy(s => s.SuraNumber));
                    Juzs = new ObservableCollection<JuzDisplay>(Juzs.OrderBy(j => j.JuzNumber));
                    break;
                case SortType.AscendingReadCount:
                    Pages = new ObservableCollection<QuranPage>(Pages.OrderBy(p => p.ReadCount));
                    Suras = new ObservableCollection<QuranSura>(Suras.OrderBy(s => s.ReadCount));
                    Juzs = new ObservableCollection<JuzDisplay>(Juzs.OrderBy(j => j.MinCount));
                    break;
                case SortType.DescendingReadCount:
                    Pages = new ObservableCollection<QuranPage>(Pages.OrderByDescending(p => p.ReadCount));
                    Suras = new ObservableCollection<QuranSura>(Suras.OrderByDescending(s => s.ReadCount));
                    Juzs = new ObservableCollection<JuzDisplay>(Juzs.OrderByDescending(j => j.MinCount));
                    break;
            }
            OnPropertyChanged(nameof(Pages));
            OnPropertyChanged(nameof(Suras));
            OnPropertyChanged(nameof(Juzs));
        }

        private void OpenEdit(EditMode mode, int? prefillId = null)
        {
            if (!IsMainViewEnabled || _currentTab == AppTab.History || _currentTab == AppTab.Settings) return;

            CurrentEditMode = mode;
            StartInput = prefillId?.ToString() ?? "";
            EndInput = ""; AddCountInput = ""; SubCountInput = ""; ErrorMessage = "";

            if (IsSuraDropdownVisible && prefillId.HasValue)
                SelectedStartSura = Suras?.FirstOrDefault(s => s.SuraNumber == prefillId.Value);

            EditTitle = mode == EditMode.Multiple ? $"ویرایش متوالی ({CurrentTabName})" : $"ویرایش تکی ({CurrentTabName})";
            CurrentOverlay = OverlayState.Edit;
            UpdateCurrentItemCount();
        }

        private void ExecuteEditSelected(object param)
        {
            if (param is QuranPage p) OpenEdit(EditMode.Single, p.PageNumber);
            else if (param is JuzDisplay j) OpenEdit(EditMode.Single, j.JuzNumber);
            else if (param is QuranSura s) OpenEdit(EditMode.Single, s.SuraNumber);
            else if (param is KhatmRecord k) OpenEdit(EditMode.Single, k.Id);
            else if (param is int id) OpenEdit(EditMode.Single, id);
        }

        private string GetTargetDescription(int start, int end)
        {
            if (CurrentEditMode == EditMode.Multiple)
            {
                return _currentTab switch
                {
                    AppTab.Pages => $"صفحات {start} تا {end}",
                    AppTab.Juzs => $"جزءهای {start} تا {end}",
                    AppTab.Juz30 => _juz30CurrentMode == Juz30Mode.Suras ? $"سوره‌های {start} تا {end}" : $"صفحات {start} تا {end}",
                    AppTab.Khatms => $"ختم‌های {start} تا {end}",
                    _ => $"از کد {start} تا {end}"
                };
            }
            else
            {
                return _currentTab switch
                {
                    AppTab.Pages => $"صفحه {start}",
                    AppTab.Juzs => $"جزء {start}",
                    AppTab.Juz30 => _juz30CurrentMode == Juz30Mode.Suras
                        ? $"سوره {Suras?.FirstOrDefault(s => s.SuraNumber == start)?.Name ?? start.ToString()} (کد {start})"
                        : $"صفحه {start}",
                    AppTab.Khatms => $"ختم {KhatmRecords?.FirstOrDefault(k => k.Id == start)?.Name ?? start.ToString()} (شناسه {start})",
                    _ => $"شناسه {start}"
                };
            }
        }

        private void ExecuteEnter()
        {
            if (CurrentOverlay == OverlayState.Edit)
            {
                if (ValidateInputs(out int start, out int end, out int delta))
                {
                    string targetDesc = GetTargetDescription(start, end);
                    string action = delta > 0 ? $"افزایش {delta} تکرار" : $"کاهش {Math.Abs(delta)} تکرار";

                    ConfirmationMessage = $"آیا تغییر زیر را تایید می‌کنید؟\n\n{action} برای {targetDesc}";
                    ErrorMessage = "";
                    CurrentOverlay = OverlayState.Confirm;
                }
            }
            else if (CurrentOverlay == OverlayState.Confirm)
            {
                if (ApplyChanges())
                {
                    CurrentOverlay = OverlayState.None;
                }
                else
                {
                    CurrentOverlay = OverlayState.Edit;
                    ErrorMessage = "خطا: مقدار تکرار نمی‌تواند منفی شود.";
                }
            }
        }

        private void ExecuteEsc() => CurrentOverlay = OverlayState.None;

        private bool ValidateInputs(out int start, out int end, out int delta)
        {
            start = 0; end = 0; delta = 0;

            if (!int.TryParse(StartInput, out start) || start <= 0)
            {
                ErrorMessage = "مقدار شماره ورودی (از) نامعتبر است.";
                return false;
            }

            if (CurrentEditMode == EditMode.Multiple)
            {
                if (!int.TryParse(EndInput, out end) || end < start)
                {
                    ErrorMessage = "مقدار 'تا شماره' باید بزرگتر یا مساوی 'از شماره' باشد.";
                    return false;
                }
            }
            else { end = start; }

            int add = 0, sub = 0;
            if (!string.IsNullOrEmpty(AddCountInput)) int.TryParse(AddCountInput, out add);
            if (!string.IsNullOrEmpty(SubCountInput)) int.TryParse(SubCountInput, out sub);

            delta = add - sub;
            if (delta == 0)
            {
                ErrorMessage = "لطفاً تعداد اضافه یا کم کردن تکرار را وارد کنید (برآیند صفر است).";
                return false;
            }

            return true;
        }

        private bool ApplyChanges()
        {
            ValidateInputs(out int start, out int end, out int delta);
            bool success = true;

            switch (_currentTab)
            {
                case AppTab.Pages:
                    success = _quranService.UpdatePageRange(start, end, delta, "ویرایش صفحات");
                    break;
                case AppTab.Juzs:
                    success = _quranService.UpdateJuzRange(start, end, delta, "ویرایش اجزا");
                    break;
                case AppTab.Juz30:
                    if (_juz30CurrentMode == Juz30Mode.Suras)
                        success = _quranService.UpdateSuraRange(start, end, delta, "ویرایش سوره‌های جزء ۳۰");
                    else
                        success = _quranService.UpdatePageRange(start, end, delta, "ویرایش صفحات جزء ۳۰");
                    break;
                case AppTab.Khatms:
                    if (CurrentEditMode == EditMode.Multiple)
                        success = _quranService.UpdateKhatmRange(start, end, delta, "ویرایش متوالی ختم‌ها");
                    else
                        success = _quranService.UpdateKhatmRange(start, start, delta, "ویرایش ختم");
                    break;
            }

            if (success)
            {
                LoadData();
            }
            return success;
        }
    }
}