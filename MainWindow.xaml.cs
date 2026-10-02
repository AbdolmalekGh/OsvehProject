using System.Windows;
using QuranTracker.Models;
using QuranTracker.Services;
using QuranTracker.ViewModels;

namespace QuranTracker
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // تزریق وابستگی‌ها در زمان اجرا
            var dbContext = new AppDbContext();
            var quranService = new QuranService(dbContext);
            var viewModel = new MainViewModel(quranService);

            // اتصال DataContext برای بایندینگ شدن اطلاعات به UI
            this.DataContext = viewModel;
        }
    }
}