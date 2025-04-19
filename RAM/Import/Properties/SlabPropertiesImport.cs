// SlabPropertiesImport.cs
using System;
using System.Collections.Generic;
using Core.Models.Properties;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.Properties
{
    public class SlabPropertiesImport
    {
        private IModel _model;
        private string _lengthUnit;

        public SlabPropertiesImport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public Dictionary<string, int> Import(IEnumerable<FloorProperties> floorProperties,
                                               Dictionary<string, string> levelToFloorTypeMapping)
        {
            var idMapping = new Dictionary<string, int>();

            try
            {
                IConcSlabProps slabProps = _model.GetConcreteSlabProps();

                foreach (var floorProp in floorProperties)
                {
                    if (floorProp.Type?.ToLower() != "slab" || idMapping.ContainsKey(floorProp.Id))
                        continue;

                    // Ensure the FloorType is associated with a valid Level
                    if (!levelToFloorTypeMapping.ContainsValue(floorProp.Id))
                        continue;

                    double thickness = UnitConversionUtils.ConvertToInches(floorProp.Thickness, _lengthUnit);

                    double selfWeight = floorProp.SlabProperties != null &&
                                        floorProp.SlabProperties.TryGetValue("selfWeight", out var weight) &&
                                        weight is double w
                        ? w
                        : thickness / 12.0 * 150.0;

                    IConcSlabProp slabProp = slabProps.Add(
                        floorProp.Name ?? $"Slab {thickness}\"",
                        thickness,
                        selfWeight);

                    if (slabProp != null)
                    {
                        idMapping[floorProp.Id] = slabProp.lUID;
                        Console.WriteLine($"Created slab property: {floorProp.Name}, ID: {slabProp.lUID}");
                    }
                }

                return idMapping;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing slab properties: {ex.Message}");
                return idMapping;
            }
        }
    }
}
