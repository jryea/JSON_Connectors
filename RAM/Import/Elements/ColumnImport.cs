// ColumnImport.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Utilities;
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

        public int Import(IEnumerable<Column> columns, IEnumerable<Level> levels,
                  IEnumerable<FrameProperties> frameProperties,
                  IEnumerable<Material> materials,
                  Dictionary<string, string> levelToFloorTypeMapping)
        {
            try
            {
                if (columns == null || !columns.Any() || levels == null || !levels.Any())
                    return 0;

                // Convert levels to a sorted list for elevation comparison
                var sortedLevels = levels.OrderBy(l => l.Elevation).ToList();

                // Get RAM floor types
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes.GetCount() == 0)
                    return 0;

                // Create dictionary to map FloorTypeId to RAM IFloorType
                Dictionary<string, IFloorType> floorTypeIdToRamFloorType = new Dictionary<string, IFloorType>();

                // Map each RAM floor type to its corresponding Core FloorTypeId
                for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                {
                    IFloorType ramFloorType = ramFloorTypes.GetAt(i);

                    // Find the corresponding Core FloorTypeId for this RAM floor type
                    foreach (var mapping in levelToFloorTypeMapping)
                    {
                        string levelId = mapping.Key;
                        string floorTypeId = mapping.Value;

                        // Check if we already mapped this floor type
                        if (!floorTypeIdToRamFloorType.ContainsKey(floorTypeId))
                        {
                            // Skip levels at elevation 0
                            var level = sortedLevels.FirstOrDefault(l => l.Id == levelId);
                            if (level != null && level.Elevation > 0)
                            {
                                if (i < ramFloorTypes.GetCount())
                                {
                                    floorTypeIdToRamFloorType[floorTypeId] = ramFloorType;
                                    Console.WriteLine($"Mapped FloorTypeId {floorTypeId} to RAM floor type {ramFloorType.strLabel}");
                                    break; // Move to next RAM floor type
                                }
                            }
                        }
                    }
                }

                // Track processed columns per floor type to avoid duplicates
                Dictionary<string, HashSet<string>> processedColumnsByFloorType = new Dictionary<string, HashSet<string>>();

                // Import columns
                int count = 0;
                foreach (var column in columns)
                {
                    if (column.StartPoint == null || string.IsNullOrEmpty(column.TopLevelId) ||
                        string.IsNullOrEmpty(column.BaseLevelId))
                        continue;

                    // Get base and top level
                    Level baseLevel = sortedLevels.FirstOrDefault(l => l.Id == column.BaseLevelId);
                    Level topLevel = sortedLevels.FirstOrDefault(l => l.Id == column.TopLevelId);

                    if (baseLevel == null || topLevel == null)
                    {
                        Console.WriteLine($"Could not find base or top level for column");
                        continue;
                    }

                    // Find intermediate levels (excluding base level if it's at elevation 0)
                    var intermediateLevels = sortedLevels.Where(l =>
                        l.Elevation > 0 && // Skip level at elevation 0
                        l.Elevation >= baseLevel.Elevation &&
                        l.Elevation <= topLevel.Elevation).ToList();

                    // Convert coordinates
                    double x = UnitConversionUtils.ConvertToInches(column.StartPoint.X, _lengthUnit);
                    double y = UnitConversionUtils.ConvertToInches(column.StartPoint.Y, _lengthUnit);

                    // Get material type
                    EMATERIALTYPES columnMaterial = RAMHelpers.GetRAMMaterialType(
                        column.FramePropertiesId,
                        frameProperties,
                        materials);

                    // Process each intermediate level
                    foreach (var level in intermediateLevels)
                    {
                        // Get the floor type ID for this level
                        if (!levelToFloorTypeMapping.TryGetValue(level.Id, out string floorTypeId) ||
                            string.IsNullOrEmpty(floorTypeId))
                        {
                            Console.WriteLine($"No floor type mapping found for level {level.Id} with elevation {level.Elevation}");
                            continue;
                        }

                        // Get RAM floor type for this floor type ID
                        if (!floorTypeIdToRamFloorType.TryGetValue(floorTypeId, out IFloorType ramFloorType))
                        {
                            Console.WriteLine($"No RAM floor type found for floor type ID {floorTypeId}");
                            continue;
                        }

                        // Create a unique key for this column on this floor type
                        string columnKey = $"{x:F2}_{y:F2}_{floorTypeId}";

                        // Check if this column already exists in this floor type
                        if (!processedColumnsByFloorType.TryGetValue(floorTypeId, out var processedColumns))
                        {
                            processedColumns = new HashSet<string>();
                            processedColumnsByFloorType[floorTypeId] = processedColumns;
                        }

                        if (processedColumns.Contains(columnKey))
                        {
                            Console.WriteLine($"Skipping duplicate column on floor type {floorTypeId}");
                            continue;
                        }

                        // Add the column to the processed set
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
                                    Console.WriteLine($"Added column to floor type {ramFloorType.strLabel} for level {level.Name} at elevation {level.Elevation}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error creating column: {ex.Message}");
                        }
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
    }
}
