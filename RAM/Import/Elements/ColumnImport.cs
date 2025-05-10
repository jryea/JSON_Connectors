// ColumnImport.cs - Update to use MaterialProvider
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
        private readonly IModel _model;
        private readonly string _lengthUnit;
        private readonly MaterialProvider _materialProvider;

        public ColumnImport(
            IModel model,
            MaterialProvider materialProvider,
            string lengthUnit = "inches")
        {
            _model = model;
            _materialProvider = materialProvider;
            _lengthUnit = lengthUnit;
        }

        public int Import(IEnumerable<Column> columns, IEnumerable<Level> levels,
                  IEnumerable<FrameProperties> frameProperties,
                  Dictionary<string, string> levelToFloorTypeMapping)
        {
            try
            {
                if (columns == null || !columns.Any() || levels == null || !levels.Any())
                    return 0;

                // Get RAM floor types
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes.GetCount() == 0)
                    return 0;

                // Map Core floor types to RAM floor types
                Dictionary<string, IFloorType> ramFloorTypeByFloorTypeId = new Dictionary<string, IFloorType>();

                // Assign RAM floor types to Core floor types
                for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                {
                    IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                    string coreFloorTypeId = levelToFloorTypeMapping.Values.ElementAtOrDefault(i);
                    if (!string.IsNullOrEmpty(coreFloorTypeId))
                    {
                        ramFloorTypeByFloorTypeId[coreFloorTypeId] = ramFloorType;
                        Console.WriteLine($"Mapped Core floor type {coreFloorTypeId} to RAM floor type {ramFloorType.strLabel}");
                    }
                }

                // Get all levels sorted by elevation
                var sortedLevels = levels.OrderBy(l => l.Elevation).ToList();
                var levelsById = levels.ToDictionary(l => l.Id);

                // Track processed columns per floor type to avoid duplicates
                Dictionary<string, HashSet<string>> processedColumnsByFloorType = new Dictionary<string, HashSet<string>>();

                // Import columns
                int count = 0;
                foreach (var column in columns)
                {
                    if (column.StartPoint == null || string.IsNullOrEmpty(column.TopLevelId))
                        continue;

                    Console.WriteLine($"Processing column: Id={column.Id}, TopLevelId={column.TopLevelId}");

                    // Convert coordinates
                    double x = Math.Round(UnitConversionUtils.ConvertToInches(column.StartPoint.X, _lengthUnit), 6);
                    double y = Math.Round(UnitConversionUtils.ConvertToInches(column.StartPoint.Y, _lengthUnit), 6);

                    // Get the top level
                    if (!levelsById.TryGetValue(column.TopLevelId, out Level topLevel))
                    {
                        Console.WriteLine($"Could not find top level with ID {column.TopLevelId}");
                        continue;
                    }

                    // Get base level (if specified)
                    Level baseLevel = null;
                    if (!string.IsNullOrEmpty(column.BaseLevelId) && levelsById.TryGetValue(column.BaseLevelId, out baseLevel))
                    {
                        Console.WriteLine($"Found base level: {baseLevel.Name}, Elevation: {baseLevel.Elevation}");
                    }

                    // Get all levels that need columns
                    List<Level> levelsForColumns = new List<Level>();

                    // Start with the top level (always create a column there if it has positive elevation)
                    if (topLevel.Elevation > 0)
                    {
                        levelsForColumns.Add(topLevel);
                        Console.WriteLine($"Will create column at top level: {topLevel.Name}");
                    }

                    // Add intermediate levels only if baseLevel exists and isn't at elevation 0
                    if (baseLevel != null && baseLevel.Elevation > 0)
                    {
                        foreach (var level in sortedLevels)
                        {
                            if (level.Id != topLevel.Id &&
                                level.Id != baseLevel.Id &&
                                level.Elevation > baseLevel.Elevation &&
                                level.Elevation < topLevel.Elevation)
                            {
                                levelsForColumns.Add(level);
                                Console.WriteLine($"Will create column at intermediate level: {level.Name}");
                            }
                        }
                    }
                    // If baseLevel is at elevation 0 or doesn't exist, add intermediate levels below top level
                    else
                    {
                        foreach (var level in sortedLevels)
                        {
                            if (level.Id != topLevel.Id &&
                                level.Elevation > 0 &&
                                level.Elevation < topLevel.Elevation)
                            {
                                levelsForColumns.Add(level);
                                Console.WriteLine($"Will create column at intermediate level: {level.Name}");
                            }
                        }
                    }

                    // Get material type using MaterialProvider
                    EMATERIALTYPES columnMaterial = _materialProvider.GetRAMMaterialType(
                        column.FramePropertiesId,
                        frameProperties);

                    // Process each level that needs a column
                    foreach (var level in levelsForColumns)
                    {
                        // Get the floor type ID for this level
                        if (!levelToFloorTypeMapping.TryGetValue(level.Id, out string floorTypeId) ||
                            string.IsNullOrEmpty(floorTypeId))
                        {
                            Console.WriteLine($"No floor type mapping found for level {level.Id}");
                            continue;
                        }

                        // Get RAM floor type for this floor type
                        if (!ramFloorTypeByFloorTypeId.TryGetValue(floorTypeId, out IFloorType ramFloorType))
                        {
                            Console.WriteLine($"No RAM floor type found for floor type {floorTypeId}");
                            continue;
                        }

                        // Create a unique key for this column
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
                                    Console.WriteLine($"SUCCESS: Added column to floor type {ramFloorType.strLabel} for level {level.Name}");
                                }
                                else
                                {
                                    Console.WriteLine($"FAILURE: ramColumn is null after adding column to floor type {ramFloorType.strLabel}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"FAILURE: layoutColumns is null for floor type {ramFloorType.strLabel}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"ERROR creating column: {ex.Message}");
                        }
                    }
                }

                Console.WriteLine($"Total columns created: {count}");
                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR importing columns: {ex.Message}");
                throw;
            }
        }
    }
}