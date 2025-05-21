using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Core.Converters;
using Core.Models;
using CE = Core.Models.Elements;
using CM = Core.Models.Metadata;
using Revit.Utilities;
using System.Linq;

namespace Revit.Export
{
    public class ExportManager
    {
        private readonly Document _doc;
        private readonly UIApplication _uiApp;
        private BaseModel _model;

        public ExportManager(Document doc, UIApplication uiApp)
        {
            _doc = doc;
            _uiApp = uiApp;
            _model = new BaseModel();
        }

        public int ExportToJson(string filePath)
        {
            // Create default filters (all enabled)
            Dictionary<string, bool> elementFilters = new Dictionary<string, bool>
            {
                { "Grids", true },
                { "Beams", true },
                { "Braces", true },
                { "Columns", true },
                { "Floors", true },
                { "Walls", true },
                { "Footings", true }
            };

            Dictionary<string, bool> materialFilters = new Dictionary<string, bool>
            {
                { "Steel", true },
                { "Concrete", true }
            };

            return ExportToJson(filePath, elementFilters, materialFilters, null, null);
        }

        public int ExportToJson(string filePath, Dictionary<string, bool> elementFilters,
                       Dictionary<string, bool> materialFilters,
                       List<ElementId> selectedLevelIds = null,
                       ElementId baseLevelId = null,
                       List<Core.Models.ModelLayout.FloorType> customFloorTypes = null,
                       List<Core.Models.ModelLayout.Level> customLevels = null)
        {
            int totalExported = 0;

            try
            {
                // Initialize metadata
                InitializeMetadata();

                // Handle custom levels or export regular levels
                if (customLevels != null && customLevels.Count > 0)
                {
                    // Use the provided custom levels
                    _model.ModelLayout.Levels = customLevels;
                    totalExported += customLevels.Count;
                }
                else
                {
                    // Export standard layout elements
                    totalExported += ExportLayoutElements(selectedLevelIds, baseLevelId);
                }

                // Process the base level (if specified) to rename it, set elevation to 0, and create a Base floor type
                if (baseLevelId != null)
                {
                    ProcessBaseLevel(baseLevelId);
                }

                // Handle custom floor types or create default ones
                if (customFloorTypes != null && customFloorTypes.Count > 0)
                {
                    // Use the provided custom floor types
                    _model.ModelLayout.FloorTypes = customFloorTypes;

                    // Don't perform CreateFloorTypesFromLevels() as we're using custom floor types
                    // Note: The levels should already have FloorTypeId set correctly
                }
                else
                {
                    // Create default floor types based on level names
                    CreateFloorTypesFromLevels();
                }

                // Export materials first so we have their IDs for referencing
                totalExported += ExportMaterials(materialFilters);

                // Then export other property definitions that reference materials
                totalExported += ExportProperties(materialFilters);

                // Export structural elements with level filtering
                totalExported += ExportStructuralElements(elementFilters, selectedLevelIds, baseLevelId);

                // Save the model to file
                JsonConverter.SaveToFile(_model, filePath);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Export Error", $"Error exporting model: {ex.Message}");
            }

            return totalExported;
        }

        private void ProcessBaseLevel(ElementId baseLevelId)
        {
            if (baseLevelId == null || _model.ModelLayout.Levels == null || _model.ModelLayout.Levels.Count == 0)
                return;

            // Find the base level in the model
            Level baseRevitLevel = _doc.GetElement(baseLevelId) as Level;
            if (baseRevitLevel == null)
                return;

            // Find the corresponding level in our model
            var baseModelLevel = _model.ModelLayout.Levels.FirstOrDefault(l =>
                l.Name == baseRevitLevel.Name ||
                Math.Abs(l.Elevation - (baseRevitLevel.Elevation * 12.0)) < 0.1);

            if (baseModelLevel == null)
                return;

            // Save original elevation for adjusting other levels if needed
            double originalElevation = baseModelLevel.Elevation;

            // Rename the level to "Base" and set elevation to 0
            baseModelLevel.Name = "Base";
            baseModelLevel.Elevation = 0.0;

            // Create a special "Base" floor type for this level
            var baseFloorType = new Core.Models.ModelLayout.FloorType
            {
                Name = "Base"
            };

            // Add the floor type to the model
            if (_model.ModelLayout.FloorTypes == null)
                _model.ModelLayout.FloorTypes = new List<Core.Models.ModelLayout.FloorType>();

            _model.ModelLayout.FloorTypes.Add(baseFloorType);

            // Associate the base level with this floor type
            baseModelLevel.FloorTypeId = baseFloorType.Id;

            // Adjust all other levels relative to the base level
            if (Math.Abs(originalElevation) > 0.001) // Only adjust if there was a change
            {
                foreach (var level in _model.ModelLayout.Levels)
                {
                    if (level != baseModelLevel)
                    {
                        level.Elevation -= originalElevation;
                    }
                }
            }
        }

        // Create unique FloorTypes based on Levels for Revit export only
        private void CreateFloorTypesFromLevels()
        {
            // Clear any existing floor types
            _model.ModelLayout.FloorTypes = new List<Core.Models.ModelLayout.FloorType>();

            // Create a unique FloorType for each Level
            foreach (var level in _model.ModelLayout.Levels)
            {
                // Generate a unique floor type ID
                string floorTypeId = Core.Utilities.IdGenerator.Generate(Core.Utilities.IdGenerator.Layout.FLOOR_TYPE);

                // Create a new FloorType using the level name
                Core.Models.ModelLayout.FloorType floorType = new Core.Models.ModelLayout.FloorType(level.Name);

                // Add to the model's FloorTypes collection
                _model.ModelLayout.FloorTypes.Add(floorType);

                // Associate this FloorType with the Level
                level.FloorTypeId = floorType.Id;
            }

            System.Diagnostics.Debug.WriteLine($"Created {_model.ModelLayout.FloorTypes.Count} unique FloorTypes for Revit export");
        }

        private void InitializeMetadata()
        {
            // Initialize project info
            CM.ProjectInfo projectInfo = new CM.ProjectInfo
            {
                ProjectName = _doc.ProjectInformation?.Name ?? _doc.Title,
                ProjectId = _doc.ProjectInformation?.Number ?? Guid.NewGuid().ToString(),
                CreationDate = DateTime.Now,
                SchemaVersion = "1.0"
            };

            // Initialize units
            CM.Units units = new CM.Units
            {
                Length = "inches",
                Force = "pounds",
                Temperature = "fahrenheit"
            };

            // Extract coordinates
            CM.Coordinates coordinates = Helpers.ExtractCoordinateSystem(_doc);

            // Set the metadata containers
            _model.Metadata.ProjectInfo = projectInfo;
            _model.Metadata.Units = units;
            _model.Metadata.Coordinates = coordinates;
        }

        private int ExportLayoutElements(List<ElementId> selectedLevelIds = null, ElementId baseLevelId = null)
        {
            int count = 0;

            // Export levels
            Export.ModelLayout.LevelExport levelExport = new Export.ModelLayout.LevelExport(_doc);
            count += levelExport.Export(_model.ModelLayout.Levels, selectedLevelIds);

            // If a base level is specified, adjust elevations relative to it
            if (baseLevelId != null)
            {
                AdjustElevationsForBaseLevel(baseLevelId);
            }

            // Export grids
            Export.ModelLayout.GridExport gridExport = new Export.ModelLayout.GridExport(_doc);
            count += gridExport.Export(_model.ModelLayout.Grids);

            return count;
        }

        private void AdjustElevationsForBaseLevel(ElementId baseLevelId)
        {
            // Find the base level in Revit
            Level baseLevel = _doc.GetElement(baseLevelId) as Level;
            if (baseLevel == null) return;

            double baseElevation = baseLevel.Elevation;

            // Find the corresponding level in our model
            var modelBaseLevel = _model.ModelLayout.Levels.Find(l => 
                Math.Abs(l.Elevation - (baseElevation * 12.0)) < 0.1 || // Compare elevation
                l.Name == baseLevel.Name); // Or compare name as fallback

            if (modelBaseLevel == null) return;

            // Use the elevation of the base level as zero reference
            double offset = modelBaseLevel.Elevation;

            // Adjust all level elevations
            foreach (var level in _model.ModelLayout.Levels)
            {
                level.Elevation -= offset;
            }
        }

        private int ExportMaterials(Dictionary<string, bool> materialFilters)
        {
            // Export materials first so we can reference them
            Export.Properties.MaterialExport materialExport = new Export.Properties.MaterialExport(_doc);
            int materialCount = materialExport.Export(_model.Properties.Materials, materialFilters);

            System.Diagnostics.Debug.WriteLine($"Exported {materialCount} materials");
            return materialCount;
        }

        private int ExportProperties(Dictionary<string, bool> materialFilters)
        {
            int count = 0;

            // Export wall properties
            Export.Properties.WallPropertiesExport wallPropertiesExport = new Export.Properties.WallPropertiesExport(_doc);
            count += wallPropertiesExport.Export(_model.Properties.WallProperties);

            // Export floor properties
            Export.Properties.FloorPropertiesExport floorPropertiesExport = new Export.Properties.FloorPropertiesExport(_doc);
            count += floorPropertiesExport.Export(_model.Properties.FloorProperties);

            // Export frame properties - pass the exported materials for correct ID mapping
            Export.Properties.FramePropertiesExport framePropertiesExport = new Export.Properties.FramePropertiesExport(_doc);
            count += framePropertiesExport.Export(_model.Properties.FrameProperties, _model.Properties.Materials);

            return count;
        }
        private int ExportStructuralElements(Dictionary<string, bool> elementFilters, List<ElementId> selectedLevelIds, ElementId baseLevelId = null)
        {
            int count = 0;

            try
            {
                // Convert ElementIds to level IDs that we can use for filtering
                HashSet<string> selectedLevelIdStrings = new HashSet<string>();
                Dictionary<ElementId, string> revitToModelLevelIds = new Dictionary<ElementId, string>();

                if (selectedLevelIds != null && selectedLevelIds.Count > 0)
                {
                    // Build mapping between Revit and model level IDs
                    foreach (var level in _model.ModelLayout.Levels)
                    {
                        foreach (var revitLevelId in selectedLevelIds)
                        {
                            Level revitLevel = _doc.GetElement(revitLevelId) as Level;
                            if (revitLevel != null && (revitLevel.Name == level.Name ||
                                Math.Abs(revitLevel.Elevation * 12.0 - level.Elevation) < 0.1))
                            {
                                revitToModelLevelIds[revitLevelId] = level.Id;
                                selectedLevelIdStrings.Add(level.Id);
                                break;
                            }
                        }
                    }
                }

                // Determine base level ID to exclude
                string baseLevelIdString = null;
                if (baseLevelId != null)
                {
                    Level baseLevel = _doc.GetElement(baseLevelId) as Level;
                    if (baseLevel != null)
                    {
                        // Find corresponding level in our model
                        foreach (var level in _model.ModelLayout.Levels)
                        {
                            if (baseLevel.Name == level.Name ||
                                Math.Abs(baseLevel.Elevation * 12.0 - level.Elevation) < 0.1)
                            {
                                baseLevelIdString = level.Id;
                                break;
                            }
                        }
                    }
                }

                // Export elements based on filter settings
                if (elementFilters["Walls"])
                {
                    Export.Elements.WallExport wallExport = new Export.Elements.WallExport(_doc);
                    count += wallExport.Export(_model.Elements.Walls, _model);

                    // Filter walls based on TopLevelId
                    if (_model.Elements.Walls != null && _model.Elements.Walls.Count > 0)
                    {
                        if ((selectedLevelIdStrings.Count > 0 || baseLevelIdString != null))
                        {
                            int initialCount = _model.Elements.Walls.Count;

                            // Get a filtered list of walls
                            _model.Elements.Walls = _model.Elements.Walls
                                .Where(wall =>
                                    // Include only walls that have a top level ID
                                    !string.IsNullOrEmpty(wall.TopLevelId) &&
                                    // AND the top level is in our selected levels
                                    (selectedLevelIdStrings.Count == 0 || selectedLevelIdStrings.Contains(wall.TopLevelId)) &&
                                    // AND the top level is not the base level
                                    (baseLevelIdString == null || wall.TopLevelId != baseLevelIdString))
                                .ToList();

                            // Update count to reflect the filtered number
                            count -= (initialCount - _model.Elements.Walls.Count);
                        }
                    }
                }

                if (elementFilters["Floors"])
                {
                    Export.Elements.FloorExport floorExport = new Export.Elements.FloorExport(_doc);
                    count += floorExport.Export(_model.Elements.Floors, _model);

                    // Filter floors based on LevelId
                    if (_model.Elements.Floors != null && _model.Elements.Floors.Count > 0)
                    {
                        if ((selectedLevelIdStrings.Count > 0 || baseLevelIdString != null))
                        {
                            int initialCount = _model.Elements.Floors.Count;

                            // Get a filtered list of floors
                            _model.Elements.Floors = _model.Elements.Floors
                                .Where(floor =>
                                    // Include only floors that have a level ID
                                    !string.IsNullOrEmpty(floor.LevelId) &&
                                    // AND the level is in our selected levels
                                    (selectedLevelIdStrings.Count == 0 || selectedLevelIdStrings.Contains(floor.LevelId)) &&
                                    // AND the level is not the base level
                                    (baseLevelIdString == null || floor.LevelId != baseLevelIdString))
                                .ToList();

                            // Update count to reflect the filtered number
                            count -= (initialCount - _model.Elements.Floors.Count);
                        }
                    }
                }

                // Inside ExportStructuralElements, for the column filtering section:

                if (elementFilters["Columns"])
                {
                    // Step 1: Export columns with normal segmentation
                    Export.Elements.ColumnExport columnExport = new Export.Elements.ColumnExport(_doc);
                    count += columnExport.Export(_model.Elements.Columns, _model);

                    // Step 2: Simple filtering - remove segments entirely below base level
                    if (_model.Elements.Columns != null && _model.Elements.Columns.Count > 0 && baseLevelId != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"COLUMN SIMPLE: Starting with {_model.Elements.Columns.Count} column segments");

                        // Get base level elevation
                        Level baseRevitLevel = _doc.GetElement(baseLevelId) as Level;
                        if (baseRevitLevel != null)
                        {
                            double baseElevation = baseRevitLevel.Elevation * 12.0; // Convert to inches
                            System.Diagnostics.Debug.WriteLine($"COLUMN SIMPLE: Base elevation = {baseElevation}");

                            // Get level elevations
                            Dictionary<string, double> levelElevations = new Dictionary<string, double>();
                            foreach (var level in _model.ModelLayout.Levels)
                            {
                                levelElevations[level.Id] = level.Elevation;
                            }

                            int initialCount = _model.Elements.Columns.Count;

                            // Filter: Keep only segments where the TOP level is at or above base elevation
                            _model.Elements.Columns = _model.Elements.Columns
                                .Where(column =>
                                {
                                    // Keep if we can't determine elevation
                                    if (string.IsNullOrEmpty(column.TopLevelId) ||
                                        !levelElevations.ContainsKey(column.TopLevelId))
                                    {
                                        System.Diagnostics.Debug.WriteLine($"COLUMN SIMPLE: Keeping column - can't determine top elevation");
                                        return true;
                                    }

                                    double topColumnElev = levelElevations[column.TopLevelId];

                                    // Keep if top level is at or above base elevation
                                    bool keep = topColumnElev >= baseElevation;

                                    System.Diagnostics.Debug.WriteLine($"COLUMN SIMPLE: Column top={topColumnElev}, base={baseElevation} -> {(keep ? "KEEP" : "FILTER")}");

                                    return keep;
                                })
                                .ToList();

                            count -= (initialCount - _model.Elements.Columns.Count);
                            System.Diagnostics.Debug.WriteLine($"COLUMN SIMPLE: After filtering, {_model.Elements.Columns.Count} column segments remain");
                        }
                    }
                }

                if (elementFilters["Beams"])
                {
                    // Export beams, but we'll filter them after export
                    Export.Elements.BeamExport beamExport = new Export.Elements.BeamExport(_doc);
                    count += beamExport.Export(_model.Elements.Beams, _model);

                    // Filter out beams not associated with selected levels or associated with base level
                    if (_model.Elements.Beams != null && _model.Elements.Beams.Count > 0)
                    {
                        // If we have selected levels and a base level to filter by
                        if ((selectedLevelIdStrings.Count > 0 || baseLevelIdString != null))
                        {
                            int initialCount = _model.Elements.Beams.Count;

                            // Get a filtered list of beams
                            _model.Elements.Beams = _model.Elements.Beams
                                .Where(beam =>
                                    // Include only beams that have a level ID
                                    !string.IsNullOrEmpty(beam.LevelId) &&
                                    // AND the level is in our selected levels
                                    (selectedLevelIdStrings.Count == 0 || selectedLevelIdStrings.Contains(beam.LevelId)) &&
                                    // AND the level is not the base level
                                    (baseLevelIdString == null || beam.LevelId != baseLevelIdString))
                                .ToList();

                            // Update count to reflect the filtered number
                            count -= (initialCount - _model.Elements.Beams.Count);
                        }
                    }
                }

                if (elementFilters["Braces"])
                {
                    Export.Elements.BraceExport braceExport = new Export.Elements.BraceExport(_doc);
                    count += braceExport.Export(_model.Elements.Braces, _model);

                    // Filter braces - diagnostic version
                    if (_model.Elements.Braces != null && _model.Elements.Braces.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"BRACE DIAGNOSTIC: Starting with {_model.Elements.Braces.Count} braces");

                        // Log selected level IDs
                        System.Diagnostics.Debug.WriteLine($"BRACE DIAGNOSTIC: Selected level IDs: {string.Join(", ", selectedLevelIdStrings)}");
                        System.Diagnostics.Debug.WriteLine($"BRACE DIAGNOSTIC: Base level ID: {baseLevelIdString}");

                        // Log brace properties before filtering
                        foreach (var brace in _model.Elements.Braces)
                        {
                            System.Diagnostics.Debug.WriteLine($"BRACE DIAGNOSTIC: Brace has BaseLevelId={brace.BaseLevelId}, TopLevelId={brace.TopLevelId}");
                        }

                        if ((selectedLevelIdStrings.Count > 0 || baseLevelIdString != null))
                        {
                            int initialCount = _model.Elements.Braces.Count;

                            // Log which braces would be kept or filtered
                            foreach (var brace in _model.Elements.Braces)
                            {
                                bool wouldKeep =
                                    (!string.IsNullOrEmpty(brace.TopLevelId) &&
                                     (selectedLevelIdStrings.Count == 0 || selectedLevelIdStrings.Contains(brace.TopLevelId)) &&
                                     (baseLevelIdString == null || brace.TopLevelId != baseLevelIdString));

                                System.Diagnostics.Debug.WriteLine($"BRACE DIAGNOSTIC: Brace with BaseLevelId={brace.BaseLevelId}, TopLevelId={brace.TopLevelId} would be {(wouldKeep ? "KEPT" : "FILTERED OUT")}");
                            }

                            // Get a filtered list of braces
                            var filteredBraces = _model.Elements.Braces
                                .Where(brace =>
                                    (!string.IsNullOrEmpty(brace.TopLevelId) &&
                                     (selectedLevelIdStrings.Count == 0 || selectedLevelIdStrings.Contains(brace.TopLevelId)) &&
                                     (baseLevelIdString == null || brace.TopLevelId != baseLevelIdString)))
                                .ToList();

                            _model.Elements.Braces = filteredBraces;

                            // Update count to reflect the filtered number
                            count -= (initialCount - _model.Elements.Braces.Count);

                            System.Diagnostics.Debug.WriteLine($"BRACE DIAGNOSTIC: After filtering, {_model.Elements.Braces.Count} braces remain");
                        }
                    }
                }

                if (elementFilters["Footings"])
                {
                    // Export spread footings
                    Export.Elements.IsolatedFootingExport isolatedFootingExport = new Export.Elements.IsolatedFootingExport(_doc);
                    System.Diagnostics.Debug.WriteLine($"Starting isolated footing export, collection initialized: {_model.Elements.IsolatedFootings != null}");
                    int footingsExported = isolatedFootingExport.Export(_model.Elements.IsolatedFootings, _model);
                    System.Diagnostics.Debug.WriteLine($"Finished isolated footing export: {footingsExported} footings exported");
                    count += footingsExported;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ExportStructuralElements: {ex.Message}");
            }

            return count;
        }
    }
}