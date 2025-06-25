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
using Revit.Models;

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
        private double _rotationAngle = 0.0;
        private double _baseLevelElevation = 0.0;

        // Grid selection properties
        private string _selectedGridSource;
        private Autodesk.Revit.DB.Grid _selectedHorizontalGrid;
        private Autodesk.Revit.DB.Grid _selectedVerticalGrid;
        private ObservableCollection<string> _availableGridSources;
        private ObservableCollection<Autodesk.Revit.DB.Grid> _availableHorizontalGrids;
        private ObservableCollection<Autodesk.Revit.DB.Grid> _availableVerticalGrids;

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
                    if (value)
                    {
                        _useManualRotation = false;
                        // Populate grid collections when grid intersection is selected
                        PopulateGridCollections();
                    }
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

        // Grid Selection Properties
        public string SelectedGridSource
        {
            get => _selectedGridSource;
            set
            {
                if (_selectedGridSource != value)
                {
                    _selectedGridSource = value;
                    OnPropertyChanged();
                    // Refresh grid lists when source changes
                    PopulateGridCollections();
                }
            }
        }

        public Autodesk.Revit.DB.Grid SelectedHorizontalGrid
        {
            get => _selectedHorizontalGrid;
            set
            {
                _selectedHorizontalGrid = value;
                OnPropertyChanged();
            }
        }

        public Autodesk.Revit.DB.Grid SelectedVerticalGrid
        {
            get => _selectedVerticalGrid;
            set
            {
                _selectedVerticalGrid = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> AvailableGridSources
        {
            get => _availableGridSources;
            set
            {
                _availableGridSources = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Autodesk.Revit.DB.Grid> AvailableHorizontalGrids
        {
            get => _availableHorizontalGrids;
            set
            {
                _availableHorizontalGrids = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<Autodesk.Revit.DB.Grid> AvailableVerticalGrids
        {
            get => _availableVerticalGrids;
            set
            {
                _availableVerticalGrids = value;
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
            InitializeGridCollections();
        }

        public ImportStructuralModelViewModel(UIApplication uiApp)
        {
            _uiApp = uiApp;
            _document = uiApp?.ActiveUIDocument?.Document;
            InitializeCommands();
            InitializeGridCollections();

            // Populate grid sources on initialization
            if (_document != null)
            {
                PopulateGridSources();
            }
        }
        #endregion

        #region Methods
        private void InitializeCommands()
        {
            BrowseInputCommand = new RelayCommand(BrowseInput);
            ImportCommand = new RelayCommand(Import, CanImport);
            CancelCommand = new RelayCommand(obj => RequestClose?.Invoke());
        }

        private void InitializeGridCollections()
        {
            AvailableGridSources = new ObservableCollection<string>();
            AvailableHorizontalGrids = new ObservableCollection<Autodesk.Revit.DB.Grid>();
            AvailableVerticalGrids = new ObservableCollection<Autodesk.Revit.DB.Grid>();

            // Populate initial grid sources
            PopulateGridSources();
        }

        private void PopulateGridSources()
        {
            AvailableGridSources.Clear();

            // Add current structural model option
            AvailableGridSources.Add("Current Structural Model");

            // Add linked architectural models
            if (_document != null)
            {
                try
                {
                    var linkedModels = new FilteredElementCollector(_document)
                        .OfCategory(BuiltInCategory.OST_RvtLinks)
                        .WhereElementIsNotElementType()
                        .Cast<RevitLinkInstance>()
                        .Where(link => link.GetLinkDocument() != null)
                        .ToList();

                    foreach (var link in linkedModels)
                    {
                        var linkDoc = link.GetLinkDocument();
                        if (linkDoc != null)
                        {
                            // Check if the linked model has grids
                            var gridCount = new FilteredElementCollector(linkDoc)
                                .OfClass(typeof(Autodesk.Revit.DB.Grid))
                                .GetElementCount();

                            if (gridCount > 0)
                            {
                                string linkName = linkDoc.Title;
                                AvailableGridSources.Add($"Linked: {linkName}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // If we can't get linked models, just use current model
                    System.Diagnostics.Debug.WriteLine($"Error getting linked models: {ex.Message}");
                }
            }

            // Set default selection
            if (AvailableGridSources.Count > 0 && string.IsNullOrEmpty(SelectedGridSource))
            {
                SelectedGridSource = AvailableGridSources.First();
            }
        }

        private void PopulateGridCollections()
        {
            // Clear existing collections
            AvailableHorizontalGrids.Clear();
            AvailableVerticalGrids.Clear();

            if (string.IsNullOrEmpty(SelectedGridSource) || _document == null)
                return;

            try
            {
                List<Autodesk.Revit.DB.Grid> allGrids = new List<Autodesk.Revit.DB.Grid>();

                if (SelectedGridSource == "Current Structural Model")
                {
                    // Get grids from current document
                    allGrids = new FilteredElementCollector(_document)
                        .OfClass(typeof(Autodesk.Revit.DB.Grid))
                        .Cast<Autodesk.Revit.DB.Grid>()
                        .ToList();
                }
                else if (SelectedGridSource.StartsWith("Linked:"))
                {
                    // Get grids from linked document
                    string linkName = SelectedGridSource.Substring(7).Trim(); // Remove "Linked: " prefix

                    var linkedModel = new FilteredElementCollector(_document)
                        .OfCategory(BuiltInCategory.OST_RvtLinks)
                        .WhereElementIsNotElementType()
                        .Cast<RevitLinkInstance>()
                        .FirstOrDefault(link => link.GetLinkDocument()?.Title == linkName);

                    if (linkedModel?.GetLinkDocument() != null)
                    {
                        allGrids = new FilteredElementCollector(linkedModel.GetLinkDocument())
                            .OfClass(typeof(Autodesk.Revit.DB.Grid))
                            .Cast<Autodesk.Revit.DB.Grid>()
                            .ToList();
                    }
                }

                // Filter grids by direction
                foreach (var grid in allGrids)
                {
                    if (IsHorizontalGrid(grid))
                    {
                        AvailableHorizontalGrids.Add(grid);
                    }
                    else if (IsVerticalGrid(grid))
                    {
                        AvailableVerticalGrids.Add(grid);
                    }
                }

                // Auto-select first grid of each type if available
                if (AvailableHorizontalGrids.Count > 0 && SelectedHorizontalGrid == null)
                {
                    SelectedHorizontalGrid = AvailableHorizontalGrids.First();
                }
                if (AvailableVerticalGrids.Count > 0 && SelectedVerticalGrid == null)
                {
                    SelectedVerticalGrid = AvailableVerticalGrids.First();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error populating grid collections: {ex.Message}");
            }
        }

        private bool IsHorizontalGrid(Autodesk.Revit.DB.Grid grid)
        {
            if (grid?.Curve is Line line)
            {
                var direction = line.Direction.Normalize();
                // Consider horizontal if the X component is greater (more horizontal than vertical)
                return Math.Abs(direction.X) > Math.Abs(direction.Y);
            }
            return false;
        }

        private bool IsVerticalGrid(Autodesk.Revit.DB.Grid grid)
        {
            if (grid?.Curve is Line line)
            {
                var direction = line.Direction.Normalize();
                // Consider vertical if the Y component is greater (more vertical than horizontal)
                return Math.Abs(direction.Y) > Math.Abs(direction.X);
            }
            return false;
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

            // Validate grid selection if using grid intersection
            if (UseGridIntersection)
            {
                if (SelectedHorizontalGrid == null || SelectedVerticalGrid == null)
                {
                    MessageBox.Show("Please select both horizontal and vertical grids for grid intersection alignment.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // Just close the dialog with success - let the command handle the actual import
            DialogResult = true;
            RequestClose?.Invoke();
        }

        private bool CanImport(object parameter)
        {
            return true;
            //return !string.IsNullOrEmpty(InputLocation);
        }
        #endregion

        #region Public Methods for Command Access

        /// <summary>
        /// Gets the transformation parameters including grid selections
        /// </summary>
        public ImportTransformationParameters GetTransformationParameters()
        {
            return new ImportTransformationParameters
            {
                UseGridIntersection = UseGridIntersection,
                UseManualRotation = UseManualRotation,
                RotationAngle = RotationAngle,
                BaseLevelElevation = BaseLevelElevation,
                GridSource = SelectedGridSource,
                HorizontalGrid = SelectedHorizontalGrid,
                VerticalGrid = SelectedVerticalGrid
            };
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

        // New properties for grid intersection
        public string GridSource { get; set; }
        public Autodesk.Revit.DB.Grid HorizontalGrid { get; set; }
        public Autodesk.Revit.DB.Grid VerticalGrid { get; set; }
    }
}