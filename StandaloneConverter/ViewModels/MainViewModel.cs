using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using StandaloneConverter.Models;
using StandaloneConverter.Services;

namespace StandaloneConverter.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ConversionService _conversionService;
        private readonly LoggingService _loggingService;

        private bool _isRamToEtabs = true;
        private string _inputFilePath;
        private string _outputFilePath;
        private string _logOutput;

        public bool IsRamToEtabs
        {
            get => _isRamToEtabs;
            set
            {
                if (_isRamToEtabs != value)
                {
                    _isRamToEtabs = value;
                    OnPropertyChanged();
                    UpdateFilePaths();
                }
            }
        }

        public bool IsEtabsToRam
        {
            get => !_isRamToEtabs;
            set
            {
                if (_isRamToEtabs == value)
                {
                    _isRamToEtabs = !value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsRamToEtabs));
                    UpdateFilePaths();
                }
            }
        }

        public string InputFilePath
        {
            get => _inputFilePath;
            set
            {
                if (_inputFilePath != value)
                {
                    _inputFilePath = value;
                    OnPropertyChanged();

                    if (!string.IsNullOrEmpty(_inputFilePath))
                    {
                        var directory = Path.GetDirectoryName(_inputFilePath);
                        var fileName = Path.GetFileNameWithoutExtension(_inputFilePath);

                        OutputFilePath = Path.Combine(directory,
                            fileName + (IsRamToEtabs ? ".e2k" : ".rss"));
                    }
                }
            }
        }

        public string OutputFilePath
        {
            get => _outputFilePath;
            set
            {
                if (_outputFilePath != value)
                {
                    _outputFilePath = value;
                    OnPropertyChanged();
                }
            }
        }

        public string LogOutput
        {
            get => _logOutput;
            set
            {
                if (_logOutput != value)
                {
                    _logOutput = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand BrowseInputCommand { get; }
        public ICommand BrowseOutputCommand { get; }
        public ICommand ConvertCommand { get; }
        public ICommand CloseCommand { get; }

        public MainViewModel()
        {
            _conversionService = new ConversionService();
            _loggingService = new LoggingService();

            _loggingService.LogMessageAdded += (sender, message) =>
            {
                LogOutput += message + Environment.NewLine;
            };

            BrowseInputCommand = new RelayCommand(BrowseInput);
            BrowseOutputCommand = new RelayCommand(BrowseOutput);
            ConvertCommand = new RelayCommand(ConvertAsync, CanConvert);
            CloseCommand = new RelayCommand(Close);

            _logOutput = "Ready to convert. Select files and options to begin.";
        }

        private void UpdateFilePaths()
        {
            if (!string.IsNullOrEmpty(InputFilePath))
            {
                var directory = Path.GetDirectoryName(InputFilePath);
                var fileName = Path.GetFileNameWithoutExtension(InputFilePath);

                OutputFilePath = Path.Combine(directory,
                    fileName + (IsRamToEtabs ? ".e2k" : ".rss"));
            }
        }

        private void BrowseInput()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Input File",
                Filter = IsRamToEtabs
                    ? "RAM Files (*.rss)|*.rss|All Files (*.*)|*.*"
                    : "ETABS Files (*.e2k)|*.e2k|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                InputFilePath = dialog.FileName;
            }
        }

        private void BrowseOutput()
        {
            var dialog = new SaveFileDialog
            {
                Title = "Select Output File",
                Filter = IsRamToEtabs
                    ? "ETABS Files (*.e2k)|*.e2k|All Files (*.*)|*.*"
                    : "RAM Files (*.rss)|*.rss|All Files (*.*)|*.*",
                OverwritePrompt = true,
                FileName = !string.IsNullOrEmpty(OutputFilePath)
                    ? Path.GetFileName(OutputFilePath)
                    : ""
            };

            if (dialog.ShowDialog() == true)
            {
                OutputFilePath = dialog.FileName;
            }
        }

        private async void ConvertAsync()
        {
            try
            {
                // Remove setting IsConverting = true
                LogOutput = ""; // Clear previous log

                if (!File.Exists(InputFilePath))
                {
                    _loggingService.Log("Error: Input file does not exist.");
                    return;
                }

                var options = new ConversionOptions
                {
                    InputFilePath = InputFilePath,
                    OutputFilePath = OutputFilePath,
                    IntermediateJsonPath = Path.Combine(
                        Path.GetDirectoryName(OutputFilePath),
                        Path.GetFileNameWithoutExtension(OutputFilePath) + ".json"),
                    IsRamToEtabs = IsRamToEtabs
                };

                await Task.Run(() => _conversionService.Convert(options, _loggingService));

                _loggingService.Log("Conversion complete.");
            }
            catch (Exception ex)
            {
                _loggingService.Log($"Error during conversion: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _loggingService.Log($"Inner error: {ex.InnerException.Message}");
                }
                MessageBox.Show($"Conversion failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanConvert()
        {
            return !string.IsNullOrEmpty(InputFilePath) &&
                   !string.IsNullOrEmpty(OutputFilePath);
        }

        private void Close()
        {
            Application.Current.Shutdown();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

            public bool CanExecute(object parameter)
            {
                return _canExecute == null || _canExecute();
            }

            public void Execute(object parameter)
            {
                _execute();
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }
        }
    }
}