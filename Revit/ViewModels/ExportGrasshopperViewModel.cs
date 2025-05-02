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
        private ObservableCollection<SheetViewModel> _sheetViewCollection;
        private ICollectionView _filteredSheetViewCollection;
        private string _searchText;
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

        public ObservableCollection<SheetViewModel> SheetViewCollection
        {
            get => _sheetViewCollection;
            set
            {
                _sheetViewCollection = value;
                OnPropertyChanged();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilterSheetViews();
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
                LoadSheetsAndViews();
            }
        }

        private void InitializeProperties()
        {
            FloorTypes = new ObservableCollection<FloorTypeModel>();
            SheetViewCollection = new ObservableCollection<SheetViewModel>();
            ReferencePointText = "No point selected";

            // Initialize the filtered view
            _filteredSheetViewCollection = CollectionViewSource.GetDefaultView(SheetViewCollection);
            _filteredSheetViewCollection.Filter = SheetViewFilter;

            // Add a default floor type
            FloorTypes.Add(new FloorTypeModel { Id = "default", Name = "Default" });
            SelectedFloorType = FloorTypes.First();
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
            FloorTypes.Add(new FloorTypeModel { Id = "ft1", Name = "Concrete Slab" });
            FloorTypes.Add(new FloorTypeModel { Id = "ft2", Name = "Metal Deck" });

            // Add sample sheets
            SheetViewCollection.Add(new SheetViewModel
            {
                SheetNumber = "A1.01",
                SheetName = "First Floor Plan",
                ViewName = "Level 1",
                IsSelected = true,
                SelectedFloorType = FloorTypes[0]
            });

            SheetViewCollection.Add(new SheetViewModel
            {
                SheetNumber = "A1.02",
                SheetName = "Second Floor Plan",
                ViewName = "Level 2",
                IsSelected = true,
                SelectedFloorType = FloorTypes[0]
            });
        }

        private void LoadSheetsAndViews()
        {
            // Clear existing items
            SheetViewCollection.Clear();

            try
            {
                // Get all sheets
                var sheets = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Sheets)
                    .WhereElementIsNotElementType()
                    .Cast<ViewSheet>()
                    .ToList();

                foreach (var sheet in sheets)
                {
                    var sheetNumber = sheet.SheetNumber;
                    var sheetName = sheet.Name;

                    // Get views on this sheet
                    var viewsOnSheet = sheet.GetAllPlacedViews()
                        .Select(id => _document.GetElement(id) as View)
                        .Where(v => v != null && v.ViewType == ViewType.FloorPlan);

                    foreach (var view in viewsOnSheet)
                    {
                        var sheetViewModel = new SheetViewModel
                        {
                            SheetNumber = sheetNumber,
                            SheetName = sheetName,
                            ViewName = view.Name,
                            ViewId = view.Id,
                            SheetId = sheet.Id,
                            IsSelected = true,
                            SelectedFloorType = FloorTypes.First() // Default floor type
                        };

                        SheetViewCollection.Add(sheetViewModel);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading sheets and views: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterSheetViews()
        {
            _filteredSheetViewCollection?.Refresh();
        }

        private bool SheetViewFilter(object item)
        {
            if (string.IsNullOrEmpty(SearchText))
                return true;

            var sheetView = (SheetViewModel)item;
            return sheetView.SheetNumber.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   sheetView.SheetName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   sheetView.ViewName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void BrowseOutput(object parameter)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Select Output Location",
                Filter = "JSON Files (*.json)|*.json",
                DefaultExt = ".json",
                FileName = _document?.Title ?? "export"
            };

            if (dialog.ShowDialog() == true)
            {
                OutputLocation = dialog.FileName;
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
        }

        private bool CanAddFloorType(object parameter)
        {
            return !string.IsNullOrWhiteSpace(NewFloorTypeName);
        }

        private void RemoveFloorType(object parameter)
        {
            if (parameter is FloorTypeModel floorType)
            {
                // Check if this is the default type
                if (floorType.Id == "default")
                {
                    MessageBox.Show("The default floor type cannot be removed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Update any sheet views using this floor type to use default
                var defaultType = FloorTypes.First(ft => ft.Id == "default");
                foreach (var sheetView in SheetViewCollection)
                {
                    if (sheetView.SelectedFloorType?.Id == floorType.Id)
                    {
                        sheetView.SelectedFloorType = defaultType;
                    }
                }

                // Remove the floor type
                FloorTypes.Remove(floorType);
            }
        }

        private void Export(object parameter)
        {
            if (string.IsNullOrEmpty(OutputLocation))
            {
                MessageBox.Show("Please select an output location first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (ReferencePoint == null)
            {
                MessageBox.Show("Please select a reference point first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Get selected sheets and views
                var selectedItems = SheetViewCollection
                    .Where(sv => sv.IsSelected)
                    .ToList();

                if (!selectedItems.Any())
                {
                    MessageBox.Show("Please select at least one sheet view to export.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Create export directory
                string exportFolder = Path.GetDirectoryName(OutputLocation);
                string baseName = Path.GetFileNameWithoutExtension(OutputLocation);

                // Create folder structure
                string projectFolder = Path.Combine(exportFolder, baseName);
                Directory.CreateDirectory(projectFolder);

                string jsonPath = Path.Combine(projectFolder, baseName + ".json");
                string dwgFolder = Path.Combine(projectFolder, "CAD");
                Directory.CreateDirectory(dwgFolder);

                // Convert view models to domain models
                var floorTypesList = FloorTypes.Select(ft => new Core.Models.ModelLayout.FloorType
                {
                    Id = ft.Id,
                    Name = ft.Name,
                    Description = $"Floor type for {ft.Name}"
                }).ToList();

                // Create level-to-floor-type mapping
                var levelToFloorTypeMap = new Dictionary<string, string>();
                foreach (var sheetView in selectedItems)
                {
                    if (sheetView.ViewId != null && sheetView.SelectedFloorType != null)
                    {
                        // Get the view from the document
                        View view = _document.GetElement(sheetView.ViewId) as View;
                        if (view != null)
                        {
                            // Try to find the level associated with this view
                            Level level = view.GenLevel;
                            if (level != null)
                            {
                                levelToFloorTypeMap[level.Id.ToString()] = sheetView.SelectedFloorType.Id;
                            }
                        }
                    }
                }

                // Execute the exporter
                GrasshopperExporter exporter = new GrasshopperExporter(_document, _uiApp);

                // Export the model with floor type information
                exporter.ExportSelectedSheets(jsonPath, dwgFolder, selectedItems, floorTypesList, ReferencePoint);

                MessageBox.Show($"Export completed successfully to:\n{projectFolder}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);

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
                   SheetViewCollection.Any(sv => sv.IsSelected);
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
                propertyName == nameof(NewFloorTypeName))
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

