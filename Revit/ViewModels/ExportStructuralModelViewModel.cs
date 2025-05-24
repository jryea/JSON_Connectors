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
using CG = Core.Models.Geometry;
using Revit.Export.Models;

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
                if (_exportToETABS != value)
                {
                    _exportToETABS = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsAnalysisExport));
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
                    OnPropertyChanged(nameof(IsAnalysisExport));
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
                    OnPropertyChanged(nameof(IsAnalysisExport));
                    OnPropertyChanged(nameof(IsFloorLayoutEnabled));
                }
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

        // Helper property to show/hide analysis-specific controls (ETABS or RAM)
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

        #endregion

        #region Commands
        public ICommand BrowseOutputCommand { get; private set; }
        public ICommand AddFloorTypeCommand { get; private set; }
        public ICommand RemoveFloorTypeCommand { get; private set; }
        public ICommand ExportCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        #endregion

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
            ExportCommand = new RelayCommand(Export, CanExport);
            CancelCommand = new RelayCommand(obj => RequestClose?.Invoke());
        }

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
                        Id = level.UniqueId,
                        Name = level.Name,
                        // Store elevation in feet for UI display purposes only
                        // Revit provides it in feet already, so no conversion needed here
                        Elevation = Math.Round(level.Elevation, 2)
                    };

                    levelViewModel.LevelId = level.Id;
                    levelViewModel.IsSelected = false; // Set to false by default
                    levelViewModel.IsEnabledForExport = true; // Enabled by default
                    levelViewModel.IsMasterStory = false; // Not a master story by default
                    levelViewModel.SimilarToLevel = null; // No similar level by default

                    // Subscribe to property changes
                    levelViewModel.PropertyChanged += LevelViewModel_PropertyChanged;

                    LevelCollection.Add(levelViewModel);
                }

                // Set the default base level (lowest level)
                if (LevelCollection.Count > 0)
                {
                    BaseLevel = LevelCollection.OrderBy(l => l.Elevation).FirstOrDefault();
                }

                // Update enabled states based on base level
                UpdateLevelEnabledStates();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading levels: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LevelViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // When a level's Master Story status changes, update the MasterStoryLevels collection
            if (e.PropertyName == nameof(LevelViewModel.IsMasterStory) ||
                e.PropertyName == nameof(LevelViewModel.IsSelected) ||
                e.PropertyName == "MasterStoryCollectionChanged")
            {
                UpdateMasterStoryLevels();
            }
        }

        private void LoadViewPlans()
        {
            // Clear existing items
            ViewPlanCollection.Clear();

            try
            {
                // Get all floor plan, ceiling plan, and engineering plan views from Revit
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

        private void UpdateFloorTypeViewMappings()
        {
            // Clear existing mappings
            FloorTypeViewMappingCollection.Clear();

            // Create a mapping for each floor type
            foreach (var floorType in FloorTypes)
            {
                var mapping = new FloorTypeViewMappingModel
                {
                    FloorTypeName = floorType.Name,
                    FloorTypeId = floorType.Id, // This is a temporary ID that will be updated when exporting
                    SelectedViewPlan = ViewPlanCollection.FirstOrDefault() // Set first view plan as default
                };

                FloorTypeViewMappingCollection.Add(mapping);
            }
        }

        private void UpdateLevelEnabledStates()
        {
            if (BaseLevel == null || LevelCollection == null)
                return;

            // Update the IsEnabledForExport property based on base level
            foreach (var level in LevelCollection)
            {
                // If level is below base level, disable it
                level.IsEnabledForExport = level.Elevation >= BaseLevel.Elevation;

                // If level is below base level and was selected, deselect it
                if (!level.IsEnabledForExport && level.IsSelected)
                {
                    level.IsSelected = false;
                }
            }

            // Update master story levels collection
            UpdateMasterStoryLevels();
        }

        private void UpdateMasterStoryLevels()
        {
            MasterStoryLevels.Clear();

            // Add master story levels to the collection
            foreach (var level in LevelCollection)
            {
                if (level.IsMasterStory && level.IsSelected)
                {
                    MasterStoryLevels.Add(level);
                }
            }

            // Handle "Similar To" references when a master story is deselected
            foreach (var level in LevelCollection)
            {
                if (level.SimilarToLevel != null && !MasterStoryLevels.Contains(level.SimilarToLevel))
                {
                    level.SimilarToLevel = null; // Reset reference if the master story is no longer available
                }
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

        private void BrowseOutput(object parameter)
        {
            try
            {
                // Determine the file extension based on selected export format
                string extension = ".json"; // Default for Grasshopper
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

                // Get default filename from document title
                string defaultFileName = _document?.Title ?? "Export";

                // Remove any existing extension
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
                    // Save the path without extension - we'll add it during export
                    string selectedPath = dialog.FileName;

                    // If user manually entered an extension that doesn't match the selected format,
                    // strip it so we can apply the correct one at export time
                    string fileExt = Path.GetExtension(selectedPath).ToLowerInvariant();
                    if (fileExt != extension)
                    {
                        selectedPath = Path.GetFileNameWithoutExtension(selectedPath);
                        selectedPath = Path.Combine(Path.GetDirectoryName(dialog.FileName), selectedPath);
                    }

                    OutputLocation = selectedPath;
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

            // Check if name already exists
            if (FloorTypes.Any(ft => string.Equals(ft.Name, typeName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("A floor type with this name already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Add new floor type
            var newType = new FloorTypeModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = typeName
            };

            FloorTypes.Add(newType);
            NewFloorTypeName = string.Empty;

            // Set as selected floor type if it's the first one added
            if (FloorTypes.Count == 1)
            {
                SelectedFloorType = newType;
            }

            // Update floor type-view mappings
            UpdateFloorTypeViewMappings();

            // Update level floor type assignments if needed
            if (FloorTypes.Count == 1)
            {
                // This is the first floor type, assign it to all levels
                foreach (var level in LevelCollection)
                {
                    level.SelectedFloorType = newType;
                }
            }
        }

        private bool CanAddFloorType(object parameter)
        {
            return !string.IsNullOrWhiteSpace(NewFloorTypeName);
        }

        private void RemoveFloorType(object parameter)
        {
            if (parameter is FloorTypeModel floorType)
            {
                // Get the index of the floor type being removed
                int index = FloorTypes.IndexOf(floorType);

                // Check if this is the last floor type
                if (FloorTypes.Count == 1)
                {
                    MessageBox.Show("Cannot remove the last floor type. At least one floor type is required.",
                                   "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Find a replacement floor type for levels using this one
                FloorTypeModel replacementType = null;
                if (index > 0)
                {
                    replacementType = FloorTypes[index - 1]; // Use previous type
                }
                else if (FloorTypes.Count > 1)
                {
                    replacementType = FloorTypes[1]; // Use next type
                }

                // Update any levels using this floor type
                foreach (var level in LevelCollection)
                {
                    if (level.SelectedFloorType?.Id == floorType.Id)
                    {
                        level.SelectedFloorType = replacementType;
                    }
                }

                // Remove the floor type
                FloorTypes.Remove(floorType);

                // Update SelectedFloorType if needed
                if (SelectedFloorType == floorType)
                {
                    SelectedFloorType = replacementType;
                }

                // Remove corresponding floor type-view mapping
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
                // Get selected levels
                var selectedLevels = LevelCollection
                    .Where(level => level.IsSelected)
                    .ToList();

                if (!selectedLevels.Any())
                {
                    MessageBox.Show("Please select at least one level to export.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Prepare common export data
                var exportData = PrepareExportData(selectedLevels);

                // Step 1: Create uniform JSON
                string tempJsonPath = CreateUniformJson(exportData);

                // Step 2: Apply rotation if enabled
                if (ApplyRotation && Math.Abs(RotationAngle) > 0.001)
                {
                    ApplyModelRotation(tempJsonPath);
                }

                // Step 3: Convert to target format
                ConvertToTargetFormat(tempJsonPath, exportData);

                MessageBox.Show($"Successfully exported to {OutputLocation}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                // Close the dialog
                RequestClose?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during export: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            exportData.SelectedLevelIds = selectedLevels
                .Select(level => level.LevelId)
                .Where(id => id != null)
                .ToList();

            // Prepare custom floor types and levels for RAM/Grasshopper
            if (FloorTypes.Count > 0 && (ExportToGrasshopper || ExportToRAM))
            {
                // Validate floor type assignments
                foreach (var level in selectedLevels)
                {
                    if (level.SelectedFloorType == null)
                    {
                        throw new InvalidOperationException($"Please assign a floor type to level: {level.Name}");
                    }
                }

                // Convert to core model format
                exportData.CustomFloorTypes = FloorTypes.Select(ft => new Core.Models.ModelLayout.FloorType
                {
                    Id = ft.Id,
                    Name = ft.Name
                }).ToList();

                exportData.CustomLevels = ConvertToCoreLevelsWithBaseProcessing(selectedLevels);

                // Add Base floor type if needed
                if (BaseLevel != null)
                {
                    var baseFloorType = new Core.Models.ModelLayout.FloorType
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Base"
                    };
                    exportData.CustomFloorTypes.RemoveAll(ft => ft.Name == "Base");
                    exportData.CustomFloorTypes.Add(baseFloorType);
                }
            }

            // Prepare Grasshopper-specific data
            if (ExportToGrasshopper)
            {
                exportData.FloorTypeToViewMap = new Dictionary<string, ElementId>();
                foreach (var mapping in FloorTypeViewMappingCollection)
                {
                    if (mapping.SelectedViewPlan != null)
                    {
                        var floorType = FloorTypes.FirstOrDefault(ft => ft.Name == mapping.FloorTypeName);
                        if (floorType != null)
                        {
                            exportData.FloorTypeToViewMap[floorType.Id] = mapping.SelectedViewPlan.ViewId;
                        }
                    }
                }
            }

            return exportData;
        }

        private string CreateUniformJson(ExportData exportData)
        {
            string tempJsonPath = Path.Combine(Path.GetDirectoryName(OutputLocation),
                Path.GetFileNameWithoutExtension(OutputLocation) + "_temp.json");

            if (ExportToGrasshopper)
            {
                // Create CAD folder for Grasshopper
                string dwgFolder = Path.Combine(Path.GetDirectoryName(OutputLocation), "CAD");
                Directory.CreateDirectory(dwgFolder);

                // Use GrasshopperExporter to create JSON
                GrasshopperExporter exporter = new GrasshopperExporter(_document, _uiApp);
                exporter.ExportWithFloorTypeViewMappings(
                    tempJsonPath,
                    dwgFolder,
                    exportData.CustomFloorTypes ?? new List<Core.Models.ModelLayout.FloorType>(),
                    exportData.CustomLevels ?? new List<Core.Models.ModelLayout.Level>(),
                    null, // No reference point needed
                    exportData.FloorTypeToViewMap,
                    exportData.SelectedLevelIds
                );
            }
            else
            {
                // Use ExportManager for ETABS/RAM
                ExportManager exportManager = new ExportManager(_document, _uiApp);
                exportManager.ExportToJson(tempJsonPath,
                    exportData.ElementFilters,
                    exportData.MaterialFilters,
                    exportData.SelectedLevelIds,
                    BaseLevel?.LevelId,
                    exportData.CustomFloorTypes,
                    exportData.CustomLevels);
            }

            return tempJsonPath;
        }

        // Update ExportStructuralModelViewModel.cs methods:

        private void ApplyModelRotation(string jsonPath)
        {
            try
            {
                // Save pre-transformation JSON for debugging
                string debugPath = Path.ChangeExtension(OutputLocation, ".json");
                File.Copy(jsonPath, debugPath, true);

                // Load the model from JSON
                var model = Core.Converters.JsonConverter.LoadFromFile(jsonPath);

                // Determine rotation center - use model's geometric center for better results
                var rotationCenter = CalculateModelCenter(model);

                // Apply rotation
                Core.Models.ModelTransformation.RotateModel(model, RotationAngle, rotationCenter);

                // Save the rotated model back to JSON
                Core.Converters.JsonConverter.SaveToFile(model, jsonPath);

                // Save post-transformation JSON for debugging
                string transformedPath = Path.ChangeExtension(OutputLocation, "-transformed.json");
                File.Copy(jsonPath, transformedPath, true);

                System.Diagnostics.Debug.WriteLine($"Applied {RotationAngle}° rotation around center ({rotationCenter.X:F2}, {rotationCenter.Y:F2})");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error applying rotation: {ex.Message}", ex);
            }
        }

        private void ConvertToTargetFormat(string tempJsonPath, ExportData exportData)
        {
            // Save debug JSON files for non-Grasshopper exports
            if (!ExportToGrasshopper)
            {
                string debugJsonPath = Path.ChangeExtension(OutputLocation, ".json");
                File.Copy(tempJsonPath, debugJsonPath, true);
            }

            if (ExportToETABS)
            {
                // Convert JSON to ETABS E2K format
                string jsonContent = File.ReadAllText(tempJsonPath);
                var converter = new ETABS.ETABSImport();
                string e2kContent = converter.ProcessModel(jsonContent);
                File.WriteAllText(OutputLocation, e2kContent);
            }
            else if (ExportToRAM)
            {
                // Convert JSON to RAM format
                RAM.RAMImporter ramImporter = new RAM.RAMImporter();
                var conversionResult = ramImporter.ConvertJSONFileToRAM(tempJsonPath, OutputLocation);

                if (!conversionResult.Success)
                {
                    throw new Exception($"Error converting to RAM format: {conversionResult.Message}");
                }
            }
            else if (ExportToGrasshopper)
            {
                // For Grasshopper, just move the JSON to final location
                File.Move(tempJsonPath, OutputLocation);
            }

            // Clean up temp file if it still exists
            if (File.Exists(tempJsonPath) && tempJsonPath != OutputLocation)
            {
                File.Delete(tempJsonPath);
            }
        }

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

        private CG.Point2D GetGridIntersectionPoint(Core.Models.BaseModel model, string grid1Name, string grid2Name)
        {
            if (model.ModelLayout?.Grids == null)
                return null;

            var grid1 = model.ModelLayout.Grids.FirstOrDefault(g => g.Name == grid1Name);
            var grid2 = model.ModelLayout.Grids.FirstOrDefault(g => g.Name == grid2Name);

            if (grid1 == null || grid2 == null)
                return null;

            return CalculateLineIntersection(grid1, grid2);
        }

            private CG.Point2D CalculateLineIntersection(Core.Models.ModelLayout.Grid grid1, Core.Models.ModelLayout.Grid grid2)
        {
            // Grid 1 line: (x1,y1) to (x2,y2)
            double x1 = grid1.StartPoint.X, y1 = grid1.StartPoint.Y;
            double x2 = grid1.EndPoint.X, y2 = grid1.EndPoint.Y;

            // Grid 2 line: (x3,y3) to (x4,y4)  
            double x3 = grid2.StartPoint.X, y3 = grid2.StartPoint.Y;
            double x4 = grid2.EndPoint.X, y4 = grid2.EndPoint.Y;

            // Calculate intersection using line intersection formula
            double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);

            if (Math.Abs(denom) < 1e-10) // Lines are parallel
                return null;

            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;

            double intersectionX = x1 + t * (x2 - x1);
            double intersectionY = y1 + t * (y2 - y1);

            return new CG.Point2D(intersectionX, intersectionY);
        }

        // Import-specific transformation method
        public static void ApplyImportTransformation(Core.Models.BaseModel model, string grid1Name, string grid2Name,
            double rotationAngle, CG.Point3D translation)
        {
            if (model == null) return;

            // Only apply transforms if grid intersection is defined
            var intersectionPoint = GetGridIntersectionPointStatic(model, grid1Name, grid2Name);
            if (intersectionPoint == null) return;

            // Apply rotation around grid intersection
            if (Math.Abs(rotationAngle) > 0.001)
            {
                Core.Models.ModelTransformation.RotateModel(model, rotationAngle, intersectionPoint);
            }

            // Apply translation
            if (Math.Abs(translation.X) > 0.001 || Math.Abs(translation.Y) > 0.001 || Math.Abs(translation.Z) > 0.001)
            {
                Core.Models.ModelTransformation.TranslateModel(model, translation);
            }
        }

        private static CG.Point2D GetGridIntersectionPointStatic(Core.Models.BaseModel model, string grid1Name, string grid2Name)
        {
            if (model.ModelLayout?.Grids == null || string.IsNullOrEmpty(grid1Name) || string.IsNullOrEmpty(grid2Name))
                return null;

            var grid1 = model.ModelLayout.Grids.FirstOrDefault(g => g.Name == grid1Name);
            var grid2 = model.ModelLayout.Grids.FirstOrDefault(g => g.Name == grid2Name);

            if (grid1 == null || grid2 == null)
                return null;

            return CalculateLineIntersectionStatic(grid1, grid2);
        }

        private static CG.Point2D CalculateLineIntersectionStatic(Core.Models.ModelLayout.Grid grid1, Core.Models.ModelLayout.Grid grid2)
        {
            double x1 = grid1.StartPoint.X, y1 = grid1.StartPoint.Y;
            double x2 = grid1.EndPoint.X, y2 = grid1.EndPoint.Y;
            double x3 = grid2.StartPoint.X, y3 = grid2.StartPoint.Y;
            double x4 = grid2.EndPoint.X, y4 = grid2.EndPoint.Y;

            double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);

            if (Math.Abs(denom) < 1e-10)
                return null;

            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;

            return new CG.Point2D(
                x1 + t * (x2 - x1),
                y1 + t * (y2 - y1)
            );
        }


       

        private List<Core.Models.ModelLayout.Level> ConvertToCoreLevelsWithBaseProcessing(List<LevelViewModel> levels)
        {
            var result = new List<Core.Models.ModelLayout.Level>();
            LevelViewModel baseLevel = BaseLevel;

            // First pass - convert all levels
            foreach (var level in levels)
            {
                var coreLevel = new Core.Models.ModelLayout.Level
                {
                    Name = level.Name,
                    // Convert elevation from feet (UI display) to inches (model units)
                    Elevation = level.Elevation * 12.0
                };

                if (level.SelectedFloorType != null)
                {
                    coreLevel.FloorTypeId = level.SelectedFloorType.Id;
                }

                result.Add(coreLevel);
            }

            // If we have a base level, process it
            if (baseLevel != null)
            {
                // Find the corresponding core level
                var baseModelLevel = result.FirstOrDefault(l =>
                    l.Name == baseLevel.Name ||
                    Math.Abs(l.Elevation - (baseLevel.Elevation * 12.0)) < 0.001);

                if (baseModelLevel != null)
                {
                    // Store the original elevation for adjustment
                    double originalElevation = baseModelLevel.Elevation;

                    // Create a special Base floor type
                    var baseFloorType = new Core.Models.ModelLayout.FloorType
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Base"
                    };

                    // Rename the level to "Base" and set elevation to 0
                    baseModelLevel.Name = "Base";
                    baseModelLevel.Elevation = 0.0;
                    baseModelLevel.FloorTypeId = baseFloorType.Id;

                    // Adjust all other levels relative to the base level
                    foreach (var level in result)
                    {
                        if (level != baseModelLevel)
                        {
                            level.Elevation -= originalElevation;
                        }
                    }

                    // Return the base floor type separately
                    return result;
                }
            }

            return result;
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
        // Helper class to organize export data
        private class ExportData
        {
            public Dictionary<string, bool> ElementFilters { get; set; }
            public Dictionary<string, bool> MaterialFilters { get; set; }
            public List<ElementId> SelectedLevelIds { get; set; }
            public List<Core.Models.ModelLayout.FloorType> CustomFloorTypes { get; set; }
            public List<Core.Models.ModelLayout.Level> CustomLevels { get; set; }
            public Dictionary<string, ElementId> FloorTypeToViewMap { get; set; }
        }
    }
}