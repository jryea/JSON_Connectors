using System;
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
        #endregion

        #region Properties
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
            FloorTypes = new ObservableCollection<FloorTypeModel>();
            LevelCollection = new ObservableCollection<LevelViewModel>();
            ViewPlanCollection = new ObservableCollection<ViewPlanViewModel>();
            FloorTypeViewMappingCollection = new ObservableCollection<FloorTypeViewMappingModel>();
            MasterStoryLevels = new ObservableCollection<LevelViewModel>();

            LevelSearchText = string.Empty;
            ViewSearchText = string.Empty;

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
                        Elevation = level.Elevation * 12.0, // Convert to inches
                        LevelId = level.Id,
                        IsSelected = false, // Set to false by default
                    };

                    LevelCollection.Add(levelViewModel);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading levels: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var dialog = new SaveFileDialog
            {
                Title = "Select Output Folder",
                Filter = "Folder Selection|*.folder",
                FileName = "Select Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                // Extract the folder path from the selected file path
                OutputLocation = Path.GetDirectoryName(dialog.FileName);
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
                MessageBox.Show("Please select an output folder first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Get selected export format
                string exportFormat = "UNKNOWN";
                if (ExportToETABS) exportFormat = "ETABS";
                else if (ExportToRAM) exportFormat = "RAM";
                else if (ExportToGrasshopper) exportFormat = "Grasshopper";

                // Get selected levels
                var selectedLevels = LevelCollection
                    .Where(level => level.IsSelected)
                    .ToList();

                if (!selectedLevels.Any())
                {
                    MessageBox.Show("Please select at least one level to export.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Verify each selected level has a floor type assigned
                foreach (var level in selectedLevels)
                {
                    if (level.SelectedFloorType == null)
                    {
                        MessageBox.Show($"Please assign a floor type to level: {level.Name}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                MessageBox.Show($"Would export to {exportFormat} format\nOutput location: {OutputLocation}\nSelected levels: {selectedLevels.Count}",
                               "Export", MessageBoxButton.OK, MessageBoxImage.Information);

                // Close the dialog
                RequestClose?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during export: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExport(object parameter)
        {
            return !string.IsNullOrEmpty(OutputLocation) &&
                   LevelCollection.Any(level => level.IsSelected) &&
                   FloorTypes.Count > 0;
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
    }
}