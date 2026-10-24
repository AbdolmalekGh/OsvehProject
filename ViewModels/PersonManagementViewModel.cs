using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using QuranTracker.Helpers;
using QuranTracker.Models;
using QuranTracker.Services;

namespace QuranTracker.ViewModels
{
    public class PersonManagementViewModel : ObservableObject
    {
        private readonly PersonService _personService;

        public ObservableCollection<Person> Persons { get; set; } = new();

        private Person _selectedPerson;
        public Person SelectedPerson
        {
            get => _selectedPerson;
            set
            {
                _selectedPerson = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedPerson));
            }
        }

        public bool HasSelectedPerson => SelectedPerson != null;

        private string _searchNameQuery = "";
        public string SearchNameQuery
        {
            get => _searchNameQuery;
            set { _searchNameQuery = value; OnPropertyChanged(); PerformSearch(); }
        }

        private string _searchNationalCodeQuery = "";
        public string SearchNationalCodeQuery
        {
            get => _searchNationalCodeQuery;
            set { _searchNationalCodeQuery = value; OnPropertyChanged(); PerformSearch(); }
        }

        private int _detailActiveTab = 0;
        public int DetailActiveTab
        {
            get => _detailActiveTab;
            set { _detailActiveTab = value; OnPropertyChanged(); }
        }

        private string _newPhoneTitle = "همراه";
        public string NewPhoneTitle { get => _newPhoneTitle; set { _newPhoneTitle = value; OnPropertyChanged(); } }

        private string _newPhoneNumber = "";
        public string NewPhoneNumber { get => _newPhoneNumber; set { _newPhoneNumber = value; OnPropertyChanged(); } }

        public RelayCommand SearchCommand { get; }
        public RelayCommand AddNewPersonCommand { get; }
        public RelayCommand SavePersonCommand { get; }
        public RelayCommand DeletePersonCommand { get; }
        public RelayCommand AddPhoneCommand { get; }
        public RelayCommandWithParameter DeletePhoneCommand { get; }
        public RelayCommand AddAttachmentCommand { get; }
        public RelayCommandWithParameter DeleteAttachmentCommand { get; }
        public RelayCommandWithParameter OpenAttachmentCommand { get; }
        public RelayCommand ChangeProfileImageCommand { get; }
        public RelayCommandWithParameter ChangeDetailTabCommand { get; }

        public PersonManagementViewModel(PersonService personService)
        {
            _personService = personService;

            SearchCommand = new RelayCommand(PerformSearch);
            AddNewPersonCommand = new RelayCommand(CreateNewPerson);
            SavePersonCommand = new RelayCommand(SaveCurrentPerson);
            DeletePersonCommand = new RelayCommand(DeleteCurrentPerson);

            AddPhoneCommand = new RelayCommand(AddPhoneNumber);
            DeletePhoneCommand = new RelayCommandWithParameter(param => DeletePhoneNumber(param as PersonPhone));

            AddAttachmentCommand = new RelayCommand(AddAttachment);
            DeleteAttachmentCommand = new RelayCommandWithParameter(param => DeleteAttachment(param as PersonAttachment));
            OpenAttachmentCommand = new RelayCommandWithParameter(param => OpenAttachment(param as PersonAttachment));
            ChangeProfileImageCommand = new RelayCommand(ChangeProfileImage);

            ChangeDetailTabCommand = new RelayCommandWithParameter(param =>
            {
                if (param != null && int.TryParse(param.ToString(), out int tabIndex))
                {
                    DetailActiveTab = tabIndex;
                }
            });

            PerformSearch();
        }

        public void PerformSearch()
        {
            var results = _personService.SearchPersons(SearchNameQuery, SearchNationalCodeQuery);
            Persons.Clear();
            foreach (var p in results) Persons.Add(p);

            if (SelectedPerson == null && Persons.Any())
            {
                SelectedPerson = Persons.First();
            }
            else if (SelectedPerson != null && !Persons.Contains(SelectedPerson))
            {
                SelectedPerson = Persons.FirstOrDefault();
            }
        }

        private void CreateNewPerson()
        {
            var newPerson = new Person
            {
                FirstName = "عضو",
                LastName = "جدید",
                Gender = "مرد"
            };
            _personService.SavePerson(newPerson);
            PerformSearch();
            SelectedPerson = Persons.FirstOrDefault(p => p.Id == newPerson.Id);
        }

        private void SaveCurrentPerson()
        {
            if (SelectedPerson != null)
            {
                _personService.SavePerson(SelectedPerson);
                int currentId = SelectedPerson.Id;
                PerformSearch();
                SelectedPerson = Persons.FirstOrDefault(p => p.Id == currentId);
            }
        }

        private void DeleteCurrentPerson()
        {
            if (SelectedPerson != null)
            {
                var result = MessageBox.Show($"آیا از حذف کامل پرونده '{SelectedPerson.FullName}' اطمینان دارید؟", "تایید حذف", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    _personService.DeletePerson(SelectedPerson.Id);
                    SelectedPerson = null;
                    PerformSearch();
                }
            }
        }

        private void AddPhoneNumber()
        {
            if (SelectedPerson != null && !string.IsNullOrWhiteSpace(NewPhoneNumber))
            {
                var newPhone = new PersonPhone
                {
                    PersonId = SelectedPerson.Id,
                    Title = string.IsNullOrWhiteSpace(NewPhoneTitle) ? "همراه" : NewPhoneTitle,
                    PhoneNumber = NewPhoneNumber.Trim()
                };

                SelectedPerson.PhoneNumbers.Add(newPhone);
                NewPhoneNumber = "";
                _personService.SavePerson(SelectedPerson);

                RefreshSelectedPersonView();
            }
        }

        private void DeletePhoneNumber(PersonPhone phone)
        {
            if (SelectedPerson != null && phone != null)
            {
                SelectedPerson.PhoneNumbers.Remove(phone);
                _personService.SavePerson(SelectedPerson);

                RefreshSelectedPersonView();
            }
        }

        private void ChangeProfileImage()
        {
            if (SelectedPerson == null) return;

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "انتخاب تصویر پروفایل",
                Filter = "Image Files (*.jpg;*.png;*.jpeg)|*.jpg;*.png;*.jpeg"
            };

            if (dlg.ShowDialog() == true)
            {
                string path = _personService.SaveProfileImage(SelectedPerson.Id, dlg.FileName);
                if (!string.IsNullOrEmpty(path))
                {
                    SelectedPerson.ProfileImagePath = path;
                    _personService.SavePerson(SelectedPerson);

                    RefreshSelectedPersonView();
                }
            }
        }

        private void AddAttachment()
        {
            if (SelectedPerson == null) return;

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "انتخاب فایل ضمیمه مدرک",
                Filter = "All Files (*.*)|*.*|Images (*.jpg;*.png)|*.jpg;*.png|PDF Documents (*.pdf)|*.pdf"
            };

            if (dlg.ShowDialog() == true)
            {
                var attachment = _personService.AddAttachmentToPerson(SelectedPerson.Id, dlg.FileName);
                if (!SelectedPerson.Attachments.Any(a => a.Id == attachment.Id))
                {
                    SelectedPerson.Attachments.Add(attachment);
                }

                RefreshSelectedPersonView();
            }
        }

        private void DeleteAttachment(PersonAttachment att)
        {
            if (SelectedPerson != null && att != null)
            {
                _personService.DeleteAttachment(att.Id);
                SelectedPerson.Attachments.Remove(att);

                RefreshSelectedPersonView();
            }
        }

        private void OpenAttachment(PersonAttachment att)
        {
            if (att != null && !string.IsNullOrEmpty(att.FilePath))
            {
                string targetPath = att.FilePath;
                if (!File.Exists(targetPath))
                {
                    string localFallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, att.FilePath);
                    if (File.Exists(localFallback)) targetPath = localFallback;
                }

                if (File.Exists(targetPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = targetPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("فایل مورد نظر در مسیر ذخیره شده یافت نشد.", "خطا در باز کردن فایل", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RefreshSelectedPersonView()
        {
            int currentId = SelectedPerson?.Id ?? 0;
            PerformSearch();
            if (currentId > 0)
            {
                SelectedPerson = Persons.FirstOrDefault(p => p.Id == currentId);
            }
        }
    }
}