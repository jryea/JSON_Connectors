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
using Revit.Models;
using Core.Models;
using CG = Core.Models.Geometry;
using Core.Utilities;

namespace Revit.ViewModels
{
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
        private string _viewSearchText;
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

        public bool ExportToETABS
        {
            get => _exportToETABS;
            set
            {
                _exportToETABS = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFloorLayoutEnabled));
                OnPropertyChanged(nameof(IsAnalysisExport));
            }
        }

        public bool ExportToRAM
        {
            get => _exportToRAM;
            set
            {
                _exportToRAM = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFloorLayoutEnabled));
                OnPropertyChanged(nameof(IsAnalysisExport));
            }
        }

        public bool ExportToGrasshopper
        {
            get => _exportToGrasshopper;
            set
            {
                _exportToGrasshopper = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFloorLayoutEnabled));
                OnPropertyChanged(nameof(IsAnalysisExport));
            }
        }

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

        public string ViewSearchText
        {
            get => _viewSearchText;
            set
            {
                _viewSearchText = value;
                OnPropertyChanged();
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
        public bool IsAnalysisExport => ExportToETABS || ExportToRAM;

        // Element Categories
        public bool ExportGrids
        {
            get => _exportGrids;
            set
            {
                _exportGrids = value;
                OnPropertyChanged();
            }
        }

        public bool ExportBeams
        {
            get => _exportBeams;
            set
            {
                _exportBeams = value;
                OnPropertyChanged();
            }
        }

        public bool ExportBraces
        {
            get => _exportBraces;
            set
            {
                _exportBraces = value;
                OnPropertyChanged();
            }
        }

        public bool ExportColumns
        {
            get => _exportColumns;
            set
            {
                _exportColumns = value;
                OnPropertyChanged();
            }
        }

        public bool ExportFloors
        {
            get => _exportFloors;
            set
            {
                _exportFloors = value;
                OnPropertyChanged();
            }
        }

        public bool ExportWalls
        {
            get => _exportWalls;
            set
            {
                _exportWalls = value;
                OnPropertyChanged();
            }
        }

        public bool ExportFootings
        {
            get => _exportFootings;
            set
            {
                _exportFootings = value;
                OnPropertyChanged();
            }
        }

        // Material Types
        public bool ExportSteel
        {
            get => _exportSteel;
            set
            {
                _exportSteel = value;
                OnPropertyChanged();
            }
        }

        public bool ExportConcrete
        {
            get => _exportConcrete;
            set
            {
                _exportConcrete = value;
                OnPropertyChanged();
            }
        }

        // Rotation Properties
        public bool ApplyRotation
        {
            get => _applyRotation;
            set
            {
                _applyRotation = value;
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

        public ICollectionView FilteredLevelCollection => _filteredLevelCollection;
        #endregion

        #region Commands
        public ICommand BrowseOutputCommand { get; private set; }
        public ICommand AddFloorTypeCommand { get; private set; }
        public ICommand RemoveFloorTypeCommand { get; private set; }
        public ICommand ExportCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        #endregion

        #region Constructors
        // Constructor for designer time
        public ExportStructuralModelViewModel()
        {
            InitializeProperties();
            InitializeCommands();

            // Add sample data for design time
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                AddSampleData();
            }
        }

        // Constructor for runtime with Revit API
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
            // Initialize collections first
            FloorTypes = new ObservableCollection<FloorTypeModel>();
            LevelCollection = new ObservableCollection<LevelViewModel>();
            ViewPlanCollection = new ObservableCollection<ViewPlanViewModel>();
            FloorTypeViewMappingCollection = new ObservableCollection<FloorTypeViewMappingModel>();
            MasterStoryLevels = new ObservableCollection<LevelViewModel>();

            // Initialize search text
            LevelSearchText = string.Empty;
            ViewSearchText = string.Empty;

            // Set default state
            ExportToETABS = true;
            ExportToRAM = false;
            ExportToGrasshopper = false;

            // Initialize the filtered views
            _filteredLevelCollection = CollectionViewSource.GetDefaultView(LevelCollection);
            _filteredLevelCollection.Filter = LevelViewFilter;
        }

        private void InitializeCommands()
        {
            BrowseOutputCommand = new RelayCommand(BrowseOutput);
            AddFloorTypeCommand = new RelayCommand(AddFloorType, CanAddFloorType);
            RemoveFloorTypeCommand = new RelayCommand(RemoveFloorType);
            ExportCommand = new RelayCommand(DoExport, CanExport);
            CancelCommand = new RelayCommand(obj => RequestClose?.Invoke());
        }
        #endregion

        #region Data Loading
        private void AddSampleData()
        {
            // Add sample floor types
            FloorTypes.Add(new FloorTypeModel { Name = "Concrete Slab" });
            FloorTypes.Add(new FloorTypeModel { Name = "Metal Deck" });

            // Add sample levels
            LevelCollection.Add(new LevelViewModel
            {
                Id = "level1",
                Name = "Level 1",
                Elevation = 0.0,
                IsSelected = true,
                SelectedFloorType = FloorTypes[0]
            });

            LevelCollection.Add(new LevelViewModel
            {
                Id = "level2",
                Name = "Level 2",
                Elevation = 144.0,
                IsSelected = true,
                SelectedFloorType = FloorTypes[1]
            });

            // Add sample view plans
            ViewPlanCollection.Add(new ViewPlanViewModel
            {
                Id = "view1",
                Name = "First Floor Plan"
            });

            ViewPlanCollection.Add(new ViewPlanViewModel
            {
                Id = "view2",
                Name = "Second Floor Plan"
            });

            // Add sample floor type view mappings
            FloorTypeViewMappingCollection.Add(new FloorTypeViewMappingModel
            {
                FloorTypeName = "Concrete Slab",
                SelectedViewPlan = ViewPlanCollection[0]
            });

            FloorTypeViewMappingCollection.Add(new FloorTypeViewMappingModel
            {
                FloorTypeName = "Metal Deck",
                SelectedViewPlan = ViewPlanCollection[1]
            });
        }

        private void LoadLevels()
        {
            // Clear existing items
            LevelCollection.Clear();
            MasterStoryLevels.Clear();

            try
            {
                // Get all levels from Revit
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
                        Id = level.Id.IntegerValue.ToString(),
                        Name = level.Name,
                        Elevation = level.Elevation,
                        LevelId = level.Id,
                        IsSelected = true,
                        IsEnabledForExport = true
                    };

                    LevelCollection.Add(levelViewModel);
                    MasterStoryLevels.Add(levelViewModel);
                }

                // Set the first level as base level by default
                if (LevelCollection.Any())
                {
                    BaseLevel = LevelCollection.First();
                }
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
                    .Cast<ViewPlan>()
                    .Where(vp => vp.ViewType == ViewType.FloorPlan)
                    .OrderBy(vp => vp.Name)
                    .ToList();

                foreach (var viewPlan in viewPlans)
                {
                    ViewPlanCollection.Add(new ViewPlanViewModel
                    {
                        Id = viewPlan.Id.IntegerValue.ToString(),
                        Name = viewPlan.Name,
                        ViewId = viewPlan.Id
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading view plans: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateFloorTypeViewMappings()
        {
            FloorTypeViewMappingCollection.Clear();

            foreach (var floorType in FloorTypes)
            {
                FloorTypeViewMappingCollection.Add(new FloorTypeViewMappingModel
                {
                    FloorTypeName = floorType.Name,
                    SelectedViewPlan = ViewPlanCollection.FirstOrDefault()
                });
            }
        }
        #endregion

        #region UI Interaction
        private void BrowseOutput(object parameter)
        {
            try
            {
                string filter = "";
                string defaultExt = "";

                if (ExportToETABS)
                {
                    filter = "ETABS Files (*.e2k)|*.e2k";
                    defaultExt = ".e2k";
                }
                else if (ExportToRAM)
                {
                    filter = "RAM Files (*.rss)|*.rss";
                    defaultExt = ".rss";
                }
                else if (ExportToGrasshopper)
                {
                    filter = "JSON Files (*.json)|*.json";
                    defaultExt = ".json";
                }

                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Title = "Save Export File",
                    Filter = filter,
                    DefaultExt = defaultExt,
                    FileName = _document?.Title ?? "StructuralModel"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    OutputLocation = saveDialog.FileName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error browsing for output location: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddFloorType(object parameter)
        {
            if (!string.IsNullOrWhiteSpace(NewFloorTypeName))
            {
                var newFloorType = new FloorTypeModel { Name = NewFloorTypeName.Trim() };
                FloorTypes.Add(newFloorType);

                // Add corresponding mapping
                FloorTypeViewMappingCollection.Add(new FloorTypeViewMappingModel
                {
                    FloorTypeName = newFloorType.Name,
                    SelectedViewPlan = ViewPlanCollection.FirstOrDefault()
                });

                NewFloorTypeName = string.Empty;
            }
        }

        private bool CanAddFloorType(object parameter)
        {
            return !string.IsNullOrWhiteSpace(NewFloorTypeName) &&
                   !FloorTypes.Any(ft => ft.Name.Equals(NewFloorTypeName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private void RemoveFloorType(object parameter)
        {
            if (SelectedFloorType != null)
            {
                // Remove from both collections
                var mappingToRemove = FloorTypeViewMappingCollection
                    .FirstOrDefault(m => m.FloorTypeName == SelectedFloorType.Name);
                if (mappingToRemove != null)
                {
                    FloorTypeViewMappingCollection.Remove(mappingToRemove);
                }

                FloorTypes.Remove(SelectedFloorType);
                SelectedFloorType = null;
            }
        }

        private void FilterLevelCollection()
        {
            _filteredLevelCollection?.Refresh();
        }

        private bool LevelViewFilter(object obj)
        {
            if (obj is LevelViewModel level)
            {
                if (string.IsNullOrEmpty(LevelSearchText))
                    return true;

                return level.Name.ToLower().Contains(LevelSearchText.ToLower());
            }
            return false;
        }

        private void UpdateLevelEnabledStates()
        {
            if (BaseLevel == null) return;

            foreach (var level in LevelCollection)
            {
                level.IsEnabledForExport = level.Elevation >= BaseLevel.Elevation;
                if (!level.IsEnabledForExport)
                {
                    level.IsSelected = false;
                }
            }
        }
        #endregion

        #region Export Logic
        private void DoExport(object parameter)
        {
            if (string.IsNullOrEmpty(OutputLocation))
            {
                MessageBox.Show("Please specify an output location.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Get selected levels
                var selectedLevels = LevelCollection
                    .Where(level => level.IsSelected)
                    .ToList();

                if (!selectedLevels.Any())
                {
                    MessageBox.Show("Please select at least one level to export.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Prepare export data
                var exportData = PrepareExportData(selectedLevels);

                // Step 1: Create clean model using StructuralModelExporter directly
                var context = StructuralModelExporter.CreateContext(_document,
                    exportData.ElementFilters,
                    exportData.MaterialFilters,
                    exportData.SelectedLevelIds,
                    BaseLevel?.LevelId,
                    exportData.CustomFloorTypes,
                    exportData.CustomLevels);

                var exporter = new StructuralModelExporter();
                var model = exporter.Export(context);

                // Step 2: Save pre-transform JSON (debug file)
                string preTransformPath = Path.ChangeExtension(OutputLocation, ".json");
                Core.Converters.JsonConverter.SaveToFile(model, preTransformPath);

                // Step 3: Apply rotation if enabled
                if (ApplyRotation && Math.Abs(RotationAngle) > 0.001)
                {
                    var rotationCenter = CalculateModelCenter(model);
                    ModelTransformation.RotateModel(model, RotationAngle, rotationCenter);

                    // Save post-transform JSON (debug file)
                    string directory = Path.GetDirectoryName(OutputLocation);
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(OutputLocation);
                    string postTransformPath = Path.Combine(directory, fileNameWithoutExtension + "-transformed.json");
                    Core.Converters.JsonConverter.SaveToFile(model, postTransformPath);
                }

                // Step 4: Handle format-specific export
                if (ExportToGrasshopper)
                {
                    HandleGrasshopperExport(model, exportData);
                }
                else
                {
                    HandleAnalysisExport(model);
                }

                MessageBox.Show($"Successfully exported to {OutputLocation}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                RequestClose?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during export: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HandleGrasshopperExport(Core.Models.BaseModel model, ExportData exportData)
        {
            // Save final JSON for Grasshopper
            Core.Converters.JsonConverter.SaveToFile(model, OutputLocation);

            // Export CAD files if floor type mappings exist
            if (exportData.FloorTypeToViewMap != null && exportData.FloorTypeToViewMap.Any())
            {
                var grasshopperExporter = new GrasshopperExporter(_document, _uiApp);

                // Create CAD folder
                string dwgFolder = Path.Combine(Path.GetDirectoryName(OutputLocation), "CAD");
                Directory.CreateDirectory(dwgFolder);

                grasshopperExporter.ExportWithFloorTypeViewMappings(
                    OutputLocation,
                    dwgFolder,
                    exportData.CustomFloorTypes ?? new List<Core.Models.ModelLayout.FloorType>(),
                    exportData.CustomLevels ?? new List<Core.Models.ModelLayout.Level>(),
                    null, // No reference point needed
                    exportData.FloorTypeToViewMap,
                    exportData.SelectedLevelIds
                );
            }
        }

        private void HandleAnalysisExport(Core.Models.BaseModel model)
        {
            // Create temporary JSON file
            string tempJsonPath = Path.Combine(Path.GetDirectoryName(OutputLocation),
                Path.GetFileNameWithoutExtension(OutputLocation) + "_temp.json");
            Core.Converters.JsonConverter.SaveToFile(model, tempJsonPath);

            try
            {
                // Convert to target format
                if (ExportToETABS)
                {
                    ConvertToETABS(tempJsonPath);
                }
                else if (ExportToRAM)
                {
                    ConvertToRAM(tempJsonPath);
                }

                // Copy debug JSON to final location (for non-Grasshopper exports)
                string debugJsonPath = Path.ChangeExtension(OutputLocation, ".json");
                File.Copy(tempJsonPath, debugJsonPath, true);
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempJsonPath))
                {
                    File.Delete(tempJsonPath);
                }
            }
        }

        private void ConvertToETABS(string jsonPath)
        {
            string jsonContent = File.ReadAllText(jsonPath);
            var etabsConverter = new ETABS.ETABSImport();
            string e2kContent = etabsConverter.ProcessModel(jsonContent);
            File.WriteAllText(OutputLocation, e2kContent);
        }

        private void ConvertToRAM(string jsonPath)
        {
            string jsonContent = File.ReadAllText(jsonPath);
            var ramConverter = new RAM.RAMImporter();
            var result = ramConverter.ConvertJSONStringToRAM(jsonContent, OutputLocation);

            if (!result.Success)
            {
                throw new Exception($"RAM conversion failed: {result.Message}");
            }
        }

        private ExportData PrepareExportData(List<LevelViewModel> selectedLevels)
        {
            var exportData = new ExportData();

            // Create filter dictionaries
            exportData.ElementFilters = new Dictionary<string, bool>
            {
                { "Grids", ExportGrids },
                { "Beams", ExportBeams },
                { "Braces", ExportBraces },
                { "Columns", ExportColumns },
                { "Floors", ExportFloors },
                { "Walls", ExportWalls },
                { "Footings", ExportFootings }
            };

            exportData.MaterialFilters = new Dictionary<string, bool>
            {
                { "Steel", ExportSteel },
                { "Concrete", ExportConcrete }
            };

            // Get selected level IDs
            exportData.SelectedLevelIds = selectedLevels.Select(l => l.LevelId).ToList();

            // Create custom floor types if we have any
            if (FloorTypes.Any())
            {
                exportData.CustomFloorTypes = FloorTypes.Select(ft => new Core.Models.ModelLayout.FloorType(ft.Name)).ToList();
            }

            // Create custom levels with floor type assignments
            if (selectedLevels.Any() && FloorTypes.Any())
            {
                exportData.CustomLevels = new List<Core.Models.ModelLayout.Level>();

                for (int i = 0; i < selectedLevels.Count; i++)
                {
                    var levelVm = selectedLevels[i];
                    var floorType = levelVm.SelectedFloorType ?? FloorTypes.FirstOrDefault();

                    if (floorType != null)
                    {
                        var customFloorType = exportData.CustomFloorTypes.FirstOrDefault(ft => ft.Name == floorType.Name);
                        if (customFloorType != null)
                        {
                            var customLevel = new Core.Models.ModelLayout.Level(levelVm.Name, customFloorType.Id, levelVm.Elevation);
                            exportData.CustomLevels.Add(customLevel);
                        }
                    }
                }
            }

            // Create floor type to view mappings for Grasshopper
            if (ExportToGrasshopper && FloorTypeViewMappingCollection.Any())
            {
                exportData.FloorTypeToViewMap = new Dictionary<string, ElementId>();

                foreach (var mapping in FloorTypeViewMappingCollection)
                {
                    if (mapping.SelectedViewPlan != null)
                    {
                        exportData.FloorTypeToViewMap[mapping.FloorTypeName] = mapping.SelectedViewPlan.ViewId;
                    }
                }
            }

            return exportData;
        }

        private bool CanExport(object parameter)
        {
            // Basic requirement: output location must be specified
            if (string.IsNullOrEmpty(OutputLocation))
                return false;

            // Check if we're doing a grids-only export
            bool isGridsOnlyExport = ExportGrids &&
                                   !ExportBeams &&
                                   !ExportColumns &&
                                   !ExportWalls &&
                                   !ExportFloors &&
                                   !ExportBraces &&
                                   !ExportFootings;

            // Grids-only export is always valid
            if (isGridsOnlyExport)
                return true;

            // If not a grids-only export, we just need a base level selected
            // The normal flow will create floor types for each level automatically
            return BaseLevel != null;
        }
        #endregion

        #region Helper Methods
        private CG.Point2D CalculateModelCenter(Core.Models.BaseModel model)
        {
            var allPoints = new List<CG.Point2D>();

            // Collect points from grids
            if (model.ModelLayout?.Grids != null)
            {
                foreach (var grid in model.ModelLayout.Grids)
                {
                    if (grid.StartPoint != null) allPoints.Add(new CG.Point2D(grid.StartPoint.X, grid.StartPoint.Y));
                    if (grid.EndPoint != null) allPoints.Add(new CG.Point2D(grid.EndPoint.X, grid.EndPoint.Y));
                }
            }

            // Collect points from elements
            if (model.Elements != null)
            {
                if (model.Elements.Beams != null)
                    foreach (var beam in model.Elements.Beams)
                    {
                        if (beam.StartPoint != null) allPoints.Add(beam.StartPoint);
                        if (beam.EndPoint != null) allPoints.Add(beam.EndPoint);
                    }

                if (model.Elements.Columns != null)
                    foreach (var column in model.Elements.Columns)
                    {
                        if (column.StartPoint != null) allPoints.Add(column.StartPoint);
                    }
            }

            // Return geometric center or origin if no points found
            if (allPoints.Count == 0)
                return new CG.Point2D(0, 0);

            double centerX = allPoints.Average(p => p.X);
            double centerY = allPoints.Average(p => p.Y);

            return new CG.Point2D(centerX, centerY);
        }
        #endregion

        #region Events
        public event Action RequestClose;
        public event Action RequestMinimize;
        public event Action RequestRestore;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            // Update command states when properties change
            if (propertyName == nameof(OutputLocation) ||
                propertyName == nameof(LevelCollection))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion

        #region Helper Classes
        private class ExportData
        {
            public Dictionary<string, bool> ElementFilters { get; set; }
            public Dictionary<string, bool> MaterialFilters { get; set; }
            public List<ElementId> SelectedLevelIds { get; set; }
            public List<Core.Models.ModelLayout.FloorType> CustomFloorTypes { get; set; }
            public List<Core.Models.ModelLayout.Level> CustomLevels { get; set; }
            public Dictionary<string, ElementId> FloorTypeToViewMap { get; set; }
        }
        #endregion
    }
}