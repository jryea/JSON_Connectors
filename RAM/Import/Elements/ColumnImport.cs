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
    /// <summary>
    /// Imports column elements to RAM from the Core model
    /// </summary>
    public class ColumnImport
    {
        private IModel _model;
        private string _lengthUnit;
        private Dictionary<string, IFloorType> _floorTypeByLevelId = new Dictionary<string, IFloorType>();

        /// <summary>
        /// Initializes a new instance of the ColumnImport class
        /// </summary>
        /// <param name="model">The RAM model</param>
        /// <param name="lengthUnit">The length unit used in the model</param>
        public ColumnImport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        /// <summary>
        /// Imports columns to RAM model
        /// </summary>
        /// <param name="columns">The collection of columns to import</param>
        /// <param name="levels">The collection of levels in the model</param>
        /// <param name="frameProperties">The collection of frame properties in the model</param>
        /// <returns>The number of columns successfully imported</returns>
        public int Import(IEnumerable<Column> columns, IEnumerable<Level> levels, IEnumerable<FrameProperties> frameProperties)
        {
            try
            {
                // First, ensure we have valid input
                if (columns == null || !columns.Any())
                {
                    Console.WriteLine("No columns to import.");
                    return 0;
                }

                if (levels == null || !levels.Any())
                {
                    Console.WriteLine("No levels available for column import.");
                    return 0;
                }

                // Get all available floor types from RAM
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes.GetCount() == 0)
                {
                    Console.WriteLine("Error: No floor types found in RAM model");
                    return 0;
                }

                // Use the first floor type as default
                IFloorType defaultFloorType = ramFloorTypes.GetAt(0);
                Console.WriteLine($"Using default floor type: {defaultFloorType.strLabel} (ID: {defaultFloorType.lUID})");

                // Create mappings for levels and materials
                Dictionary<string, Level> levelMap = new Dictionary<string, Level>();
                Dictionary<string, EMATERIALTYPES> materialsMap = new Dictionary<string, EMATERIALTYPES>();
                Dictionary<string, Level> levelById = new Dictionary<string, Level>();

                // Build level mapping
                foreach (var level in levels)
                {
                    if (!string.IsNullOrEmpty(level.Id))
                    {
                        levelMap[level.Id] = level;
                        levelById[level.Id] = level;

                        // Setup floor type mapping for each level
                        if (string.IsNullOrEmpty(level.FloorTypeId))
                        {
                            _floorTypeByLevelId[level.Id] = defaultFloorType;
                        }
                        else
                        {
                            // In a real implementation, match by level's floor type ID
                            // For now, just use the first floor type
                            _floorTypeByLevelId[level.Id] = ramFloorTypes.GetAt(0);
                        }
                    }
                }

                // Build materials mapping
                foreach (var frameProp in frameProperties)
                {
                    if (!string.IsNullOrEmpty(frameProp.Id))
                    {
                        EMATERIALTYPES materialType = EMATERIALTYPES.ESteelMat; // Default to steel

                        if (frameProp.MaterialId != null)
                        {
                            // Try to determine material from material ID
                            string materialName = frameProp.MaterialId?.ToLower() ?? "";
                            if (materialName.Contains("concrete"))
                            {
                                materialType = EMATERIALTYPES.EConcreteMat;
                            }
                        }

                        materialsMap[frameProp.Id] = materialType;
                    }
                }

                // Now process and import the columns
                int count = 0;
                foreach (var column in columns)
                {
                    if (column.StartPoint == null || column.EndPoint == null)
                    {
                        Console.WriteLine($"Skipping column {column.Id}: Missing start or end point");
                        continue;
                    }

                    // Get the floor type for the top level of the column
                    IFloorType floorType = null;

                    if (!string.IsNullOrEmpty(column.TopLevelId) && _floorTypeByLevelId.TryGetValue(column.TopLevelId, out floorType))
                    {
                        // We found the floor type directly
                    }
                    else if (levelById.Count > 0)
                    {
                        // Use the first available floor type if no specific mapping
                        floorType = _floorTypeByLevelId.Values.FirstOrDefault() ?? defaultFloorType;
                    }
                    else
                    {
                        floorType = defaultFloorType;
                    }

                    // Get column material
                    EMATERIALTYPES columnMaterial = EMATERIALTYPES.ESteelMat; // Default
                    if (!string.IsNullOrEmpty(column.FramePropertiesId) &&
                        materialsMap.TryGetValue(column.FramePropertiesId, out EMATERIALTYPES material))
                    {
                        columnMaterial = material;
                    }

                    // Convert coordinates to inches
                    double colX = Helpers.ConvertToInches(column.StartPoint.X, _lengthUnit);
                    double colY = Helpers.ConvertToInches(column.StartPoint.Y, _lengthUnit);
                    double colTopZ = 0;    // In RAM, the Z coordinate is determined by the level
                    double colBaseZ = 0;   // We'll use 0 since RAM manages the heights internally

                    try
                    {
                        // Create the column in RAM
                        ILayoutColumns layoutColumns = floorType.GetLayoutColumns();
                        if (layoutColumns == null)
                        {
                            Console.WriteLine($"Error: GetLayoutColumns() returned null for floor type {floorType.strLabel}");
                            continue;
                        }

                        ILayoutColumn ramColumn = layoutColumns.Add(columnMaterial, colX, colY, colTopZ, colBaseZ);

                        if (ramColumn != null)
                        {
                            count++;
                            Console.WriteLine($"Successfully created column {count}");
                        }
                        else
                        {
                            Console.WriteLine("Error: RAM returned null column");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating column: {ex.Message}");
                        // Continue with next column instead of failing the whole import
                    }
                }

                Console.WriteLine($"Successfully imported {count} columns");
                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing columns: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}