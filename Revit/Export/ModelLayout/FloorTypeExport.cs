using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DB = Autodesk.Revit.DB;
using Core.Models.ModelLayout;
using Core.Utilities;

namespace Revit.Export.ModelLayout
{
    // Exports floor types from the WPF form inputs and associates them with levels
    public class FloorTypeExport
    {
        private readonly DB.Document _doc;

        public FloorTypeExport(DB.Document doc)
        {
            _doc = doc;
        }

        // Exports floor types based on user input and associates them with levels
        
        public int Export(List<FloorType> floorTypes, List<Level> levels)
        {
            if (floorTypes == null || floorTypes.Count == 0)
            {
                Debug.WriteLine("No floor types to export");
                return 0;
            }

            int count = 0;

            try
            {
                // Export all floor types to the model
                foreach (var floorType in floorTypes)
                {
                    // Ensure proper ID is set
                    if (string.IsNullOrEmpty(floorType.Id))
                    {
                        floorType.Id = IdGenerator.Generate(IdGenerator.Layout.FLOOR_TYPE);
                    }

                    count++;
                    Debug.WriteLine($"Exported floor type: {floorType.Name} ({floorType.Id})");
                }

                // Associate floor types with levels based on mappings
                AssociateLevelsWithFloorTypes(levels, floorTypes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting floor types: {ex.Message}");
            }

            return count;
        }

        // Associates floor types with levels based on specified mappings
       
        public void AssociateLevelsWithFloorTypes(List<Level> levels, List<FloorType> floorTypes,
                                                 Dictionary<string, string> levelToFloorTypeMap = null)
        {
            if (levels == null || levels.Count == 0 || floorTypes == null || floorTypes.Count == 0)
                return;

            // Get default floor type ID
            string defaultFloorTypeId = floorTypes.FirstOrDefault()?.Id;
            if (string.IsNullOrEmpty(defaultFloorTypeId))
                return;

            foreach (var level in levels)
            {
                // If mapping provided, use it
                if (levelToFloorTypeMap != null && levelToFloorTypeMap.TryGetValue(level.Id, out string floorTypeId))
                {
                    // Ensure floor type exists
                    if (floorTypes.Any(ft => ft.Id == floorTypeId))
                    {
                        level.FloorTypeId = floorTypeId;
                        Debug.WriteLine($"Associated level '{level.Name}' with floor type ID '{floorTypeId}'");
                    }
                    else
                    {
                        level.FloorTypeId = defaultFloorTypeId;
                        Debug.WriteLine($"Associated level '{level.Name}' with default floor type ID '{defaultFloorTypeId}'");
                    }
                }
                else
                {
                    // If no mapping or level not in mapping, use default
                    level.FloorTypeId = defaultFloorTypeId;
                }
            }
        }

        // Updates or creates floor types in the model based on UI input
  
        public List<FloorType> CreateFloorTypesFromNames(List<string> floorTypeNames)
        {
            var floorTypes = new List<FloorType>();

            if (floorTypeNames == null || floorTypeNames.Count == 0)
            {
                // Add at least a default floor type
                floorTypes.Add(new FloorType
                {
                    Id = IdGenerator.Generate(IdGenerator.Layout.FLOOR_TYPE),
                    Name = "Default",
                });
                return floorTypes;
            }

            // Create floor types from names
            foreach (var name in floorTypeNames)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var floorType = new FloorType
                {
                    Id = IdGenerator.Generate(IdGenerator.Layout.FLOOR_TYPE),
                    Name = name.Trim(),
                };

                floorTypes.Add(floorType);
            }

            // Ensure at least one floor type exists
            if (floorTypes.Count == 0)
            {
                floorTypes.Add(new FloorType
                {
                    Id = IdGenerator.Generate(IdGenerator.Layout.FLOOR_TYPE),
                    Name = "Default",
                });
            }

            return floorTypes;
        }
    }
}