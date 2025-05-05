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
using Revit.Export.ModelLayout;
using System.Collections.Generic;

namespace Revit.ViewModels
{
    public class ExportGrasshopperViewModel : INotifyPropertyChanged
    {
        #region Fields
        private UIApplication _uiApp;
        private Document _document;
        private string _outputLocation;
        private XYZ _referencePoint;
        private string _referencePointText;
        private string _newFloorTypeName;
        private ObservableCollection<FloorTypeModel> _floorTypes;
        private FloorTypeModel _selectedFloorType;
        private string _levelSearchText;
        private string _viewSearchText;
        private ObservableCollection<LevelViewModel> _levelCollection;
        private ObservableCollection<ViewPlanViewModel> _viewPlanCollection;
        private ObservableCollection<FloorTypeViewMappingModel> _floorTypeViewMappingCollection;
        private ICollectionView _filteredLevelCollection;
        private FloorTypeExport _floorTypeExporter;
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

        public XYZ ReferencePoint
        {
            get => _referencePoint;
            set
            {
                _referencePoint = value;
                OnPropertyChanged();

                // Update the text representation
                ReferencePointText = value == null ?
                    "No point selected" :
                    $"X: {value.X:F2}, Y: {value.Y:F2}, Z: {value.Z:F2}";
            }
        }

        public string ReferencePointText
        {
            get => _referencePointText;
            set
            {
                _referencePointText = value;
                OnPropertyChanged();
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
                FilterViewPlanCollection();
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
        #endregion

        #region Commands
        public ICommand BrowseOutputCommand { get; private set; }
        public ICommand SelectPointCommand { get; private set; }
        public ICommand AddFloorTypeCommand { get; private set; }
        public ICommand RemoveFloorTypeCommand { get; private set; }
        public ICommand ExportCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        #endregion

        // Constructor for designer time
        public ExportGrasshopperViewModel()
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
        public ExportGrasshopperViewModel(UIApplication uiApp)
        {
            _uiApp = uiApp;
            _document = uiApp?.ActiveUIDocument?.Document;

            if (_document != null)
            {
                _floorTypeExporter = new FloorTypeExport(_document);
            }

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

            ReferencePointText = "No point selected";
            LevelSearchText = string.Empty;
            ViewSearchText = string.Empty;

            // Initialize the filtered views
            _filteredLevelCollection = CollectionViewSource.GetDefaultView(LevelCollection);
            _filteredLevelCollection.Filter = LevelViewFilter;
        }

        private void InitializeCommands()
        {
            BrowseOutputCommand = new RelayCommand(BrowseOutput);
            SelectPointCommand = new RelayCommand(SelectPoint);
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
                                            // No default floor type assignment - this will be null initially
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

        private void FilterViewPlanCollection()
        {
            // Since we don't have a filtered collection for ViewPlans directly,
            // we need to ensure the appropriate views are shown in the mappings
            // This could be enhanced with more complex filtering if needed
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

        private void SelectPoint(object parameter)
        {
            try
            {
                RequestMinimize?.Invoke();

                // Let user select a point in Revit
                var uiDoc = _uiApp?.ActiveUIDocument;
                if (uiDoc != null)
                {
                    var result = uiDoc.Selection.PickPoint("Select a reference point");
                    ReferencePoint = result;
                }

                RequestRestore?.Invoke();
            }
            catch (Exception ex)
            {
                RequestRestore?.Invoke();
                MessageBox.Show($"Error selecting point: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

            if (ReferencePoint == null)
            {
                MessageBox.Show("Please select a reference point first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (FloorTypes.Count == 0)
            {
                MessageBox.Show("Please create at least one floor type first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

                // Verify each selected level has a floor type assigned
                foreach (var level in selectedLevels)
                {
                    if (level.SelectedFloorType == null)
                    {
                        MessageBox.Show($"Please assign a floor type to level: {level.Name}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Check if all floor types have an assigned view plan
                foreach (var floorTypeMapping in FloorTypeViewMappingCollection)
                {
                    if (floorTypeMapping.SelectedViewPlan == null)
                    {
                        MessageBox.Show($"Please assign a view plan to floor type: {floorTypeMapping.FloorTypeName}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Get the folder name from the path
                string exportFolder = OutputLocation;
                string folderName = Path.GetFileName(exportFolder);

                // If the user selected a folder but not the final project folder, create it
                if (string.IsNullOrEmpty(folderName))
                {
                    folderName = _document?.Title ?? "GrasshopperExport";
                    exportFolder = Path.Combine(OutputLocation, folderName);
                    Directory.CreateDirectory(exportFolder);
                }

                // Create JSON file path and CAD folder path
                string jsonPath = Path.Combine(exportFolder, folderName + ".json");
                string dwgFolder = Path.Combine(exportFolder, "CAD");
                Directory.CreateDirectory(dwgFolder);

                // Use ONLY the floor types from the UI - no default type
                var floorTypesList = FloorTypes.Select(ft => new Core.Models.ModelLayout.FloorType
                {
                    Id = ft.Id,
                    Name = ft.Name
                }).ToList();

                // Create a map between the view model floor types and the core model instances
                var floorTypeIdToNameMap = floorTypesList
                    .ToDictionary(ft => ft.Id, ft => ft.Name);

                // Create level-to-floor-type mapping for selected levels only
                var selectedCoreModelLevels = new List<Core.Models.ModelLayout.Level>();
                foreach (var level in selectedLevels)
                {
                    // Create a Core.Models.ModelLayout.Level for each selected level
                    var coreLevel = new Core.Models.ModelLayout.Level
                    {
                        Name = level.Name,
                        Elevation = level.Elevation
                    };

                    // Assign floor type ID based on the selected floor type
                    if (level.SelectedFloorType != null)
                    {
                        coreLevel.FloorTypeId = level.SelectedFloorType.Id;
                    }

                    selectedCoreModelLevels.Add(coreLevel);
                }

                // Create list of selected level IDs to only export those levels
                var selectedLevelIds = selectedLevels
                    .Select(level => level.LevelId)
                    .Where(id => id != null)
                    .ToList();

                // Create floor type to view plan mapping for export
                var floorTypeToViewMap = new Dictionary<string, ElementId>();
                foreach (var mapping in FloorTypeViewMappingCollection)
                {
                    if (mapping.SelectedViewPlan != null)
                    {
                        // Find the floor type ID by name
                        var floorType = FloorTypes.FirstOrDefault(ft => ft.Name == mapping.FloorTypeName);
                        if (floorType != null)
                        {
                            floorTypeToViewMap[floorType.Id] = mapping.SelectedViewPlan.ViewId;
                        }
                    }
                }

                // Execute the exporter
                GrasshopperExporter exporter = new GrasshopperExporter(_document, _uiApp);

                // Export the model with floor type information, view plan mappings, and selected levels only
                exporter.ExportWithFloorTypeViewMappings(
                    jsonPath,
                    dwgFolder,
                    floorTypesList,
                    selectedCoreModelLevels,
                    ReferencePoint,
                    floorTypeToViewMap,
                    selectedLevelIds
                );

                MessageBox.Show($"Export completed successfully to:\n{exportFolder}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);

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
                   ReferencePoint != null &&
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
                propertyName == nameof(ReferencePoint) ||
                propertyName == nameof(NewFloorTypeName) ||
                propertyName == nameof(LevelCollection))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
        #endregion
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}