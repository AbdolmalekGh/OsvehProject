using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using QuranTracker.Helpers;
using QuranTracker.Models;
using QuranTracker.Services;

namespace QuranTracker.ViewModels
{
    public enum AppTab { Pages, Juzs, Juz30, Khatms, History, Settings }
    public enum OverlayState { None, EditMultiple, EditSingle, Confirm }

    // کلاس کمکی برای نمایش جزءها در جدول
    public class JuzDisplay
    {
        public int JuzNumber { get; set; }
        public int ReadCount { get; set; }
    }

    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class MainViewModel : ObservableObject
    {
        private readonly QuranService _quranService;

        // --- مجموعه‌های اتصال به جداول (DataGrids) ---
        private ObservableCollection<QuranPage> _pages;
        public ObservableCollection<QuranPage> Pages { get => _pages; set { _pages = value; OnPropertyChanged(); } }

        private ObservableCollection<JuzDisplay> _juzs;
        public ObservableCollection<JuzDisplay> Juzs { get => _juzs; set { _juzs = value; OnPropertyChanged(); } }

        private ObservableCollection<QuranSura> _suras;
        public ObservableCollection<QuranSura> Suras { get => _suras; set { _suras = value; OnPropertyChanged(); } }

        private ObservableCollection<KhatmRecord> _khatmRecords;
        public ObservableCollection<KhatmRecord> KhatmRecords { get => _khatmRecords; set { _khatmRecords = value; OnPropertyChanged(); } }

        private ObservableCollection<HistoryLog> _histories;
        public ObservableCollection<HistoryLog> Histories { get => _histories; set { _histories = value; OnPropertyChanged(); } }

        // --- آمارهای کلی ---
        private int _fullKhatmCount;
        public int FullKhatmCount { get => _fullKhatmCount; set { _fullKhatmCount = value; OnPropertyChanged(); } }

        private int _tahlilCount;
        public int TahlilCount { get => _tahlilCount; set { _tahlilCount = value; OnPropertyChanged(); } }


        // --- وضعیت سیستم و تب‌ها ---
        private AppTab _currentTab = AppTab.Pages;
        public AppTab CurrentTab
        {
            get => _currentTab;
            set
            {
                _currentTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentTabIndex));
            }
        }

        public int CurrentTabIndex
        {
            get => (int)_currentTab;
            set
            {
                _currentTab = (AppTab)value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentTab));
            }
        }

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
            }
        }

        public bool IsMainViewEnabled => CurrentOverlay == OverlayState.None;
        public bool IsEditOverlayVisible => CurrentOverlay == OverlayState.EditMultiple || CurrentOverlay == OverlayState.EditSingle;
        public bool IsConfirmOverlayVisible => CurrentOverlay == OverlayState.Confirm;

        // --- فیلدهای فرم ویرایش ---
        private string _editTitle;
        public string EditTitle { get => _editTitle; set { _editTitle = value; OnPropertyChanged(); } }

        private int _startInput;
        public int StartInput { get => _startInput; set { _startInput = value; OnPropertyChanged(); UpdateInfoText(); } }

        private int _endInput;
        public int EndInput { get => _endInput; set { _endInput = value; OnPropertyChanged(); UpdateInfoText(); } }

        private int _addCountInput;
        public int AddCountInput { get => _addCountInput; set { _addCountInput = value; OnPropertyChanged(); UpdateInfoText(); } }

        private int _subCountInput;
        public int SubCountInput { get => _subCountInput; set { _subCountInput = value; OnPropertyChanged(); UpdateInfoText(); } }

        private string _infoText;
        public string InfoText { get => _infoText; set { _infoText = value; OnPropertyChanged(); } }

        // --- Command ها ---
        public ICommand ChangeTabCommand { get; }
        public ICommand OpenEditMultipleCommand { get; }
        public ICommand OpenEditSingleCommand { get; }
        public ICommand EnterKeyCommand { get; }
        public ICommand EscKeyCommand { get; }

        // Command های مربوط به تب ختم‌ها
        public ICommand IncrementTahlilCommand { get; }
        public ICommand DecrementTahlilCommand { get; }
        public ICommand IncrementKhatmCommand { get; }
        public ICommand DecrementKhatmCommand { get; }

        public MainViewModel(QuranService quranService)
        {
            _quranService = quranService ?? throw new ArgumentNullException(nameof(quranService));

            ChangeTabCommand = new RelayCommandWithParameter(ExecuteChangeTab);
            OpenEditMultipleCommand = new RelayCommand(ExecuteOpenEditMultiple);
            OpenEditSingleCommand = new RelayCommand(ExecuteOpenEditSingle);
            EnterKeyCommand = new RelayCommand(ExecuteEnter);
            EscKeyCommand = new RelayCommand(ExecuteEsc);

            IncrementTahlilCommand = new RelayCommand(() => { _quranService.UpdateTahlilCount(1); LoadData(); });
            DecrementTahlilCommand = new RelayCommand(() => { _quranService.UpdateTahlilCount(-1); LoadData(); });

            // اصلاح کلمه instanceof به is
            IncrementKhatmCommand = new RelayCommandWithParameter(param => {
                if (param is KhatmRecord record) { _quranService.UpdateKhatmRecord(record.Id, 1); LoadData(); }
            });
            DecrementKhatmCommand = new RelayCommandWithParameter(param => {
                if (param is KhatmRecord record) { _quranService.UpdateKhatmRecord(record.Id, -1); LoadData(); }
            });

            LoadData();
        }

        private void LoadData()
        {
            Pages = new ObservableCollection<QuranPage>(_quranService.GetPages(SortMode.QuranicOrder));

            var juzList = new ObservableCollection<JuzDisplay>();
            for (int i = 1; i <= 30; i++)
            {
                juzList.Add(new JuzDisplay { JuzNumber = i, ReadCount = _quranService.GetJuzReadCount(i) });
            }
            Juzs = juzList;

            Suras = new ObservableCollection<QuranSura>(_quranService.GetSuras(SortMode.QuranicOrder));
            KhatmRecords = new ObservableCollection<KhatmRecord>(_quranService.GetKhatmRecords());
            Histories = new ObservableCollection<HistoryLog>(_quranService.GetRecentHistory());

            FullKhatmCount = _quranService.CalculateFullKhatmCount();
            TahlilCount = _quranService.GetTahlilCount();
        }

        private void ExecuteChangeTab(object param)
        {
            if (!IsMainViewEnabled) return;
            if (Enum.TryParse(param.ToString(), out AppTab tab))
                CurrentTab = tab;
        }

        private void ExecuteOpenEditMultiple()
        {
            if (!IsMainViewEnabled) return;

            CurrentOverlay = OverlayState.EditMultiple;
            StartInput = 0; EndInput = 0; AddCountInput = 0; SubCountInput = 0;

            if (CurrentTab == AppTab.Pages) EditTitle = "اضافه کردن تکرار به صفحات متوالی";
            else if (CurrentTab == AppTab.Juzs) EditTitle = "اضافه کردن تکرار به جزء‌های متوالی";
            else if (CurrentTab == AppTab.Juz30) EditTitle = "اضافه کردن تکرار به سوره‌ها";
            else EditTitle = "ویرایش متوالی";

            UpdateInfoText();
        }

        private void ExecuteOpenEditSingle()
        {
            if (!IsMainViewEnabled) return;
            CurrentOverlay = OverlayState.EditSingle;
            EditTitle = "جستجو";
        }

        private void ExecuteEnter()
        {
            if (CurrentOverlay == OverlayState.EditMultiple || CurrentOverlay == OverlayState.EditSingle)
                CurrentOverlay = OverlayState.Confirm;
            else if (CurrentOverlay == OverlayState.Confirm)
            {
                ApplyChanges();
                CurrentOverlay = OverlayState.None;
            }
        }

        private void ExecuteEsc() => CurrentOverlay = OverlayState.None;

        private void UpdateInfoText()
        {
            if (AddCountInput > 0) InfoText = $"مقدار {AddCountInput} تکرار به آیتم‌های {StartInput} تا {EndInput} اضافه خواهد شد.";
            else if (SubCountInput > 0) InfoText = $"مقدار {SubCountInput} تکرار از آیتم‌های {StartInput} تا {EndInput} کم خواهد شد.";
            else InfoText = "تعداد تکرار را وارد کنید";
        }

        private void ApplyChanges()
        {
            int delta = AddCountInput > 0 ? AddCountInput : -SubCountInput;
            if (delta == 0) return;

            if (CurrentTab == AppTab.Pages)
                _quranService.UpdatePageRangeReadCount(StartInput, EndInput, delta, "ویرایش گروهی دستی صفحات");
            else if (CurrentTab == AppTab.Juzs)
                _quranService.UpdateJuzReadCount(StartInput, EndInput, delta, "ویرایش گروهی اجزا");
            else if (CurrentTab == AppTab.Juz30)
                _quranService.UpdateSuraRangeReadCount(StartInput, EndInput, delta, "ویرایش گروهی سوره‌ها");

            LoadData();
        }
    }
}