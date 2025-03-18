using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Collections.Generic;
using System.IO;
using Core.Converters;
using Core.Models.Model;
using Core.Models.Elements;

namespace Revit.Import
{
    /// <summary>
    /// WPF Dialog for grid import options
    /// </summary>
    public partial class GridImportOptionsDialog : Window
    {
        public bool ImportGrids { get; set; } = true;
        public string FilePath { get; set; }

        public GridImportOptionsDialog(string filePath)
        {
            FilePath = filePath;

            // Set window title
            Title = "Grid Import Options";

            // Set size
            Width = 450;
            Height = 300;

            // Create main layout
            var stackPanel = new StackPanel { Margin = new Thickness(10) };
            Content = stackPanel;

            // File info
            var fileNamePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            fileNamePanel.Children.Add(new TextBlock { Text = "File: ", FontWeight = FontWeights.Bold });
            fileNamePanel.Children.Add(new TextBlock { Text = Path.GetFileName(filePath) });
            stackPanel.Children.Add(fileNamePanel);

            // Grid import checkbox
            var gridCheckBox = new CheckBox
            {
                Content = "Import Grids",
                IsChecked = ImportGrids,
                Margin = new Thickness(0, 10, 0, 10)
            };
            gridCheckBox.Checked += (s, e) => ImportGrids = true;
            gridCheckBox.Unchecked += (s, e) => ImportGrids = false;
            stackPanel.Children.Add(gridCheckBox);

            // Grid preview (future enhancement)
            var previewLabel = new TextBlock
            {
                Text = "Grid Preview:",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 10, 0, 5)
            };
            stackPanel.Children.Add(previewLabel);

            // Placeholder for grid preview (could be enhanced with a real preview)
            var previewBox = new Border
            {
                Height = 120,
                Background = System.Windows.Media.Brushes.LightGray,
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1)
            };
            previewBox.Child = new TextBlock
            {
                Text = "Grid preview would appear here",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            stackPanel.Children.Add(previewBox);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 25,
                Margin = new Thickness(10, 0, 0, 0)
            };
            cancelButton.Click += (s, e) => DialogResult = false;

            var importButton = new Button
            {
                Content = "Import",
                Width = 80,
                Height = 25,
                IsDefault = true,
                Margin = new Thickness(10, 0, 0, 0)
            };
            importButton.Click += (s, e) => DialogResult = true;

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(importButton);
            stackPanel.Children.Add(buttonPanel);
        }
    }

    /// <summary>
    /// External command for showing import options dialog
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class GridImportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Show file dialog to select JSON file
                string jsonFilePath = ShowFileDialog();
                if (string.IsNullOrEmpty(jsonFilePath))
                {
                    return Result.Cancelled;
                }

                // Create and show options dialog
                var optionsDialog = new GridImportOptionsDialog(jsonFilePath);
                optionsDialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                // Make dialog modal to Revit window
                IntPtr revitHandle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                WindowInteropHelper helper = new WindowInteropHelper(optionsDialog);
                helper.Owner = revitHandle;

                bool? result = optionsDialog.ShowDialog();

                if (result == true)
                {
                    // Proceed with import based on dialog settings
                    return ImportGrids(doc, jsonFilePath, optionsDialog);
                }

                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        /// <summary>
        /// Shows file dialog to select JSON file using Revit's FileOpenDialog
        /// </summary>
        private string ShowFileDialog()
        {
            // Create Revit's FileOpenDialog
            FileOpenDialog fileOpenDialog = new FileOpenDialog("JSON Files (*.json)|*.json");
            fileOpenDialog.Title = "Select JSON File";
            //fileOpenDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Set default extension
            //fileOpenDialog.DefaultExt = "json";

            // Show dialog and check result
            if (fileOpenDialog.Show() == ItemSelectionDialogResult.Canceled)
            {
                return null;
            }

            // Get selected file
            ModelPath modelPath = fileOpenDialog.GetSelectedModelPath();
            return ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath);
        }

        /// <summary>
        /// Imports grids based on user options
        /// </summary>
        private Result ImportGrids(Document doc, string jsonFilePath, GridImportOptionsDialog options)
        {
            if (!options.ImportGrids)
            {
                // Nothing to import (future: could be expanded for other element types)
                TaskDialog.Show("Import", "No elements selected for import.");
                return Result.Succeeded;
            }

            try
            {
                // Load JSON file
                var model = JsonConverter.LoadFromFile(jsonFilePath);

                using (Transaction transaction = new Transaction(doc, "Import Grids from JSON"))
                {
                    transaction.Start();

                    int importedCount = 0;
                    int failedCount = 0;

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

                            importedCount++;
                        }
                        catch (Exception ex)
                        {
                            // Log the error but continue with other grids
                            failedCount++;
                        }
                    }

                    if (importedCount > 0)
                    {
                        transaction.Commit();

                        string resultMessage = $"Successfully imported {importedCount} grids";
                        if (failedCount > 0)
                        {
                            resultMessage += $"\nFailed to import {failedCount} grids";
                        }

                        TaskDialog.Show("Import Successful", resultMessage);
                        return Result.Succeeded;
                    }
                    else
                    {
                        transaction.RollBack();
                        TaskDialog.Show("Import Failed", "No grids could be imported.");
                        return Result.Failed;
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Converts JSON GridPoint to Revit XYZ (with unit conversion)
        /// </summary>
        private XYZ ConvertToRevitCoordinates(GridPoint point)
        {
            // Assuming JSON coordinates are in inches, convert to feet for Revit
            double x = point.X / 12.0;
            double y = point.Y / 12.0;
            double z = point.Z / 12.0;

            return new XYZ(x, y, z);
        }
    }
}