using System;
using System.Collections.Generic;
using Core.Models.ModelLayout;
using Core.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Export.ModelLayout
{
    public class FloorTypeExport
    {
        private IModel _model;

        public FloorTypeExport(IModel model)
        {
            _model = model;
        }

        public List<FloorType> Export()
        {
            var floorTypes = new List<FloorType>();

            try
            {
                // Get floor types from RAM
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes == null || ramFloorTypes.GetCount() == 0)
                    return floorTypes;

                // Extract each floor type
                for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                {
                    IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                    if (ramFloorType != null)
                    {
                        FloorType floorType = new FloorType
                        {
                            Id = IdGenerator.Generate(IdGenerator.Layout.FLOOR_TYPE),
                            Name = ramFloorType.strLabel,
                            Description = $"Floor type from RAM (ID: {ramFloorType.lUID})"
                        };

                        floorTypes.Add(floorType);
                    }
                }

                return floorTypes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting floor types from RAM: {ex.Message}");
                return floorTypes;
            }
        }

        // Helper method to create a mapping from RAM floor type UIDs to their Core model IDs
        public Dictionary<int, string> CreateFloorTypeMapping(List<FloorType> floorTypes)
        {
            var mapping = new Dictionary<int, string>();

            try
            {
                // Get floor types from RAM
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes == null || ramFloorTypes.GetCount() == 0 || floorTypes == null || floorTypes.Count == 0)
                    return mapping;

                // Create a lookup by name for quick access
                Dictionary<string, string> floorTypeIdsByName = new Dictionary<string, string>();
                foreach (var floorType in floorTypes)
                {
                    if (!string.IsNullOrEmpty(floorType.Name))
                    {
                        floorTypeIdsByName[floorType.Name] = floorType.Id;
                    }
                }

                // Map RAM floor types to Core model IDs
                for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                {
                    IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                    if (ramFloorType != null && !string.IsNullOrEmpty(ramFloorType.strLabel))
                    {
                        if (floorTypeIdsByName.TryGetValue(ramFloorType.strLabel, out string floorTypeId))
                        {
                            mapping[ramFloorType.lUID] = floorTypeId;
                        }
                    }
                }

                return mapping;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating floor type mapping: {ex.Message}");
                return mapping;
            }
        }
    }
}