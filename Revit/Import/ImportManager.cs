using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Core.Models;
using Revit.ViewModels;
using System.Diagnostics;

namespace Revit.Import
{
    public class ImportManager
    {
        private readonly Document _doc;
        private readonly UIApplication _uiApp;

        public ImportManager(Document doc, UIApplication uiApp)
        {
            _doc = doc;
            _uiApp = uiApp;
        }

        public int ImportFromJson(string filePath)
        {
            // Create default filters (import everything)
            var elementFilters = new Dictionary<string, bool>
            {
                { "Grids", true },
                { "Beams", true },
                { "Braces", true },
                { "Columns", true },
                { "Floors", true },
                { "Walls", true },
                { "Footings", true }
            };

            var materialFilters = new Dictionary<string, bool>
            {
                { "Steel", true },
                { "Concrete", true }
            };

            // No transformations for basic import
            var transformParams = new ImportTransformationParameters();

            return ImportFromFile(filePath, elementFilters, materialFilters, transformParams);
        }

        public int ImportFromFile(string filePath, Dictionary<string, bool> elementFilters,
            Dictionary<string, bool> materialFilters, ImportTransformationParameters transformParams)
        {
            try
            {
                // Create import context
                var context = new ImportContext(_doc, _uiApp)
                {
                    FilePath = filePath,
                    ElementFilters = elementFilters ?? new Dictionary<string, bool>(),
                    MaterialFilters = materialFilters ?? new Dictionary<string, bool>(),
                    TransformationParams = transformParams
                };

                // Use clean architecture
                var importer = new StructuralModelImporter();
                var model = importer.Import(context);

                // Import to Revit
                int importedCount = ImportToRevit(model, context);

                return importedCount;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Import Error", $"Error importing model: {ex.Message}");
                return 0;
            }
        }

        private int ImportToRevit(BaseModel model, ImportContext context)
        {
            int totalImported = 0;

            using (Transaction transaction = new Transaction(_doc, "Import Structural Model"))
            {
                transaction.Start();

                try
                {
                    Debug.WriteLine($"Starting import transaction");

                    // Create level mapping first
                    Dictionary<string, ElementId> levelIdMap = CreateLevelMapping(model);
                    Debug.WriteLine($"Initial level mapping created with {levelIdMap.Count} entries");

                    // Import grids first
                    if (context.ShouldImportElement("Grids") && model.ModelLayout?.Grids != null)
                    {
                        Debug.WriteLine($"Importing {model.ModelLayout.Grids.Count} grids");
                        var gridImport = new ModelLayout.GridImport(_doc);
                        int gridCount = gridImport.Import(model.ModelLayout.Grids);
                        totalImported += gridCount;
                        Debug.WriteLine($"Grid import completed: {gridCount} grids imported");
                    }
                    else
                    {
                        Debug.WriteLine("Skipping grid import - not enabled or no grids found");
                    }

                    // Import levels first (before creating level mapping)
                    if (model.ModelLayout?.Levels != null)
                    {
                        Debug.WriteLine($"Starting level import for {model.ModelLayout.Levels.Count} levels");
                        var levelImport = new ModelLayout.LevelImport(_doc);
                        Dictionary<string, ElementId> tempLevelMapping = new Dictionary<string, ElementId>();

                        int levelCount = levelImport.Import(model.ModelLayout.Levels, tempLevelMapping);
                        totalImported += levelCount;

                        Debug.WriteLine($"Level import completed: {levelCount} levels imported");
                        Debug.WriteLine($"Level mapping contains {tempLevelMapping.Count} entries");

                        // Update our level mapping with the results
                        foreach (var kvp in tempLevelMapping)
                        {
                            levelIdMap[kvp.Key] = kvp.Value;
                            Debug.WriteLine($"Mapped level {kvp.Key} to ElementId {kvp.Value}");
                        }

                        // Verify levels still exist after creation
                        var createdLevels = new FilteredElementCollector(_doc)
                            .OfClass(typeof(Level))
                            .Cast<Level>()
                            .Where(l => tempLevelMapping.Values.Contains(l.Id))
                            .ToList();
                        Debug.WriteLine($"Verified {createdLevels.Count} levels still exist in document");
                    }

                    Debug.WriteLine("Checking element import conditions...");

                    // Import beams
                    if (context.ShouldImportElement("Beams") && model.Elements?.Beams != null)
                    {
                        Debug.WriteLine($"Importing {model.Elements.Beams.Count} beams");
                        var beamImport = new Elements.BeamImport(_doc);
                        int beamCount = beamImport.Import(model.Elements.Beams, levelIdMap, model);
                        totalImported += beamCount;
                        Debug.WriteLine($"Beam import completed: {beamCount} beams imported");
                    }
                    else
                    {
                        Debug.WriteLine($"Skipping beam import - enabled: {context.ShouldImportElement("Beams")}, beam count: {model.Elements?.Beams?.Count ?? 0}");
                    }

                    // Import columns
                    if (context.ShouldImportElement("Columns") && model.Elements?.Columns != null)
                    {
                        Debug.WriteLine($"Importing {model.Elements.Columns.Count} columns");
                        var columnImport = new Elements.ColumnImport(_doc);
                        int columnCount = columnImport.Import(model.Elements.Columns, levelIdMap, model);
                        totalImported += columnCount;
                        Debug.WriteLine($"Column import completed: {columnCount} columns imported");
                    }
                    else
                    {
                        Debug.WriteLine($"Skipping column import - enabled: {context.ShouldImportElement("Columns")}, column count: {model.Elements?.Columns?.Count ?? 0}");
                    }

                    // Import braces
                    if (context.ShouldImportElement("Braces") && model.Elements?.Braces != null)
                    {
                        Debug.WriteLine($"Importing {model.Elements.Braces.Count} braces");
                        var braceImport = new Elements.BraceImport(_doc);
                        int braceCount = braceImport.Import(model.Elements.Braces, levelIdMap, model);
                        totalImported += braceCount;
                        Debug.WriteLine($"Brace import completed: {braceCount} braces imported");
                    }
                    else
                    {
                        Debug.WriteLine($"Skipping brace import - enabled: {context.ShouldImportElement("Braces")}, brace count: {model.Elements?.Braces?.Count ?? 0}");
                    }

                    // Import walls
                    if (context.ShouldImportElement("Walls") && model.Elements?.Walls != null)
                    {
                        Debug.WriteLine($"Importing {model.Elements.Walls.Count} walls");
                        var wallImport = new Elements.WallImport(_doc);
                        int wallCount = wallImport.Import(model.Elements.Walls, levelIdMap, model);
                        totalImported += wallCount;
                        Debug.WriteLine($"Wall import completed: {wallCount} walls imported");
                    }
                    else
                    {
                        Debug.WriteLine($"Skipping wall import - enabled: {context.ShouldImportElement("Walls")}, wall count: {model.Elements?.Walls?.Count ?? 0}");
                    }

                    // Import floors
                    if (context.ShouldImportElement("Floors") && model.Elements?.Floors != null)
                    {
                        Debug.WriteLine($"Importing {model.Elements.Floors.Count} floors");
                        var floorImport = new Elements.FloorImport(_doc);
                        int floorCount = floorImport.Import(levelIdMap, model);
                        totalImported += floorCount;
                        Debug.WriteLine($"Floor import completed: {floorCount} floors imported");
                    }
                    else
                    {
                        Debug.WriteLine($"Skipping floor import - enabled: {context.ShouldImportElement("Floors")}, floor count: {model.Elements?.Floors?.Count ?? 0}");
                    }

                    // Import footings
                    if (context.ShouldImportElement("Footings") && model.Elements?.IsolatedFootings != null)
                    {
                        Debug.WriteLine($"Importing {model.Elements.IsolatedFootings.Count} footings");
                        var footingImport = new Elements.IsolatedFootingImport(_doc);
                        int footingCount = footingImport.Import(model.Elements.IsolatedFootings, levelIdMap, model);
                        totalImported += footingCount;
                        Debug.WriteLine($"Footing import completed: {footingCount} footings imported");
                    }
                    else
                    {
                        Debug.WriteLine($"Skipping footing import - enabled: {context.ShouldImportElement("Footings")}, footing count: {model.Elements?.IsolatedFootings?.Count ?? 0}");
                    }

                    Debug.WriteLine($"About to commit transaction with {totalImported} total imported elements");
                    transaction.Commit();
                    Debug.WriteLine("Transaction committed successfully");
                }
                catch (Exception ex)
                {
                    transaction.RollBack();
                    Debug.WriteLine($"Transaction rolled back due to error: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    throw;
                }
            }

            return totalImported;
        }

        private Dictionary<string, ElementId> CreateLevelMapping(BaseModel model)
        {
            var mapping = new Dictionary<string, ElementId>();

            if (model.ModelLayout?.Levels == null) return mapping;

            var revitLevels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            foreach (var modelLevel in model.ModelLayout.Levels)
            {
                // Try to find existing level by name first
                var revitLevel = revitLevels.FirstOrDefault(l => l.Name == modelLevel.Name);

                // If not found by name, try by elevation
                if (revitLevel == null)
                {
                    revitLevel = revitLevels.FirstOrDefault(l =>
                        Math.Abs(l.Elevation - (modelLevel.Elevation / 12.0)) < 0.1);
                }

                if (revitLevel != null)
                {
                    mapping[modelLevel.Id] = revitLevel.Id;
                    Debug.WriteLine($"Mapped existing level {modelLevel.Name} to {revitLevel.Name}");
                }
                else
                {
                    Debug.WriteLine($"No existing level found for {modelLevel.Name}");
                }
            }

            return mapping;
        }

        private string GetUniqueLevelName(string baseName, List<Level> existingLevels)
        {
            string testName = baseName;
            int copyCount = 1;

            while (existingLevels.Any(l => l.Name == testName))
            {
                testName = $"{baseName} Copy{(copyCount > 1 ? $" {copyCount}" : "")}";
                copyCount++;
            }

            return testName;
        }
    }
}