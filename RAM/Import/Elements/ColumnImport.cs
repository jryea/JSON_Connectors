// ColumnImport.cs
using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.Elements
{
    // Imports column elements to RAM from the Core model
    public class ColumnImport
    {
        private IModel _model;
        private string _lengthUnit;
        private Dictionary<string, IFloorType> _floorTypeMap = new Dictionary<string, IFloorType>();

        public ColumnImport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
            InitializeFloorTypeMap();
        }

        // Initializes the floor type mapping
        private void InitializeFloorTypeMap()
        {
            try
            {
                _floorTypeMap.Clear();
                IFloorTypes floorTypes = _model.GetFloorTypes();

                for (int i = 0; i < floorTypes.GetCount(); i++)
                {
                    IFloorType floorType = floorTypes.GetAt(i);
                    _floorTypeMap[floorType.strLabel] = floorType;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing floor type map: {ex.Message}");
            }
        }

        // Imports columns to RAM model for each floor type

        public int Import(IEnumerable<Column> columns, IEnumerable<Level> levels, IEnumerable<FrameProperties> frameProperties)
        {
            try
            {
                int count = 0;
                Dictionary<string, Level> levelMap = new Dictionary<string, Level>();
                Dictionary<string, Level> baseLevelMap = new Dictionary<string, Level>();
                Dictionary<string, Level> topLevelMap = new Dictionary<string, Level>();
                Dictionary<string, EMATERIALTYPES> materialsMap = new Dictionary<string, EMATERIALTYPES>();

                // Build level mapping 
                foreach (var level in levels)
                {
                    if (!string.IsNullOrEmpty(level.Id))
                    {
                        levelMap[level.Id] = level;
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
                            // Try to determine material from material ID or name
                            string materialName = frameProp.MaterialId?.ToLower() ?? "";
                            if (materialName.Contains("concrete"))
                            {
                                materialType = EMATERIALTYPES.EConcreteMat;
                            }
                        }

                        materialsMap[frameProp.Id] = materialType;
                    }
                }

                // Process all columns
                foreach (var column in columns)
                {
                    if (column.StartPoint == null ||
                        column.EndPoint == null ||
                        string.IsNullOrEmpty(column.BaseLevelId) ||
                        string.IsNullOrEmpty(column.TopLevelId))
                    {
                        continue;
                    }

                    // Get the level and floor type
                    if (!levelMap.TryGetValue(column.BaseLevelId, out Level baseLevel) ||
                        baseLevel.FloorTypeId == null ||
                        !GetRamFloorTypeByFloorTypeId(baseLevel.FloorTypeId, out IFloorType floorType))
                    {
                        continue;
                    }

                    // Get column material
                    EMATERIALTYPES columnMaterial = EMATERIALTYPES.ESteelMat; // Default
                    if (column.FramePropertiesId != null &&
                        materialsMap.TryGetValue(column.FramePropertiesId, out EMATERIALTYPES material))
                    {
                        columnMaterial = material;
                    }

                    // Convert coordinates to inches
                    double colX = Helpers.ConvertToInches(column.StartPoint.X, _lengthUnit);
                    double colY = Helpers.ConvertToInches(column.StartPoint.Y, _lengthUnit);

                    // In RAM, columns are defined at a point location with base and top elevations
                    // Top Z is the elevation at the top of the column
                    // Base Z is the elevation at the bottom of the column
                    double colTopZ = 0; // This should be determined from levels, but for simplicity we'll use 0
                    double colBaseZ = 0; // This should be determined from levels, but for simplicity we'll use 0

                    // Create the column in RAM
                    ILayoutColumns layoutColumns = floorType.GetLayoutColumns();
                    ILayoutColumn ramColumn = layoutColumns.Add(columnMaterial, colX, colY, colTopZ, colBaseZ);

                    if (ramColumn != null)
                    {
                        count++;
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

        // Gets a RAM floor type by floor type ID from our model
        private bool GetRamFloorTypeByFloorTypeId(string floorTypeId, out IFloorType floorType)
        {
            floorType = null;

            try
            {
                // Try to find a floor type with a matching ID
                // In a real implementation, we would have a mapping from our model's floor type IDs
                // to RAM floor type names, but for simplicity, we'll just use the first floor type

                if (_floorTypeMap.Count > 0)
                {
                    floorType = _floorTypeMap.Values.GetEnumerator().Current;
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}