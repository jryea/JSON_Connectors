using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using Revit.Import;
using CG = Core.Models.Geometry;
using Revit.Export.Models;

namespace Revit.ViewModels
{
    public class ImportStructuralModelViewModel : INotifyPropertyChanged
    {
        #region Fields
        private UIApplication _uiApp;
        private Document _document;
        private string _inputLocation;
        private bool _importFromETABS = true;
        private bool _importFromRAM;
        private bool _importFromGrasshopper;

        // Transformation options
        private bool _useGridIntersection = false;
        private bool _useManualRotation = true;
        private bool _useImportedGrids = false;
        private string _grid1Name = "A";
        private string _grid2Name = "1";
        private string _importedGrid1Name = "A";
        private string _importedGrid2Name = "1";
        private double _rotationAngle = 0.0;
        private double _baseLevelElevation = 0.0;

        // Element categories
        private bool _importGrids = true;
        private bool _importBeams = true;
        private bool _importBraces = true;
        private bool _importColumns = true;
        private bool _importFloors = true;
        private bool _importWalls = true;
        private bool _importFootings = true;

        // Material types
        private bool _importSteel = true;
        private bool _importConcrete = true;
        #endregion

        #region Properties
        public bool? DialogResult { get; set; }

        public string InputLocation
        {
            get => _inputLocation;
            set
            {
                _inputLocation = value;
                OnPropertyChanged();
            }
        }

        public bool ImportFromETABS
        {
            get => _importFromETABS;
            set
            {
                if (_importFromETABS != value)
                {
                    _importFromETABS = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ImportFromRAM
        {
            get => _importFromRAM;
            set
            {
                if (_importFromRAM != value)
                {
                    _importFromRAM = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ImportFromGrasshopper
        {
            get => _importFromGrasshopper;
            set
            {
                if (_importFromGrasshopper != value)
                {
                    _importFromGrasshopper = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool UseGridIntersection
        {
            get => _useGridIntersection;
            set
            {
                if (_useGridIntersection != value)
                {
                    _useGridIntersection = value;
                    if (value) _useManualRotation = false;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(UseManualRotation));
                }
            }
        }

        public bool UseManualRotation
        {
            get => _useManualRotation;
            set
            {
                if (_useManualRotation != value)
                {
                    _useManualRotation = value;
                    if (value) _useGridIntersection = false;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(UseGridIntersection));
                }
            }
        }

        public bool UseImportedGrids
        {
            get => _useImportedGrids;
            set
            {
                _useImportedGrids = value;
                OnPropertyChanged();
            }
        }

        public string Grid1Name
        {
            get => _grid1Name;
            set
            {
                _grid1Name = value;
                OnPropertyChanged();
            }
        }

        public string Grid2Name
        {
            get => _grid2Name;
            set
            {
                _grid2Name = value;
                OnPropertyChanged();
            }
        }

        public string ImportedGrid1Name
        {
            get => _importedGrid1Name;
            set
            {
                _importedGrid1Name = value;
                OnPropertyChanged();
            }
        }

        public string ImportedGrid2Name
        {
            get => _importedGrid2Name;
            set
            {
                _importedGrid2Name = value;
                OnPropertyChanged();
            }
        }

        public double RotationAngle
        {
            get => _rotationAngle;
            set
            {
                _rotationAngle = value;
                OnPropertyChanged();
            }
        }

        public double BaseLevelElevation
        {
            get => _baseLevelElevation;
            set
            {
                _baseLevelElevation = value;
                OnPropertyChanged();
            }
        }

        // Element Categories
        public bool ImportGrids
        {
            get => _importGrids;
            set
            {
                _importGrids = value;
                OnPropertyChanged();
            }
        }

        public bool ImportBeams
        {
            get => _importBeams;
            set
            {
                _importBeams = value;
                OnPropertyChanged();
            }
        }

        public bool ImportBraces
        {
            get => _importBraces;
            set
            {
                _importBraces = value;
                OnPropertyChanged();
            }
        }

        public bool ImportColumns
        {
            get => _importColumns;
            set
            {
                _importColumns = value;
                OnPropertyChanged();
            }
        }

        public bool ImportFloors
        {
            get => _importFloors;
            set
            {
                _importFloors = value;
                OnPropertyChanged();
            }
        }

        public bool ImportWalls
        {
            get => _importWalls;
            set
            {
                _importWalls = value;
                OnPropertyChanged();
            }
        }

        public bool ImportFootings
        {
            get => _importFootings;
            set
            {
                _importFootings = value;
                OnPropertyChanged();
            }
        }

        // Material Types
        public bool ImportSteel
        {
            get => _importSteel;
            set
            {
                _importSteel = value;
                OnPropertyChanged();
            }
        }

        public bool ImportConcrete
        {
            get => _importConcrete;
            set
            {
                _importConcrete = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Commands
        public ICommand BrowseInputCommand { get; private set; }
        public ICommand ImportCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        #endregion

        #region Constructors
        public ImportStructuralModelViewModel()
        {
            InitializeCommands();
        }

        public ImportStructuralModelViewModel(UIApplication uiApp)
        {
            _uiApp = uiApp;
            _document = uiApp?.ActiveUIDocument?.Document;
            InitializeCommands();
        }
        #endregion

        #region Methods
        private void InitializeCommands()
        {
            BrowseInputCommand = new RelayCommand(BrowseInput);
            ImportCommand = new RelayCommand(Import, CanImport);
            CancelCommand = new RelayCommand(obj => RequestClose?.Invoke());
        }

        private void BrowseInput(object parameter)
        {
            try
            {
                string extension = ".json";
                string filter = "JSON Files (*.json)|*.json";

                if (ImportFromETABS)
                {
                    extension = ".e2k";
                    filter = "ETABS Files (*.e2k)|*.e2k";
                }
                else if (ImportFromRAM)
                {
                    extension = ".rss";
                    filter = "RAM Files (*.rss)|*.rss";
                }

                var dialog = new OpenFileDialog
                {
                    Title = "Select Import File",
                    Filter = filter,
                    DefaultExt = extension
                };

                if (dialog.ShowDialog() == true)
                {
                    InputLocation = dialog.FileName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting input file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Import(object parameter)
        {
            if (string.IsNullOrEmpty(InputLocation))
            {
                MessageBox.Show("Please select an input file first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Create import filters
                var elementFilters = new Dictionary<string, bool>
                {
                    { "Grids", ImportGrids },
                    { "Beams", ImportBeams },
                    { "Braces", ImportBraces },
                    { "Columns", ImportColumns },
                    { "Floors", ImportFloors },
                    { "Walls", ImportWalls },
                    { "Footings", ImportFootings }
                };

                var materialFilters = new Dictionary<string, bool>
                {
                    { "Steel", ImportSteel },
                    { "Concrete", ImportConcrete }
                };

                // Create transformation parameters
                var transformParams = new ImportTransformationParameters
                {
                    UseGridIntersection = UseGridIntersection,
                    UseManualRotation = UseManualRotation,
                    UseImportedGrids = UseImportedGrids,
                    Grid1Name = Grid1Name,
                    Grid2Name = Grid2Name,
                    ImportedGrid1Name = ImportedGrid1Name,
                    ImportedGrid2Name = ImportedGrid2Name,
                    RotationAngle = RotationAngle,
                    BaseLevelElevation = BaseLevelElevation
                };

                // Perform import
                var importManager = new ImportManager(_document, _uiApp);
                int importedCount = importManager.ImportFromFile(InputLocation, elementFilters, materialFilters, transformParams);

                MessageBox.Show($"Successfully imported {importedCount} elements.",
                    "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                RequestClose?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during import: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanImport(object parameter)
        {
            return !string.IsNullOrEmpty(InputLocation);
        }
        #endregion

        #region Events
        public event Action RequestClose;
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            if (propertyName == nameof(InputLocation))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion
    }

    public class ImportTransformationParameters
    {
        public bool UseGridIntersection { get; set; }
        public bool UseManualRotation { get; set; }
        public bool UseImportedGrids { get; set; }
        public string Grid1Name { get; set; }
        public string Grid2Name { get; set; }
        public string ImportedGrid1Name { get; set; }
        public string ImportedGrid2Name { get; set; }
        public double RotationAngle { get; set; }
        public double BaseLevelElevation { get; set; }
    }
}