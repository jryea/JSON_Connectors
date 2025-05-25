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
using Revit.Export;
using Revit.Export.Models;

namespace Revit.ViewModels
{
    /// <summary>
    /// Simplified export view model using the UnifiedExporter
    /// </summary>
    public class ExportStructuralModelViewModel : INotifyPropertyChanged
    {
        #region Fields
        private UIApplication _uiApp;
        private Document _document;
        private string _outputLocation;
        private bool _exportToETABS = true;
        private bool _exportToRAM;
        private bool _exportToGrasshopper;
        private string _newFloorTypeName;
        private ObservableCollection<FloorTypeModel> _floorTypes;
        private FloorTypeModel _selectedFloorType;
        private string _levelSearchText;
        private ObservableCollection<LevelViewModel> _levelCollection;
        private ObservableCollection<ViewPlanViewModel> _viewPlanCollection;
        private ObservableCollection<FloorTypeViewMappingModel> _floorTypeViewMappingCollection;
        private ObservableCollection<LevelViewModel> _masterStoryLevels;
        private ICollectionView _filteredLevelCollection;
        private LevelViewModel _baseLevel;

        // Element categories
        private bool _exportGrids = true;
        private bool _exportBeams = true;
        private bool _exportBraces = true;
        private bool _exportColumns = true;
        private bool _exportFloors = true;
        private bool _exportWalls = true;
        private bool _exportFootings = true;

        // Material types
        private bool _exportSteel = true;
        private bool _exportConcrete = true;

        // Rotation settings
        private bool _applyRotation = false;
        private double _rotationAngle = 0.0;
        #endregion

        #region Properties
        public bool? DialogResult { get; set; }

        public string OutputLocation
        {
            get => _outputLocation;
            set
            {
                _outputLocation = value;
                OnPropertyChanged();
            }
        }

        // Export format properties
        public bool ExportToETABS
        {
            get => _exportToETABS;
            set
            {
                if (_exportToETABS != value)
                {
                    _exportToETABS = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsFloorLayoutEnabled));
                }
            }
        }

        public bool ExportToRAM
        {
            get => _exportToRAM;
            set
            {
                if (_exportToRAM != value)
                {
                    _exportToRAM = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsFloorLayoutEnabled));
                }
            }
        }

        public bool ExportToGrasshopper
        {
            get => _exportToGrasshopper;
            set
            {
                if (_exportToGrasshopper != value)
                {
                    _exportToGrasshopper = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsFloorLayoutEnabled));
                }
            }
        }

        // Floor type management
        public string NewFloorTypeName
        {
            get => _newFloorTypeName;
            set
            {
                _newFloorTypeName = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<FloorTypeModel> FloorTypes
        {
            get => _floorTypes;
            set
            {
                _floorTypes = value;
                OnPropertyChanged();
            }
        }

        public FloorTypeModel SelectedFloorType
        {
            get => _selectedFloorType;
            set
            {
                _selectedFloorType = value;
                OnPropertyChanged();
            }
        }

        // Level management
        public string LevelSearchText
        {
            get => _levelSearchText;
            set
            {
                _levelSearchText = value;
                OnPropertyChanged();
                FilterLevelCollection();
            }
        }

        public ObservableCollection<LevelViewModel> LevelCollection
        {
            get => _levelCollection;
            set
            {
                _levelCollection = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ViewPlanViewModel> ViewPlanCollection
        {
            get => _viewPlanCollection;
            set
            {
                _viewPlanCollection = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<FloorTypeViewMappingModel> FloorTypeViewMappingCollection
        {
            get => _floorTypeViewMappingCollection;
            set
            {
                _floorTypeViewMappingCollection = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<LevelViewModel> MasterStoryLevels
        {
            get => _masterStoryLevels;
            set
            {
                _masterStoryLevels = value;
                OnPropertyChanged();
            }
        }

        public LevelViewModel BaseLevel
        {
            get => _baseLevel;
            set
            {
                if (_baseLevel != value)
                {
                    _baseLevel = value;
                    OnPropertyChanged();
                    UpdateLevelEnabledStates();
                }
            }
        }

        // Helper properties for UI state
        public bool IsFloorLayoutEnabled => ExportToGrasshopper || ExportToRAM;

        // Element categories
        public bool ExportGrids { get => _exportGrids; set { _exportGrids = value; OnPropertyChanged(); } }
        public bool ExportBeams { get => _exportBeams; set { _exportBeams = value; OnPropertyChanged(); } }
        public bool ExportBraces { get => _exportBraces; set { _exportBraces = value; OnPropertyChanged(); } }
        public bool ExportColumns { get => _exportColumns; set { _exportColumns = value; OnPropertyChanged(); } }
        public bool ExportFloors { get => _exportFloors; set { _exportFloors = value; OnPropertyChanged(); } }
        public bool ExportWalls { get => _exportWalls; set { _exportWalls = value; OnPropertyChanged(); } }
        public bool ExportFootings { get => _exportFootings; set { _exportFootings = value; OnPropertyChanged(); } }

        // Material types
        public bool ExportSteel { get => _exportSteel; set { _exportSteel = value; OnPropertyChanged(); } }
        public bool ExportConcrete { get => _exportConcrete; set { _exportConcrete = value; OnPropertyChanged(); } }

        // Rotation
        public bool ApplyRotation { get => _applyRotation; set { _applyRotation = value; OnPropertyChanged(); } }
        public double RotationAngle { get => _rotationAngle; set { _rotationAngle = value; OnPropertyChanged(); } }
        #endregion

        #region Commands
        public ICommand BrowseOutputCommand { get; private set; }
        public ICommand AddFloorTypeCommand { get; private set; }
        public ICommand RemoveFloorTypeCommand { get; private set; }
        public ICommand ExportCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        #endregion

        #region Constructor
        public ExportStructuralModelViewModel()
        {
            InitializeProperties();
            InitializeCommands();
        }

        public ExportStructuralModelViewModel(UIApplication uiApp)
        {
            _uiApp = uiApp;
            _document = uiApp?.ActiveUIDocument?.Document;

            InitializeProperties();
            InitializeCommands();

            if (_document != null)
            {
                LoadLevels();
                LoadViewPlans();
                UpdateFloorTypeViewMappings();
            }
        }
        #endregion

        #region Initialization
        private void InitializeProperties()
        {
            FloorTypes = new ObservableCollection<FloorTypeModel>();
            LevelCollection = new ObservableCollection<LevelViewModel>();
            ViewPlanCollection = new ObservableCollection<ViewPlanViewModel>();
            FloorTypeViewMappingCollection = new ObservableCollection<FloorTypeViewMappingModel>();
            MasterStoryLevels = new ObservableCollection<LevelViewModel>();

            LevelSearchText = string.Empty;
            ExportToETABS = true;

            _filteredLevelCollection = CollectionViewSource.GetDefaultView(LevelCollection);
            _filteredLevelCollection.Filter = LevelViewFilter;
        }

        private void InitializeCommands()
        {
            BrowseOutputCommand = new RelayCommand(BrowseOutput);
            AddFloorTypeCommand = new RelayCommand(AddFloorType, CanAddFloorType);
            RemoveFloorTypeCommand = new RelayCommand(RemoveFloorType);
            ExportCommand = new RelayCommand(Export, CanExport);
            CancelCommand = new RelayCommand(obj => RequestClose?.Invoke());
        }
        #endregion

        #region Data Loading
        private void LoadLevels()
        {
            LevelCollection.Clear();
            MasterStoryLevels.Clear();

            try
            {
                var levels = new FilteredElementCollector(_document)
                    .OfClass(typeof(Level))
                    .WhereElementIsNotElementType()
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                foreach (var level in levels)
                {
                    var levelViewModel = new LevelViewModel
                    {
                        Id = level.UniqueId,
                        Name = level.Name,
                        Elevation = Math.Round(level.Elevation, 2),
                        LevelId = level.Id,
                        IsSelected = false,
                        IsEnabledForExport = true,
                        IsMasterStory = false,
                        SimilarToLevel = null
                    };

                    levelViewModel.PropertyChanged += LevelViewModel_PropertyChanged;
                    LevelCollection.Add(levelViewModel);
                }

                if (LevelCollection.Count > 0)
                {
                    BaseLevel = LevelCollection.OrderBy(l => l.Elevation).FirstOrDefault();
                }

                UpdateLevelEnabledStates();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading levels: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadViewPlans()
        {
            ViewPlanCollection.Clear();

            try
            {
                var viewPlans = new FilteredElementCollector(_document)
                    .OfClass(typeof(ViewPlan))
                    .WhereElementIsNotElementType()
                    .Cast<ViewPlan>()
                    .Where(v => !v.IsTemplate &&
                               (v.ViewType == ViewType.FloorPlan ||
                                v.ViewType == ViewType.CeilingPlan ||
                                v.ViewType == ViewType.EngineeringPlan))
                    .OrderBy(v => v.Name)
                    .ToList();

                foreach (var viewPlan in viewPlans)
                {
                    var viewPlanViewModel = new ViewPlanViewModel
                    {
                        Id = viewPlan.UniqueId,
                        Name = viewPlan.Name,
                        ViewId = viewPlan.Id
                    };

                    ViewPlanCollection.Add(viewPlanViewModel);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading view plans: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Command Implementations
        private void BrowseOutput(object parameter)
        {
            try
            {
                string extension = ".json";
                string filter = "JSON Files (*.json)|*.json";

                if (ExportToETABS)
                {
                    extension = ".e2k";
                    filter = "ETABS Files (*.e2k)|*.e2k";
                }
                else if (ExportToRAM)
                {
                    extension = ".rss";
                    filter = "RAM Files (*.rss)|*.rss";
                }

                string defaultFileName = _document?.Title ?? "Export";
                defaultFileName = Path.GetFileNameWithoutExtension(defaultFileName);

                var dialog = new SaveFileDialog
                {
                    Title = "Save Export File",
                    Filter = filter,
                    FileName = defaultFileName,
                    DefaultExt = extension
                };

                if (dialog.ShowDialog() == true)
                {
                    OutputLocation = dialog.FileName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting output location: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddFloorType(object parameter)
        {
            var typeName = NewFloorTypeName?.Trim();
            if (string.IsNullOrEmpty(typeName))
            {
                MessageBox.Show("Please enter a floor type name.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (FloorTypes.Any(ft => string.Equals(ft.Name, typeName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("A floor type with this name already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var newType = new FloorTypeModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = typeName
            };

            FloorTypes.Add(newType);
            NewFloorTypeName = string.Empty;

            if (FloorTypes.Count == 1)
            {
                SelectedFloorType = newType;
                foreach (var level in LevelCollection)
                {
                    level.SelectedFloorType = newType;
                }
            }

            UpdateFloorTypeViewMappings();
        }

        private bool CanAddFloorType(object parameter)
        {
            return !string.IsNullOrWhiteSpace(NewFloorTypeName);
        }

        private void RemoveFloorType(object parameter)
        {
            if (parameter is FloorTypeModel floorType)
            {
                if (FloorTypes.Count == 1)
                {
                    MessageBox.Show("Cannot remove the last floor type. At least one floor type is required.",
                                   "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int index = FloorTypes.IndexOf(floorType);
                FloorTypeModel replacementType = index > 0 ? FloorTypes[index - 1] : FloorTypes[1];

                foreach (var level in LevelCollection)
                {
                    if (level.SelectedFloorType?.Id == floorType.Id)
                    {
                        level.SelectedFloorType = replacementType;
                    }
                }

                FloorTypes.Remove(floorType);

                if (SelectedFloorType == floorType)
                {
                    SelectedFloorType = replacementType;
                }

                var mappingToRemove = FloorTypeViewMappingCollection.FirstOrDefault(m =>
                                        m.FloorTypeName == floorType.Name);
                if (mappingToRemove != null)
                {
                    FloorTypeViewMappingCollection.Remove(mappingToRemove);
                }
            }
        }

        private void Export(object parameter)
        {
            if (string.IsNullOrEmpty(OutputLocation))
            {
                MessageBox.Show("Please select an output file first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Create export options using the new simplified approach
                var options = CreateExportOptions();

                // Validate options
                ValidateExportOptions(options);

                // Create and execute the unified exporter
                var elementFilters = GetElementFilters();
                var materialFilters = GetMaterialFilters();
                var exporter = new UnifiedExporter(_document, elementFilters, materialFilters);

                var result = exporter.Export(options);

                if (result.Success)
                {
                    MessageBox.Show(result.GetSummary(), "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    RequestClose?.Invoke();
                }
                else
                {
                    MessageBox.Show($"Export failed: {result.ErrorMessage}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during export: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExport(object parameter)
        {
            if (string.IsNullOrEmpty(OutputLocation))
                return false;

            // For grids-only export, just need output location
            bool isGridsOnlyExport = ExportGrids && !ExportBeams && !ExportColumns &&
                                   !ExportWalls && !ExportFloors && !ExportBraces && !ExportFootings;

            return isGridsOnlyExport || BaseLevel != null;
        }
        #endregion

        #region Helper Methods
        private ExportOptions CreateExportOptions()
        {
            var selectedLevels = LevelCollection.Where(level => level.IsSelected).ToList();

            var options = new ExportOptions
            {
                OutputPath = OutputLocation,
                Format = GetExportFormat(),
                SelectedLevels = selectedLevels.Select(l => l.LevelId).Where(id => id != null).ToList(),
                BaseLevel = GetRevitBaseLevel(),
                RotationAngle = ApplyRotation ? RotationAngle : 0,
                ElementFilters = GetElementFilters(),
                MaterialFilters = GetMaterialFilters(),
                SaveDebugFiles = true
            };

            // Add custom floor types and levels for RAM/Grasshopper
            if (IsFloorLayoutEnabled && FloorTypes.Count > 0)
            {
                options.CustomFloorTypes = FloorTypes.Select(ft => new Core.Models.ModelLayout.FloorType
                {
                    Id = ft.Id,
                    Name = ft.Name
                }).ToList();

                options.CustomLevels = ConvertToCoreLevels(selectedLevels);
            }

            // Add view mappings for Grasshopper
            if (ExportToGrasshopper)
            {
                options.FloorTypeToViewMap = new Dictionary<string, ElementId>();
                foreach (var mapping in FloorTypeViewMappingCollection)
                {
                    if (mapping.SelectedViewPlan != null)
                    {
                        var floorType = FloorTypes.FirstOrDefault(ft => ft.Name == mapping.FloorTypeName);
                        if (floorType != null)
                        {
                            options.FloorTypeToViewMap[floorType.Id] = mapping.SelectedViewPlan.ViewId;
                        }
                    }
                }
            }

            return options;
        }

        private void ValidateExportOptions(ExportOptions options)
        {
            var selectedLevels = LevelCollection.Where(level => level.IsSelected).ToList();

            if (!selectedLevels.Any())
            {
                throw new InvalidOperationException("Please select at least one level to export.");
            }

            if (IsFloorLayoutEnabled)
            {
                foreach (var level in selectedLevels)
                {
                    if (level.SelectedFloorType == null)
                    {
                        throw new InvalidOperationException($"Please assign a floor type to level: {level.Name}");
                    }
                }

                if (ExportToGrasshopper)
                {
                    foreach (var floorTypeMapping in FloorTypeViewMappingCollection)
                    {
                        if (floorTypeMapping.SelectedViewPlan == null)
                        {
                            throw new InvalidOperationException($"Please assign a view plan to floor type: {floorTypeMapping.FloorTypeName}");
                        }
                    }
                }
            }
        }

        private ExportFormat GetExportFormat()
        {
            if (ExportToETABS) return ExportFormat.ETABS;
            if (ExportToRAM) return ExportFormat.RAM;
            return ExportFormat.Grasshopper;
        }

        private Level GetRevitBaseLevel()
        {
            if (BaseLevel?.LevelId == null) return null;
            return _document.GetElement(BaseLevel.LevelId) as Level;
        }

        private List<Core.Models.ModelLayout.Level> ConvertToCoreLevels(List<LevelViewModel> levels)
        {
            return levels.Select(level => new Core.Models.ModelLayout.Level
            {
                Name = level.Name,
                Elevation = level.Elevation * 12.0, // Convert feet to inches
                FloorTypeId = level.SelectedFloorType?.Id
            }).ToList();
        }

        private Dictionary<string, bool> GetElementFilters()
        {
            return new Dictionary<string, bool>
            {
                { "Grids", ExportGrids },
                { "Beams", ExportBeams },
                { "Braces", ExportBraces },
                { "Columns", ExportColumns },
                { "Floors", ExportFloors },
                { "Walls", ExportWalls },
                { "Footings", ExportFootings }
            };
        }

        private Dictionary<string, bool> GetMaterialFilters()
        {
            return new Dictionary<string, bool>
            {
                { "Steel", ExportSteel },
                { "Concrete", ExportConcrete }
            };
        }

        private void UpdateFloorTypeViewMappings()
        {
            FloorTypeViewMappingCollection.Clear();

            foreach (var floorType in FloorTypes)
            {
                var mapping = new FloorTypeViewMappingModel
                {
                    FloorTypeName = floorType.Name,
                    FloorTypeId = floorType.Id,
                    SelectedViewPlan = ViewPlanCollection.FirstOrDefault()
                };

                FloorTypeViewMappingCollection.Add(mapping);
            }
        }

        private void UpdateLevelEnabledStates()
        {
            if (BaseLevel == null || LevelCollection == null) return;

            foreach (var level in LevelCollection)
            {
                level.IsEnabledForExport = level.Elevation >= BaseLevel.Elevation;

                if (!level.IsEnabledForExport && level.IsSelected)
                {
                    level.IsSelected = false;
                }
            }

            UpdateMasterStoryLevels();
        }

        private void UpdateMasterStoryLevels()
        {
            MasterStoryLevels.Clear();

            foreach (var level in LevelCollection)
            {
                if (level.IsMasterStory && level.IsSelected)
                {
                    MasterStoryLevels.Add(level);
                }
            }

            foreach (var level in LevelCollection)
            {
                if (level.SimilarToLevel != null && !MasterStoryLevels.Contains(level.SimilarToLevel))
                {
                    level.SimilarToLevel = null;
                }
            }
        }

        private void LevelViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LevelViewModel.IsMasterStory) ||
                e.PropertyName == nameof(LevelViewModel.IsSelected) ||
                e.PropertyName == "MasterStoryCollectionChanged")
            {
                UpdateMasterStoryLevels();
            }
        }

        private void FilterLevelCollection()
        {
            _filteredLevelCollection?.Refresh();
        }

        private bool LevelViewFilter(object item)
        {
            if (string.IsNullOrEmpty(LevelSearchText))
                return true;

            var level = (LevelViewModel)item;
            return level.Name.IndexOf(LevelSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        #endregion

        #region Events
        public event Action RequestClose;
        public event Action RequestRestore;
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            if (propertyName == nameof(OutputLocation) || propertyName == nameof(LevelCollection))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion
    }
}