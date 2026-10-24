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
            var personService = new PersonService(dbContext);
            _viewModel = new MainViewModel(quranService, personService);

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
            if (_viewModel.IsEditOverlayVisible)
            {
                var focusableElements = GetFocusableOverlayElements();

                if (e.Key == Key.Left || e.Key == Key.Right)
                {
                    if (focusableElements.Count > 0)
                    {
                        var currentFocus = Keyboard.FocusedElement as UIElement;
                        int currentIndex = focusableElements.IndexOf(currentFocus);

                        if (currentIndex != -1)
                        {
                            int nextIndex = e.Key == Key.Left
                                ? (currentIndex + 1) % focusableElements.Count
                                : (currentIndex - 1 + focusableElements.Count) % focusableElements.Count;

                            focusableElements[nextIndex].Focus();
                            if (focusableElements[nextIndex] is TextBox tb) tb.SelectAll();
                            e.Handled = true;
                            return;
                        }
                    }
                }

                if (e.Key == Key.Up || e.Key == Key.Down)
                {
                    if (e.OriginalSource is TextBox txt && focusableElements.Contains(txt))
                    {
                        if (string.IsNullOrWhiteSpace(txt.Text)) txt.Text = "0";
                        if (int.TryParse(txt.Text, out int val))
                        {
                            val += (e.Key == Key.Up) ? 1 : -1;

                            if (_viewModel.CurrentTabIndex == 2 && _viewModel.Juz30CurrentMode == Juz30Mode.Suras && (txt.Name == "TxtStartInput" || txt.Name == "TxtEndInput"))
                            {
                                if (val > 114) val = 78;
                                if (val < 78) val = 114;
                            }
                            else if (_viewModel.CurrentTabIndex == 2 && _viewModel.Juz30CurrentMode == Juz30Mode.Pages && (txt.Name == "TxtStartInput" || txt.Name == "TxtEndInput"))
                            {
                                if (val > 604) val = 582;
                                if (val < 582) val = 604;
                            }
                            else
                            {
                                if (val < 0) val = 0;
                            }

                            txt.Text = val.ToString();
                            txt.CaretIndex = txt.Text.Length;
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }

            if (_viewModel.IsMainViewEnabled && _viewModel.CurrentTabIndex == 3)
            {
                int inputNum = -1;
                if (e.Key >= Key.D1 && e.Key <= Key.D9) inputNum = e.Key - Key.D1 + 1;
                else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9) inputNum = e.Key - Key.NumPad1 + 1;

                if (inputNum != -1)
                {
                    _viewModel.OpenEditSingleCommand.Execute(inputNum);
                    e.Handled = true;
                    return;
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

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open"
            });
        }

        private void ImportJson_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "انتخاب فایل ذخیره قبلی (JSON)",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                string resultMessage = _viewModel._quranService.ImportOldDataFromJson(dlg.FileName);
                MessageBox.Show(resultMessage, "نتیجه بازیابی اطلاعات", MessageBoxButton.OK, MessageBoxImage.Information);
                _viewModel.LoadData();
            }
        }
    }
}