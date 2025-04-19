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

                // Track processed columns per floor type to avoid duplicates
                Dictionary<string, HashSet<string>> processedColumnsByFloorType = new Dictionary<string, HashSet<string>>();

                // Import columns
                int count = 0;
                foreach (var column in columns)
                {
                    if (column.StartPoint == null || string.IsNullOrEmpty(column.TopLevelId))
                        continue;

                    // Get the floor type ID for the column's base level
                    if (!levelToFloorTypeMapping.TryGetValue(column.TopLevelId, out string floorTypeId) ||
                        string.IsNullOrEmpty(floorTypeId))
                    {
                        Console.WriteLine($"No floor type mapping found for base level {column.TopLevelId}");
                        continue;
                    }

                    // Get RAM floor type for this floor type
                    if (!ramFloorTypeByFloorTypeId.TryGetValue(floorTypeId, out IFloorType ramFloorType))
                    {
                        Console.WriteLine($"No RAM floor type found for floor type {floorTypeId}");
                        continue;
                    }

                    // Convert coordinates
                    double x = UnitConversionUtils.ConvertToInches(column.StartPoint.X, _lengthUnit);
                    double y = UnitConversionUtils.ConvertToInches(column.StartPoint.Y, _lengthUnit);

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

                    // Get material type
                    EMATERIALTYPES columnMaterial = ImportHelpers.GetRAMMaterialType(
                        column.FramePropertiesId,
                        frameProperties,
                        materials);

                    try
                    {
                        ILayoutColumns layoutColumns = ramFloorType.GetLayoutColumns();
                        if (layoutColumns != null)
                        {
                            ILayoutColumn ramColumn = layoutColumns.Add(columnMaterial, x, y, 0, 0);
                            if (ramColumn != null)
                            {
                                count++;
                                Console.WriteLine($"Added column to floor type {ramFloorType.strLabel}");
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
    }
}
