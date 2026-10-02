using QuranTracker.Models;
using QuranTracker.Services;
using QuranTracker.ViewModels;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QuranTracker
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            var dbContext = new AppDbContext();
            var quranService = new QuranService(dbContext);
            _viewModel = new MainViewModel(quranService);

            this.DataContext = _viewModel;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsEditOverlayVisible) && _viewModel.IsEditOverlayVisible)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    TxtStartInput.Focus();
                    TxtStartInput.SelectAll();
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            // مدیریت حرکت چرخشی بین فیلدها فقط با کلیدهای چپ و راست (Left/Right Arrow Keys)
            if (_viewModel.IsEditOverlayVisible)
            {
                if (e.Key == Key.Left || e.Key == Key.Right)
                {
                    var focusableElements = GetFocusableOverlayElements();
                    if (focusableElements.Count > 0)
                    {
                        var currentFocus = Keyboard.FocusedElement as UIElement;
                        int currentIndex = focusableElements.IndexOf(currentFocus);

                        if (currentIndex != -1)
                        {
                            int nextIndex;
                            if (e.Key == Key.Left) // رفتن به فیلد بعدی (چپ در layout راست‌به‌چپ)
                            {
                                nextIndex = (currentIndex + 1) % focusableElements.Count;
                            }
                            else // رفتن به فیلد قبلی
                            {
                                nextIndex = (currentIndex - 1 + focusableElements.Count) % focusableElements.Count;
                            }

                            focusableElements[nextIndex].Focus();
                            if (focusableElements[nextIndex] is TextBox tb) tb.SelectAll();
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }

            base.OnPreviewKeyDown(e);
        }

        private List<UIElement> GetFocusableOverlayElements()
        {
            var list = new List<UIElement>();
            if (TxtStartInput.IsVisible) list.Add(TxtStartInput);
            if (TxtEndInput.IsVisible) list.Add(TxtEndInput);
            if (TxtAddInput.IsVisible) list.Add(TxtAddInput);
            if (TxtSubInput.IsVisible) list.Add(TxtSubInput);
            return list;
        }

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            if (_viewModel.IsEditOverlayVisible && e.Source is TextBox)
            {
                Regex regex = new Regex("[^0-9]+");
                if (regex.IsMatch(e.Text))
                {
                    e.Handled = true;
                    return;
                }
            }
            base.OnPreviewTextInput(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_viewModel.IsMainViewEnabled && _viewModel.CurrentTabIndex == 3) // تب ختم‌ها
            {
                if (KhatmsGrid.SelectedItem is KhatmRecord selectedKhatm)
                {
                    if (e.Key == Key.Add || e.Key == Key.OemPlus)
                    {
                        _viewModel.QuickChangeKhatmCommand.Execute(new Tuple<KhatmRecord, int>(selectedKhatm, 1));
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
                    {
                        _viewModel.QuickChangeKhatmCommand.Execute(new Tuple<KhatmRecord, int>(selectedKhatm, -1));
                        e.Handled = true;
                    }
                }
            }

            base.OnKeyDown(e);
        }
    }
}