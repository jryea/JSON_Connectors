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

                Console.WriteLine("Beginning column import with corrected floor type mapping...");

                // First, create a mapping from level ID to its floor type ID
                var levelIdToFloorTypeId = new Dictionary<string, string>();
                foreach (var level in levels)
                {
                    if (!string.IsNullOrEmpty(level.Id) && !string.IsNullOrEmpty(level.FloorTypeId))
                    {
                        levelIdToFloorTypeId[level.Id] = level.FloorTypeId;
                    }
                }

                // Now create a direct mapping from floor type ID to RAM floor type
                var floorTypeIdToRamFloorType = new Dictionary<string, IFloorType>();

                // Build this map systematically, ensuring correct order
                var sortedFloorTypes = new List<string>(levelToFloorTypeMapping.Values.Distinct());
                Console.WriteLine($"Found {sortedFloorTypes.Count} distinct floor types in mapping");

                // Make sure we have floor types to map from
                if (sortedFloorTypes.Count > 0)
                {
                    // Print out the available RAM floor types
                    Console.WriteLine($"Available RAM floor types ({ramFloorTypes.GetCount()}):");
                    for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                    {
                        IFloorType floorType = ramFloorTypes.GetAt(i);
                        Console.WriteLine($"  {i}: {floorType.strLabel} (UID: {floorType.lUID})");
                    }

                    // Map in order - the key is to match them in the correct order
                    for (int i = 0; i < Math.Min(sortedFloorTypes.Count, ramFloorTypes.GetCount()); i++)
                    {
                        string floorTypeId = sortedFloorTypes[i];
                        IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                        floorTypeIdToRamFloorType[floorTypeId] = ramFloorType;
                        Console.WriteLine($"Mapped floor type ID {floorTypeId} to RAM floor type {ramFloorType.strLabel} (UID: {ramFloorType.lUID})");
                    }
                }
                else
                {
                    Console.WriteLine("ERROR: No floor types found in level-to-floor-type mapping");
                    return 0;
                }

                // Track processed columns per floor type to avoid duplicates
                var processedColumnsByFloorType = new Dictionary<int, HashSet<string>>();

                // Import columns
                int count = 0;
                foreach (var column in columns)
                {
                    if (column.StartPoint == null || string.IsNullOrEmpty(column.TopLevelId))
                        continue;

                    // Get the floor type ID for this column's top level
                    if (!levelIdToFloorTypeId.TryGetValue(column.TopLevelId, out string floorTypeId))
                    {
                        Console.WriteLine($"No floor type mapping found for column top level {column.TopLevelId}, skipping");
                        continue;
                    }

                    // Get the RAM floor type for this floor type ID
                    if (!floorTypeIdToRamFloorType.TryGetValue(floorTypeId, out IFloorType ramFloorType))
                    {
                        Console.WriteLine($"No RAM floor type found for floor type ID {floorTypeId}, skipping");
                        continue;
                    }

                    Console.WriteLine($"Processing column on top level ID {column.TopLevelId}, floor type ID {floorTypeId}, RAM floor type {ramFloorType.strLabel}");

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
                                // Set the column properties
                                if (column.IsLateral)
                                {
                                    ramColumn.eFramingType = EFRAMETYPE.MemberIsLateral;
                                }

                                if (column.Orientation != 0.0)
                                {
                                    ramColumn.dOrientation = column.Orientation;
                                }

                                // Set section label if available via frame properties
                                if (!string.IsNullOrEmpty(column.FramePropertiesId))
                                {
                                    var frameProp = frameProperties?.FirstOrDefault(fp => fp.Id == column.FramePropertiesId);
                                    if (frameProp != null && !string.IsNullOrEmpty(frameProp.Name))
                                    {
                                        ramColumn.strSectionLabel = frameProp.Name;
                                    }
                                    else
                                    {
                                        ramColumn.strSectionLabel = "W10X49"; // Default if not found
                                    }
                                }

                                count++;
                                Console.WriteLine($"Added column to floor type {ramFloorType.strLabel} for level ID {column.TopLevelId}");
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