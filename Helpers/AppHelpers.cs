using System;
using System.Globalization;
using System.Windows.Input;

namespace QuranTracker.Helpers
{
    public static class JuzPageMapper
    {
        public static (int StartPage, int EndPage) GetPageRangeForJuz(int juzNumber)
        {
            if (juzNumber < 1 || juzNumber > 30)
                throw new ArgumentOutOfRangeException(nameof(juzNumber), "Juz must be between 1 and 30.");

            if (juzNumber == 1) return (1, 21);
            if (juzNumber == 30) return (582, 604);

            int start = 22 + (juzNumber - 2) * 20;
            int end = start + 19;
            return (start, end);
        }
    }

    public static class PersianDateHelper
    {
        private static readonly PersianCalendar Calendar = new();

        public static string GetCurrentPersianDateTime()
        {
            DateTime now = DateTime.Now;
            int year = Calendar.GetYear(now);
            int month = Calendar.GetMonth(now);
            int day = Calendar.GetDayOfMonth(now);
            int hour = Calendar.GetHour(now);
            int minute = Calendar.GetMinute(now);

            return $"{year}/{month:D2}/{day:D2} - {hour:D2}:{minute:D2}";
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
        public void Execute(object parameter) => _execute();

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class RelayCommandWithParameter : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommandWithParameter(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}