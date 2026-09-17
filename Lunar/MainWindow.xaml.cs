using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Explorf
{
    public class FolderModel : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged("IsSelected");
                }
            }
        }

        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CalendarDayEntry
    {
        public string Note { get; set; } = string.Empty;
        public string ColorHex { get; set; } = string.Empty;
    }

    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _clockTimer;
        private TimeSpan _timeRemaining;
        private bool _isTimerRunning = false;

        private readonly string _foldersFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "folders.txt");
        private readonly string _weekdayNotesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "weekday_notes.json");
        private readonly string _calendarNotesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "calendar_notes.json");

        private Dictionary<string, CalendarDayEntry> _calendarData = new Dictionary<string, CalendarDayEntry>();
        private DateTime _currentCalendarMonth = DateTime.Now;
        private DateTime _selectedDate = DateTime.Now;

        private Dictionary<string, string> _weekdayNotes = new Dictionary<string, string>();
        private string _activeWeekday = "Понедельник";
        private bool _isLoadingNotes = false;

        public ObservableCollection<FolderModel> FolderList { get; set; } = new ObservableCollection<FolderModel>();

        public MainWindow()
        {
            InitializeComponent();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();

            FolderListView.ItemsSource = FolderList;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateRealTimeClock();
            LoadFoldersFromFile();
            LoadWeekdayNotes();
            LoadCalendarNotes();
            RenderCalendar();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        #region Real-Time Clock
        private void ClockTimer_Tick(object sender, EventArgs e) => UpdateRealTimeClock();

        private void UpdateRealTimeClock()
        {
            DateTime now = DateTime.Now;
            TxtClockTime.Text = now.ToString("HH:mm:ss");

            string[] monthNames = {
                "января", "февраля", "марта", "апреля", "мая", "июня",
                "июля", "августа", "сентября", "октября", "ноября", "декабря"
            };

            TxtClockDate.Text = string.Format("{0}, {1} {2} {3} г.",
                now.ToString("dddd"), now.Day, monthNames[now.Month - 1], now.Year);
        }
        #endregion

        #region Weekday Notes Persistence
        private void LoadWeekdayNotes()
        {
            _isLoadingNotes = true;

            string[] days = { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота", "Воскресенье" };
            foreach (var day in days)
            {
                _weekdayNotes[day] = string.Empty;
            }

            try
            {
                if (File.Exists(_weekdayNotesPath))
                {
                    string json = File.ReadAllText(_weekdayNotesPath);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (loaded != null)
                    {
                        foreach (var kvp in loaded)
                        {
                            _weekdayNotes[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch { }

            _isLoadingNotes = false;
            SelectWeekday("Понедельник");
        }

        private void SaveWeekdayNotes()
        {
            if (_isLoadingNotes) return;

            try
            {
                string json = JsonSerializer.Serialize(_weekdayNotes, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_weekdayNotesPath, json);
            }
            catch { }
        }

        private void DayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                SelectWeekday(btn.Tag.ToString());
            }
        }

        private void SelectWeekday(string dayName)
        {
            _activeWeekday = dayName;
            TxtWeekdayTitle.Text = "ЗАМЕТКИ: " + dayName.ToUpper();

            _isLoadingNotes = true;
            if (_weekdayNotes.ContainsKey(dayName))
                TxtWeekdayNote.Text = _weekdayNotes[dayName];
            else
                TxtWeekdayNote.Text = string.Empty;
            _isLoadingNotes = false;
        }

        private void TxtWeekdayNote_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingNotes) return;

            if (!string.IsNullOrEmpty(_activeWeekday))
            {
                _weekdayNotes[_activeWeekday] = TxtWeekdayNote.Text;
                SaveWeekdayNotes();
            }
        }
        #endregion

        #region Calendar Notes Persistence
        private void LoadCalendarNotes()
        {
            try
            {
                if (File.Exists(_calendarNotesPath))
                {
                    string json = File.ReadAllText(_calendarNotesPath);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, CalendarDayEntry>>(json);
                    if (loaded != null)
                    {
                        _calendarData = loaded;
                    }
                }
            }
            catch { }
        }

        private void SaveCalendarNotes()
        {
            try
            {
                string json = JsonSerializer.Serialize(_calendarData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_calendarNotesPath, json);
            }
            catch { }
        }

        private void RenderCalendar()
        {
            CalendarGrid.Children.Clear();

            string[] monthNames = {
                "Январь", "Февраль", "Март", "Апрель", "Май", "Июнь",
                "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"
            };

            TxtMonthYearHeader.Text = monthNames[_currentCalendarMonth.Month - 1] + " " + _currentCalendarMonth.Year;

            DateTime firstDayOfMonth = new DateTime(_currentCalendarMonth.Year, _currentCalendarMonth.Month, 1);
            int daysInMonth = DateTime.DaysInMonth(_currentCalendarMonth.Year, _currentCalendarMonth.Month);
            int dayOfWeekOffset = ((int)firstDayOfMonth.DayOfWeek + 6) % 7;

            DateTime prevMonth = _currentCalendarMonth.AddMonths(-1);
            int daysInPrevMonth = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);

            for (int i = 0; i < 42; i++)
            {
                DateTime cellDate;
                bool isCurrentMonth = false;

                if (i < dayOfWeekOffset)
                {
                    int dayNum = daysInPrevMonth - dayOfWeekOffset + i + 1;
                    cellDate = new DateTime(prevMonth.Year, prevMonth.Month, dayNum);
                }
                else if (i >= dayOfWeekOffset && i < dayOfWeekOffset + daysInMonth)
                {
                    int dayNum = i - dayOfWeekOffset + 1;
                    cellDate = new DateTime(_currentCalendarMonth.Year, _currentCalendarMonth.Month, dayNum);
                    isCurrentMonth = true;
                }
                else
                {
                    DateTime nextMonth = _currentCalendarMonth.AddMonths(1);
                    int dayNum = i - (dayOfWeekOffset + daysInMonth) + 1;
                    cellDate = new DateTime(nextMonth.Year, nextMonth.Month, dayNum);
                }

                Button btn = new Button
                {
                    Content = cellDate.Day.ToString(),
                    Tag = cellDate,
                    Style = (Style)FindResource("CalTileButton"),
                    Margin = new Thickness(3)
                };

                string dateKey = cellDate.ToString("yyyy-MM-dd");
                bool hasCustomColor = _calendarData.ContainsKey(dateKey) && !string.IsNullOrEmpty(_calendarData[dateKey].ColorHex);

                if (cellDate.Date == _selectedDate.Date)
                {
                    btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                    btn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F0F13"));
                    btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                    btn.FontWeight = FontWeights.Bold;
                }
                else if (hasCustomColor)
                {
                    string hex = _calendarData[dateKey].ColorHex;
                    btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                    btn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                    btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                }
                else if (isCurrentMonth)
                {
                    btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22222E"));
                    btn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                    btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C2C3A"));
                }
                else
                {
                    btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#14141A"));
                    btn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A58"));
                    btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1C1C24"));
                }

                btn.Click += DayTile_Click;
                CalendarGrid.Children.Add(btn);
            }

            UpdateCalendarSelectedDateUI();
        }

        private void DayTile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DateTime dt)
            {
                _selectedDate = dt.Date;

                if (_selectedDate.Month != _currentCalendarMonth.Month || _selectedDate.Year != _currentCalendarMonth.Year)
                {
                    _currentCalendarMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
                }

                RenderCalendar();
            }
        }

        private void ColorPick_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string tag = btn.Tag.ToString();
                string key = _selectedDate.ToString("yyyy-MM-dd");

                if (!_calendarData.ContainsKey(key))
                    _calendarData[key] = new CalendarDayEntry();

                if (tag == "CLEAR")
                    _calendarData[key].ColorHex = string.Empty;
                else
                    _calendarData[key].ColorHex = tag;

                SaveCalendarNotes();
                RenderCalendar();
            }
        }

        private void TxtCalendarDayNote_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingNotes) return;

            string key = _selectedDate.ToString("yyyy-MM-dd");
            if (!_calendarData.ContainsKey(key))
                _calendarData[key] = new CalendarDayEntry();

            _calendarData[key].Note = TxtCalendarDayNote.Text;
            SaveCalendarNotes();
        }

        private void UpdateCalendarSelectedDateUI()
        {
            _isLoadingNotes = true;

            string[] monthNames = {
                "Января", "Февраля", "Марта", "Апреля", "Мая", "Июня",
                "Июля", "Августа", "Сентября", "Октября", "Ноября", "Декабря"
            };

            TxtSelectedDateDisplay.Text = string.Format("{0} {1} {2}", _selectedDate.Day, monthNames[_selectedDate.Month - 1], _selectedDate.Year);

            string key = _selectedDate.ToString("yyyy-MM-dd");
            if (_calendarData.ContainsKey(key))
            {
                TxtCalendarDayNote.Text = _calendarData[key].Note;
            }
            else
            {
                TxtCalendarDayNote.Text = string.Empty;
            }

            _isLoadingNotes = false;
        }

        private void BtnPrevMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentCalendarMonth = _currentCalendarMonth.AddMonths(-1);
            RenderCalendar();
        }

        private void BtnNextMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentCalendarMonth = _currentCalendarMonth.AddMonths(1);
            RenderCalendar();
        }
        #endregion

        #region Navigation
        private void NavDashboard_Click(object sender, RoutedEventArgs e)
        {
            ViewDashboard.Visibility = Visibility.Visible;
            ViewCalendar.Visibility = Visibility.Collapsed;
            BtnNavDashboard.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            BtnNavCalendar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E8E9A"));
        }

        private void NavCalendar_Click(object sender, RoutedEventArgs e)
        {
            ViewDashboard.Visibility = Visibility.Collapsed;
            ViewCalendar.Visibility = Visibility.Visible;
            BtnNavDashboard.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E8E9A"));
            BtnNavCalendar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
        #endregion

        #region Folders Management
        private void LoadFoldersFromFile()
        {
            FolderList.Clear();
            try
            {
                if (File.Exists(_foldersFilePath))
                {
                    string[] lines = File.ReadAllLines(_foldersFilePath);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        string[] parts = line.Split('|');
                        if (parts.Length >= 2)
                        {
                            FolderList.Add(new FolderModel
                            {
                                Name = parts[0],
                                Path = parts[1],
                                IsSelected = parts.Length <= 2 || bool.Parse(parts[2])
                            });
                        }
                    }
                }
                else
                {
                    FolderList.Add(new FolderModel { Name = "Проекты C#", Path = @"C:\Dev\Projects", IsSelected = true });
                    FolderList.Add(new FolderModel { Name = "Загрузки", Path = @"C:\Users\Public\Downloads", IsSelected = true });
                    SaveFoldersToFile();
                }
            }
            catch { }

            UpdateActiveFoldersCount();
        }

        private void SaveFoldersToFile()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (var folder in FolderList)
                {
                    lines.Add(string.Format("{0}|{1}|{2}", folder.Name, folder.Path, folder.IsSelected));
                }
                File.WriteAllLines(_foldersFilePath, lines);
            }
            catch { }

            UpdateActiveFoldersCount();
        }

        private void UpdateActiveFoldersCount()
        {
            TxtActiveFoldersCount.Text = FolderList.Count.ToString();
        }

        private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Выберите папку для добавления в список";
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string path = dialog.SelectedPath;

                    if (!FolderList.Any(f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                    {
                        string name = System.IO.Path.GetFileName(path);
                        if (string.IsNullOrEmpty(name)) name = path;

                        FolderList.Add(new FolderModel { Name = name, Path = path, IsSelected = true });
                        SaveFoldersToFile();
                    }
                }
            }
        }

        private void BtnRemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = FolderList.Where(f => f.IsSelected).ToList();
            foreach (var item in selectedItems)
            {
                FolderList.Remove(item);
            }
            SaveFoldersToFile();
        }

        private void BtnOpenFolders_Click(object sender, RoutedEventArgs e)
        {
            foreach (var folder in FolderList.Where(f => f.IsSelected))
            {
                if (Directory.Exists(folder.Path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folder.Path,
                        UseShellExecute = true
                    });
                }
            }
        }
        #endregion

        #region Focus Timer
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_timeRemaining.TotalSeconds > 0)
            {
                _timeRemaining = _timeRemaining.Subtract(TimeSpan.FromSeconds(1));
                TxtTimerDisplay.Text = _timeRemaining.ToString(@"mm\:ss");
            }
            else
            {
                _timer.Stop();
                _isTimerRunning = false;
                MessageBox.Show("Фокус-сессия завершена!", "Explorf Timer", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null && int.TryParse(btn.Tag.ToString(), out int minutes))
            {
                _timer.Stop();
                _isTimerRunning = false;
                _timeRemaining = TimeSpan.FromMinutes(minutes);
                TxtTimerDisplay.Text = _timeRemaining.ToString(@"mm\:ss");
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!_isTimerRunning)
            {
                if (_timeRemaining.TotalSeconds <= 0)
                    ParseTimerInput();

                _timer.Start();
                _isTimerRunning = true;
            }
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _isTimerRunning = false;
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _isTimerRunning = false;
            _timeRemaining = TimeSpan.FromMinutes(10);
            TxtTimerDisplay.Text = "10:00";
        }

        private void TxtTimerDisplay_LostFocus(object sender, RoutedEventArgs e) => ParseTimerInput();

        private void ParseTimerInput()
        {
            if (TimeSpan.TryParseExact(TxtTimerDisplay.Text, @"mm\:ss", null, out TimeSpan parsed))
            {
                _timeRemaining = parsed;
            }
            else if (int.TryParse(TxtTimerDisplay.Text, out int minutes))
            {
                _timeRemaining = TimeSpan.FromMinutes(minutes);
                TxtTimerDisplay.Text = _timeRemaining.ToString(@"mm\:ss");
            }
        }
        #endregion
    }
}