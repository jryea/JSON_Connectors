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

                // Debug: How many columns do we have?
                int columnCount = columns.Count();
                Console.WriteLine($"Processing {columnCount} columns");

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
                    {
                        Console.WriteLine("Skipping column with incomplete data");
                        continue;
                    }

                    Console.WriteLine($"Processing column: Id={column.Id}, TopLevelId={column.TopLevelId}");

                    // Convert coordinates
                    double x = Math.Round(UnitConversionUtils.ConvertToInches(column.StartPoint.X, _lengthUnit), 6);
                    double y = Math.Round(UnitConversionUtils.ConvertToInches(column.StartPoint.Y, _lengthUnit), 6);
                    Console.WriteLine($"Column coordinates: x={x}, y={y}");

                    // Get base and top levels (for determining span)
                    Level baseLevel = null;
                    if (!string.IsNullOrEmpty(column.BaseLevelId) && levelsById.TryGetValue(column.BaseLevelId, out baseLevel))
                    {
                        Console.WriteLine($"Found base level: {baseLevel.Name}, Elevation: {baseLevel.Elevation}");
                    }
                    else
                    {
                        // If no base level specified, assume the lowest level
                        baseLevel = sortedLevels.FirstOrDefault();
                        Console.WriteLine($"Using lowest level as base: {baseLevel?.Name}, Elevation: {baseLevel?.Elevation}");
                    }

                    if (!levelsById.TryGetValue(column.TopLevelId, out var topLevel))
                    {
                        Console.WriteLine("Could not find top level for column");
                        continue;
                    }
                    Console.WriteLine($"Found top level: {topLevel.Name}, Elevation: {topLevel.Elevation}");

                    // Get all levels that need columns (level 0 is ignored, we're only creating columns at levels > 0)
                    var relevantLevels = sortedLevels.Where(l =>
                        l.Elevation > 0 && // Skip level at elevation 0
                        l.Elevation >= (baseLevel?.Elevation ?? 0) &&
                        l.Elevation <= topLevel.Elevation).ToList();

                    Console.WriteLine($"Found {relevantLevels.Count} relevant levels for this column");
                    foreach (var level in relevantLevels)
                    {
                        Console.WriteLine($"  Relevant level: {level.Id}, Name: {level.Name}, Elevation: {level.Elevation}");
                    }

                    // Get material type
                    EMATERIALTYPES columnMaterial = RAMHelpers.GetRAMMaterialType(
                        column.FramePropertiesId,
                        frameProperties,
                        materials);
                    Console.WriteLine($"Column material type: {columnMaterial}");

                    // Process each relevant level
                    foreach (var level in relevantLevels)
                    {
                        // Get the floor type ID for this level
                        if (!levelToFloorTypeMapping.TryGetValue(level.Id, out string floorTypeId) ||
                            string.IsNullOrEmpty(floorTypeId))
                        {
                            Console.WriteLine($"No floor type mapping found for level {level.Id}");
                            continue;
                        }

                        Console.WriteLine($"Found floor type ID {floorTypeId} for level {level.Id}");

                        // Get RAM floor type for this floor type
                        if (!ramFloorTypeByFloorTypeId.TryGetValue(floorTypeId, out IFloorType ramFloorType))
                        {
                            Console.WriteLine($"No RAM floor type found for floor type {floorTypeId}");
                            continue;
                        }

                        Console.WriteLine($"Found RAM floor type {ramFloorType.strLabel} for floor type ID {floorTypeId}");

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
                            Console.WriteLine($"Attempting to add column to RAM floor type {ramFloorType.strLabel}");
                            ILayoutColumns layoutColumns = ramFloorType.GetLayoutColumns();

                            if (layoutColumns != null)
                            {
                                Console.WriteLine($"Got layoutColumns interface, now adding column at ({x}, {y})");
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
