using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Revit.ViewModels
{
    public class ImportProgressViewModel : INotifyPropertyChanged
    {
        private readonly Timer _uiUpdateTimer;
        private readonly Stopwatch _stopwatch;
        private CancellationTokenSource _cancellationTokenSource;

        // Progress tracking
        private int _overallProgress;
        private string _currentStepDescription;
        private string _progressText;
        private string _elapsedTimeText;
        private string _logOutput;
        private bool _showDetails;
        private bool _canToggleDetails = true;
        private string _cancelButtonText = "Cancel";

        // Step status tracking
        private ImportStepStatus _conversionStatus = ImportStepStatus.Pending;
        private ImportStepStatus _loadingStatus = ImportStepStatus.Pending;
        private string _conversionStatusText = "Waiting...";
        private string _loadingStatusText = "Waiting...";

        public ImportProgressViewModel()
        {
            _stopwatch = Stopwatch.StartNew();
            _cancellationTokenSource = new CancellationTokenSource();

            // Initialize element import steps
            ElementImportSteps = new ObservableCollection<ElementImportStepViewModel>
            {
                new ElementImportStepViewModel("Importing grids", "Grids"),
                new ElementImportStepViewModel("Importing beams", "Beams"),
                new ElementImportStepViewModel("Importing columns", "Columns"),
                new ElementImportStepViewModel("Importing braces", "Braces"),
                new ElementImportStepViewModel("Importing walls", "Walls"),
                new ElementImportStepViewModel("Importing floors", "Floors"),
                new ElementImportStepViewModel("Importing footings", "Footings")
            };

            // Commands
            CancelCommand = new ImportProgressRelayCommand(Cancel);
            ToggleDetailsCommand = new ImportProgressRelayCommand(ToggleDetails);

            // Start UI update timer
            _uiUpdateTimer = new Timer(UpdateElapsedTime, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

            // Initial state
            CurrentStepDescription = "Preparing to import...";
            ProgressText = "0%";
        }

        #region Properties

        public int OverallProgress
        {
            get => _overallProgress;
            set
            {
                _overallProgress = value;
                OnPropertyChanged();
                ProgressText = $"{value}%";
            }
        }

        public string CurrentStepDescription
        {
            get => _currentStepDescription;
            set
            {
                _currentStepDescription = value;
                OnPropertyChanged();
            }
        }

        public string ProgressText
        {
            get => _progressText;
            private set
            {
                _progressText = value;
                OnPropertyChanged();
            }
        }

        public string ElapsedTimeText
        {
            get => _elapsedTimeText;
            private set
            {
                _elapsedTimeText = value;
                OnPropertyChanged();
            }
        }

        public string LogOutput
        {
            get => _logOutput;
            set
            {
                _logOutput = value;
                OnPropertyChanged();
            }
        }

        public bool ShowDetails
        {
            get => _showDetails;
            set
            {
                _showDetails = value;
                OnPropertyChanged();
            }
        }

        public bool CanToggleDetails
        {
            get => _canToggleDetails;
            set
            {
                _canToggleDetails = value;
                OnPropertyChanged();
            }
        }

        public string CancelButtonText
        {
            get => _cancelButtonText;
            set
            {
                _cancelButtonText = value;
                OnPropertyChanged();
            }
        }

        // Conversion step properties
        public Brush ConversionStatusColor => GetStatusColor(_conversionStatus);
        public string ConversionStatusIcon => GetStatusIcon(_conversionStatus);
        public string ConversionStatusText
        {
            get => _conversionStatusText;
            set
            {
                _conversionStatusText = value;
                OnPropertyChanged();
            }
        }

        // Loading step properties
        public Brush LoadingStatusColor => GetStatusColor(_loadingStatus);
        public string LoadingStatusIcon => GetStatusIcon(_loadingStatus);
        public string LoadingStatusText
        {
            get => _loadingStatusText;
            set
            {
                _loadingStatusText = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ElementImportStepViewModel> ElementImportSteps { get; }

        public ICommand CancelCommand { get; }
        public ICommand ToggleDetailsCommand { get; }

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        #endregion

        #region Status Update Methods

        public void StartConversion()
        {
            UpdateConversionStatus(ImportStepStatus.InProgress, "Converting...");
            CurrentStepDescription = "Converting RAM file to JSON format";
            OverallProgress = 5;
        }

        public void CompleteConversion()
        {
            UpdateConversionStatus(ImportStepStatus.Complete, "Complete");
            OverallProgress = 20;
        }

        public void FailConversion(string error)
        {
            UpdateConversionStatus(ImportStepStatus.Failed, "Failed");
            AppendLog($"Conversion failed: {error}");
        }

        public void StartLoading()
        {
            UpdateLoadingStatus(ImportStepStatus.InProgress, "Loading...");
            CurrentStepDescription = "Loading and parsing model data";
            OverallProgress = 25;
        }

        public void CompleteLoading()
        {
            UpdateLoadingStatus(ImportStepStatus.Complete, "Complete");
            OverallProgress = 35;
        }

        public void FailLoading(string error)
        {
            UpdateLoadingStatus(ImportStepStatus.Failed, "Failed");
            AppendLog($"Loading failed: {error}");
        }

        public void StartElementImport(string elementType)
        {
            var step = FindElementStep(elementType);
            if (step != null)
            {
                step.Status = ImportStepStatus.InProgress;
                step.StatusText = "Importing...";
                CurrentStepDescription = $"Importing {elementType.ToLower()}";
            }
        }

        public void CompleteElementImport(string elementType, int count)
        {
            var step = FindElementStep(elementType);
            if (step != null)
            {
                step.Status = ImportStepStatus.Complete;
                step.StatusText = $"{count} imported";
            }

            // Update overall progress based on completed steps
            UpdateOverallProgressFromSteps();
        }

        public void SkipElementImport(string elementType, string reason = "Skipped")
        {
            var step = FindElementStep(elementType);
            if (step != null)
            {
                step.Status = ImportStepStatus.Skipped;
                step.StatusText = reason;
            }

            UpdateOverallProgressFromSteps();
        }

        public void FailElementImport(string elementType, string error)
        {
            var step = FindElementStep(elementType);
            if (step != null)
            {
                step.Status = ImportStepStatus.Failed;
                step.StatusText = "Failed";
            }

            AppendLog($"{elementType} import failed: {error}");
        }

        public void CompleteImport(int totalImported)
        {
            OverallProgress = 100;
            CurrentStepDescription = $"Import completed - {totalImported} elements imported";
            CancelButtonText = "Close";
            CanToggleDetails = true;
        }

        public void AppendLog(string message)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                LogOutput += $"[{timestamp}] {message}\n";
            }));
        }

        #endregion

        #region Private Methods

        private void UpdateConversionStatus(ImportStepStatus status, string statusText)
        {
            _conversionStatus = status;
            ConversionStatusText = statusText;
            OnPropertyChanged(nameof(ConversionStatusColor));
            OnPropertyChanged(nameof(ConversionStatusIcon));
        }

        private void UpdateLoadingStatus(ImportStepStatus status, string statusText)
        {
            _loadingStatus = status;
            LoadingStatusText = statusText;
            OnPropertyChanged(nameof(LoadingStatusColor));
            OnPropertyChanged(nameof(LoadingStatusIcon));
        }

        private ElementImportStepViewModel FindElementStep(string elementType)
        {
            foreach (var step in ElementImportSteps)
            {
                if (step.ElementType.Equals(elementType, StringComparison.OrdinalIgnoreCase))
                    return step;
            }
            return null;
        }

        private void UpdateOverallProgressFromSteps()
        {
            int completedSteps = 0;
            foreach (var step in ElementImportSteps)
            {
                if (step.Status == ImportStepStatus.Complete || step.Status == ImportStepStatus.Skipped)
                    completedSteps++;
            }

            // Base progress is 35% after loading, remaining 65% for element import
            int elementProgress = (int)((double)completedSteps / ElementImportSteps.Count * 65);
            OverallProgress = 35 + elementProgress;
        }

        private Brush GetStatusColor(ImportStepStatus status)
        {
            switch (status)
            {
                case ImportStepStatus.Pending:
                    return new SolidColorBrush(Colors.LightGray);
                case ImportStepStatus.InProgress:
                    return new SolidColorBrush(Color.FromRgb(0, 86, 135)); // Primary Blue
                case ImportStepStatus.Complete:
                    return new SolidColorBrush(Color.FromRgb(139, 195, 74)); // Accent Green
                case ImportStepStatus.Failed:
                    return new SolidColorBrush(Colors.Red);
                case ImportStepStatus.Skipped:
                    return new SolidColorBrush(Colors.Orange);
                default:
                    return new SolidColorBrush(Colors.Gray);
            }
        }

        private string GetStatusIcon(ImportStepStatus status)
        {
            switch (status)
            {
                case ImportStepStatus.Pending:
                    return "○";
                case ImportStepStatus.InProgress:
                    return "●";
                case ImportStepStatus.Complete:
                    return "✓";
                case ImportStepStatus.Failed:
                    return "✗";
                case ImportStepStatus.Skipped:
                    return "−";
                default:
                    return "?";
            }
        }

        private void UpdateElapsedTime(object state)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ElapsedTimeText = $"Elapsed: {_stopwatch.Elapsed:mm\\:ss}";
            }));
        }

        private void Cancel()
        {
            _cancellationTokenSource?.Cancel();
            // Window closing will be handled by the calling code
        }

        private void ToggleDetails()
        {
            ShowDetails = !ShowDetails;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _uiUpdateTimer?.Dispose();
            _cancellationTokenSource?.Dispose();
            _stopwatch?.Stop();
        }

        #endregion
    }

    // Supporting classes
    public enum ImportStepStatus
    {
        Pending,
        InProgress,
        Complete,
        Failed,
        Skipped
    }

    public class ElementImportStepViewModel : INotifyPropertyChanged
    {
        private ImportStepStatus _status = ImportStepStatus.Pending;
        private string _statusText = "Waiting...";

        public ElementImportStepViewModel(string description, string elementType)
        {
            Description = description;
            ElementType = elementType;
        }

        public string Description { get; }
        public string ElementType { get; }

        public ImportStepStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(StatusIcon));
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public Brush StatusColor => GetStatusColor(_status);
        public string StatusIcon => GetStatusIcon(_status);

        private Brush GetStatusColor(ImportStepStatus status)
        {
            switch (status)
            {
                case ImportStepStatus.Pending:
                    return new SolidColorBrush(Colors.LightGray);
                case ImportStepStatus.InProgress:
                    return new SolidColorBrush(Color.FromRgb(0, 86, 135));
                case ImportStepStatus.Complete:
                    return new SolidColorBrush(Color.FromRgb(139, 195, 74));
                case ImportStepStatus.Failed:
                    return new SolidColorBrush(Colors.Red);
                case ImportStepStatus.Skipped:
                    return new SolidColorBrush(Colors.Orange);
                default:
                    return new SolidColorBrush(Colors.Gray);
            }
        }

        private string GetStatusIcon(ImportStepStatus status)
        {
            switch (status)
            {
                case ImportStepStatus.Pending:
                    return "○";
                case ImportStepStatus.InProgress:
                    return "●";
                case ImportStepStatus.Complete:
                    return "✓";
                case ImportStepStatus.Failed:
                    return "✗";
                case ImportStepStatus.Skipped:
                    return "−";
                default:
                    return "?";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Simple relay command implementation - remove if you already have one
    public class ImportProgressRelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public ImportProgressRelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => _execute();

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}