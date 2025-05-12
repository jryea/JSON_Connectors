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

                Console.WriteLine("Beginning column import with top-down floor type mapping...");

                // Group levels by floor type ID
                var levelsByFloorType = new Dictionary<string, List<Level>>();
                foreach (var level in levels)
                {
                    if (string.IsNullOrEmpty(level.Id) || string.IsNullOrEmpty(level.FloorTypeId))
                        continue;

                    if (!levelsByFloorType.ContainsKey(level.FloorTypeId))
                    {
                        levelsByFloorType[level.FloorTypeId] = new List<Level>();
                    }

                    levelsByFloorType[level.FloorTypeId].Add(level);
                }

                // For each floor type, identify the highest level
                var highestLevelByFloorType = new Dictionary<string, Level>();
                foreach (var entry in levelsByFloorType)
                {
                    var floorTypeId = entry.Key;
                    var levelsWithThisFloorType = entry.Value;

                    if (levelsWithThisFloorType.Count > 0)
                    {
                        // Find the highest level (highest elevation)
                        var highestLevel = levelsWithThisFloorType.OrderByDescending(l => l.Elevation).First();
                        highestLevelByFloorType[floorTypeId] = highestLevel;

                        Console.WriteLine($"FloorType {floorTypeId} uses highest level: {highestLevel.Name} (Elevation: {highestLevel.Elevation})");
                    }
                }

                // Create mapping from RAM floor type UID to RAM floor type
                var ramFloorTypeByUID = new Dictionary<int, IFloorType>();
                for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                {
                    IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                    ramFloorTypeByUID[ramFloorType.lUID] = ramFloorType;
                }

                // Map Core floor type IDs to RAM floor types
                var coreFloorTypeToRamFloorType = new Dictionary<string, IFloorType>();
                int ftIndex = 0;
                foreach (var floorTypeId in highestLevelByFloorType.Keys)
                {
                    if (ftIndex < ramFloorTypes.GetCount())
                    {
                        IFloorType ramFloorType = ramFloorTypes.GetAt(ftIndex);
                        coreFloorTypeToRamFloorType[floorTypeId] = ramFloorType;
                        Console.WriteLine($"Mapped Core floor type {floorTypeId} to RAM floor type {ramFloorType.strLabel} (UID: {ramFloorType.lUID})");
                        ftIndex++;
                    }
                }

                // Create a mapping from level ID to RAM floor type (only for highest levels of each floor type)
                var levelIdToRamFloorType = new Dictionary<string, IFloorType>();
                foreach (var entry in highestLevelByFloorType)
                {
                    string floorTypeId = entry.Key;
                    Level highestLevel = entry.Value;

                    if (coreFloorTypeToRamFloorType.TryGetValue(floorTypeId, out IFloorType ramFloorType))
                    {
                        levelIdToRamFloorType[highestLevel.Id] = ramFloorType;
                        Console.WriteLine($"Level {highestLevel.Name} (ID: {highestLevel.Id}) will use RAM floor type {ramFloorType.strLabel}");
                    }
                }

                // Track processed columns per floor type to avoid duplicates
                var processedColumnsByFloorType = new Dictionary<int, HashSet<string>>();

                // Import columns
                int count = 0;
                foreach (var column in columns)
                {
                    if (column.StartPoint == null || string.IsNullOrEmpty(column.TopLevelId))
                        continue;

                    // Only process columns on levels that are the highest for their floor type
                    if (!levelIdToRamFloorType.TryGetValue(column.TopLevelId, out IFloorType ramFloorType))
                    {
                        // Skip columns not on highest level for their floor type
                        continue;
                    }

                    // Convert coordinates
                    double x = Math.Round(UnitConversionUtils.ConvertToInches(column.StartPoint.X, _lengthUnit), 6);
                    double y = Math.Round(UnitConversionUtils.ConvertToInches(column.StartPoint.Y, _lengthUnit), 6);

                    // Create a unique key for this column
                    string columnKey = $"{x:F2}_{y:F2}";

                    // Check if this column already exists in this floor type
                    int floorTypeUid = ramFloorType.lUID;
                    if (!processedColumnsByFloorType.TryGetValue(floorTypeUid, out var processedColumns))
                    {
                        processedColumns = new HashSet<string>();
                        processedColumnsByFloorType[floorTypeUid] = processedColumns;
                    }

                    if (processedColumns.Contains(columnKey))
                    {
                        Console.WriteLine($"Skipping duplicate column on floor type {ramFloorType.strLabel}");
                        continue;
                    }

                    // Add the column to the processed set
                    processedColumns.Add(columnKey);

                    // Get material type using MaterialProvider
                    EMATERIALTYPES columnMaterial = _materialProvider.GetRAMMaterialType(
                        column.FramePropertiesId,
                        frameProperties);

                    try
                    {
                        // Get layout columns for this floor type
                        ILayoutColumns layoutColumns = ramFloorType.GetLayoutColumns();
                        if (layoutColumns != null)
                        {
                            // Add the column to the layout
                            ILayoutColumn ramColumn = layoutColumns.Add(columnMaterial, x, y, 0, 0);
                            if (ramColumn != null)
                            {
                                // Set the beam properties
                                if (column.IsLateral)
                                {
                                    ramColumn.eFramingType = EFRAMETYPE.MemberIsLateral;
                                }
                                count++;
                                Console.WriteLine($"Added column to floor type {ramFloorType.strLabel} for level {column.TopLevelId}");
                            }
                            else
                            {
                                Console.WriteLine($"Failed to create column in floor type {ramFloorType.strLabel}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Could not get layout columns for floor type {ramFloorType.strLabel}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating column: {ex.Message}");
                    }
                }
                Console.WriteLine($"Imported {count} columns");
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