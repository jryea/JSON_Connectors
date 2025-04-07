// ColumnImport.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.Elements
{
    public class ColumnImport
    {
        private IModel _model;
        private string _lengthUnit;

        public ColumnImport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public int Import(IEnumerable<Column> columns, IEnumerable<Level> levels, IEnumerable<FrameProperties> frameProperties)
        {
            try
            {
                if (columns == null || !columns.Any() || levels == null || !levels.Any())
                    return 0;

                // Get RAM floor types
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes.GetCount() == 0)
                    return 0;

                // Create a map of level IDs to Level objects
                Dictionary<string, Level> levelById = levels.ToDictionary(l => l.Id);

                // Sort levels by elevation
                var sortedLevels = levels.OrderBy(l => l.Elevation).ToList();

                // Map floor types to their "master" level (first level with that floor type)
                Dictionary<string, Level> masterLevelByFloorTypeId = new Dictionary<string, Level>();

                // Find the first (lowest) level for each floor type - this will be the "master" level
                foreach (var level in sortedLevels)
                {
                    if (!string.IsNullOrEmpty(level.FloorTypeId) && !masterLevelByFloorTypeId.ContainsKey(level.FloorTypeId))
                    {
                        masterLevelByFloorTypeId[level.FloorTypeId] = level;
                        Console.WriteLine($"Floor type {level.FloorTypeId} has master level {level.Name} at elevation {level.Elevation}");
                    }
                }

                // Map Core floor types to RAM floor types
                Dictionary<string, IFloorType> ramFloorTypeByFloorTypeId = new Dictionary<string, IFloorType>();

                // Assign RAM floor types to Core floor types
                for (int i = 0; i < ramFloorTypes.GetCount() && i < masterLevelByFloorTypeId.Count; i++)
                {
                    IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                    string coreFloorTypeId = masterLevelByFloorTypeId.Keys.ElementAt(i);
                    ramFloorTypeByFloorTypeId[coreFloorTypeId] = ramFloorType;
                    Console.WriteLine($"Mapped Core floor type {coreFloorTypeId} to RAM floor type {ramFloorType.strLabel}");
                }

                // Map materials
                var materialMap = MapMaterials(frameProperties);

                // Track processed columns to avoid duplicates
                HashSet<string> processedColumns = new HashSet<string>();

                // Process columns
                int count = 0;
                foreach (var column in columns)
                {
                    if (column.StartPoint == null || column.EndPoint == null)
                        continue;

                    // Get the TOP level for this column (similar to beam's level)
                    Level topLevel = null;
                    if (!string.IsNullOrEmpty(column.TopLevelId) && levelById.TryGetValue(column.TopLevelId, out topLevel))
                    {
                        // Great, we have the top level
                    }
                    else
                    {
                        // If no top level specified, try the highest level
                        topLevel = sortedLevels.LastOrDefault();
                        if (topLevel == null)
                        {
                            Console.WriteLine($"Skipping column {column.Id}: No levels found");
                            continue;
                        }
                    }

                    // Get the floor type ID for the top level
                    string floorTypeId = topLevel.FloorTypeId;
                    if (string.IsNullOrEmpty(floorTypeId))
                    {
                        Console.WriteLine($"Skipping column {column.Id}: Top level has no floor type");
                        continue;
                    }

                    // Check if this is the master level for this floor type
                    if (!masterLevelByFloorTypeId.TryGetValue(floorTypeId, out Level masterLevel))
                    {
                        Console.WriteLine($"Skipping column {column.Id}: No master level found for floor type {floorTypeId}");
                        continue;
                    }

                    // Get the RAM floor type
                    if (!ramFloorTypeByFloorTypeId.TryGetValue(floorTypeId, out IFloorType ramFloorType))
                    {
                        Console.WriteLine($"Skipping column {column.Id}: No RAM floor type found for floor type {floorTypeId}");
                        continue;
                    }

                    // Get material type
                    EMATERIALTYPES columnMaterial = EMATERIALTYPES.ESteelMat; // Default to steel
                    if (!string.IsNullOrEmpty(column.FramePropertiesId))
                        materialMap.TryGetValue(column.FramePropertiesId, out columnMaterial);

                    // Convert coordinates
                    double x = Helpers.ConvertToInches(column.StartPoint.X, _lengthUnit);
                    double y = Helpers.ConvertToInches(column.StartPoint.Y, _lengthUnit);

                    // Create a unique key for this column
                    string columnKey = $"{x:F2}_{y:F2}_{columnMaterial}_{floorTypeId}";

                    // Skip if we've already processed this column on this floor type
                    if (processedColumns.Contains(columnKey))
                    {
                        Console.WriteLine($"Skipping duplicate column in floor type {ramFloorType.strLabel}");
                        continue;
                    }

                    // Add to processed set
                    processedColumns.Add(columnKey);

                    try
                    {
                        ILayoutColumns layoutColumns = ramFloorType.GetLayoutColumns();
                        if (layoutColumns != null)
                        {
                            ILayoutColumn ramColumn = layoutColumns.Add(columnMaterial, x, y, 0, 0);
                            if (ramColumn != null)
                            {
                                count++;
                                Console.WriteLine($"Added column to floor type {ramFloorType.strLabel} (master level: {masterLevel.Name})");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating column: {ex.Message}");
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing columns: {ex.Message}");
                throw;
            }
        }

        private Dictionary<string, EMATERIALTYPES> MapMaterials(IEnumerable<FrameProperties> frameProperties)
        {
            var map = new Dictionary<string, EMATERIALTYPES>();

            foreach (var prop in frameProperties ?? Enumerable.Empty<FrameProperties>())
            {
                if (string.IsNullOrEmpty(prop.Id))
                    continue;

                EMATERIALTYPES type = EMATERIALTYPES.ESteelMat;

                if (prop.MaterialId != null)
                {
                    // Try to determine material type from material ID or properties
                    if (prop.MaterialId.ToLower().Contains("concrete"))
                    {
                        type = EMATERIALTYPES.EConcreteMat;
                    }
                }

                map[prop.Id] = type;
            }

            return map;
        }
    }
}