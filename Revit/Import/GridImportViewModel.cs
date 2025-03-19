using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Core.Converters;
using Core.Models.Elements;
using Core.Models.Model;

namespace Revit.Import
{
    /// <summary>
    /// ViewModel for the Grid Importer functionality
    /// </summary>
    public class GridImportViewModel : INotifyPropertyChanged
    {
        private readonly UIDocument _uiDoc;
        private string _filePath;
        private string _fileName;
        private bool _importGrids = true;
        private int _importedCount;
        private int _failedCount;
        private bool _isImportSuccessful;

        public GridImportViewModel(UIDocument uiDoc)
        {
            _uiDoc = uiDoc;
            BrowseCommand = new RelayCommand(Browse);
            ImportCommand = new RelayCommand(Import, CanImport);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand BrowseCommand { get; }
        public ICommand ImportCommand { get; }

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    FileName = Path.GetFileName(value);
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string FileName
        {
            get => _fileName;
            set
            {
                if (_fileName != value)
                {
                    _fileName = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ImportGrids
        {
            get => _importGrids;
            set
            {
                if (_importGrids != value)
                {
                    _importGrids = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private void Browse()
        {
            FileOpenDialog fileOpenDialog = new FileOpenDialog("JSON Files (*.json)|*.json");
            fileOpenDialog.Title = "Select JSON File";

            if (fileOpenDialog.Show() == ItemSelectionDialogResult.Canceled)
                return;

            ModelPath modelPath = fileOpenDialog.GetSelectedModelPath();
            FilePath = ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath);
        }

        private bool CanImport() => !string.IsNullOrEmpty(FilePath) && ImportGrids;

        private void Import()
        {
            try
            {
                Document doc = _uiDoc.Document;

                using (Transaction transaction = new Transaction(doc, "Import Grids from JSON"))
                {
                    transaction.Start();

                    _importedCount = 0;
                    _failedCount = 0;

                    if (ImportGrids)
                    {
                        ImportGridsFromJson(doc);
                    }

                    if (_importedCount > 0)
                    {
                        transaction.Commit();
                        _isImportSuccessful = true;
                    }
                    else
                    {
                        transaction.RollBack();
                        _isImportSuccessful = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _isImportSuccessful = false;
                TaskDialog.Show("Error", ex.Message);
            }
        }

        private void ImportGridsFromJson(Document doc)
        {
            try
            {
                var model = JsonConverter.LoadFromFile(FilePath);

                foreach (var jsonGrid in model.Grids)
                {
                    try
                    {
                        // Convert JSON grid points to Revit XYZ
                        XYZ startPoint = ConvertToRevitCoordinates(jsonGrid.StartPoint);
                        XYZ endPoint = ConvertToRevitCoordinates(jsonGrid.EndPoint);

                        // Create line for grid
                        Line gridLine = Line.CreateBound(startPoint, endPoint);

                        // Create grid
                        Autodesk.Revit.DB.Grid revitGrid = Autodesk.Revit.DB.Grid.Create(doc, gridLine);

                        // Set grid name
                        revitGrid.Name = jsonGrid.Name;

                        // Apply bubble visibility if specified in JSON
                        if (jsonGrid.StartPoint.IsBubble)
                        {
                            revitGrid.ShowBubbleInView(DatumEnds.End0, doc.ActiveView);
                        }
                        else
                        {
                            revitGrid.HideBubbleInView(DatumEnds.End0, doc.ActiveView);
                        }

                        if (jsonGrid.EndPoint.IsBubble)
                        {
                            revitGrid.ShowBubbleInView(DatumEnds.End1, doc.ActiveView);
                        }
                        else
                        {
                            revitGrid.HideBubbleInView(DatumEnds.End1, doc.ActiveView);
                        }

                        _importedCount++;
                    }
                    catch (Exception)
                    {
                        _failedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Failed to load JSON file: {ex.Message}");
            }
        }

        public string GetResultMessage()
        {
            if (!_isImportSuccessful)
                return "No grids could be imported.";

            string message = $"Successfully imported {_importedCount} grids";
            if (_failedCount > 0)
                message += $"\nFailed to import {_failedCount} grids";

            return message;
        }

        private XYZ ConvertToRevitCoordinates(GridPoint point)
        {
            // Assuming JSON coordinates are in inches, convert to feet for Revit
            double x = point.X / 12.0;
            double y = point.Y / 12.0;
            double z = point.Z / 12.0;

            return new XYZ(x, y, z);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Simple implementation of ICommand for MVVM
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

        public void Execute(object parameter) => _execute();
    }
}