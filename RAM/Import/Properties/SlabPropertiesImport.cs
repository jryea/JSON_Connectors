// SlabPropertiesImport.cs
using System;
using System.Collections.Generic;
using Core.Models.Properties;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.Properties
{
    /// <summary>
    /// Imports concrete slab properties to RAM from the Core model
    /// </summary>
    public class SlabPropertiesImport
    {
        private IModel _model;
        private string _lengthUnit;

        // Initializes a new instance of the SlabPropertiesImport class
      
        public SlabPropertiesImport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        // Imports slab properties to RAM
     
        public Dictionary<string, int> Import(IEnumerable<FloorProperties> floorProperties)
        {
            var idMapping = new Dictionary<string, int>();

            try
            {
                // Get the concrete slab properties collection from RAM
                IConcSlabProps slabProps = _model.GetConcreteSlabProps();

                foreach (var floorProp in floorProperties)
                {
                    // Skip if not a slab type or already imported
                    if (floorProp.Type?.ToLower() != "slab" || idMapping.ContainsKey(floorProp.Id))
                        continue;

                    // Convert thickness to inches if needed
                    double thickness = Helpers.ConvertToInches(floorProp.Thickness, _lengthUnit);

                    // Calculate self-weight based on material and thickness
                    double selfWeight = 0.0;
                    if (floorProp.SlabProperties != null &&
                        floorProp.SlabProperties.ContainsKey("selfWeight") &&
                        floorProp.SlabProperties["selfWeight"] is double weight)
                    {
                        selfWeight = weight;
                    }
                    else
                    {
                        // Default self-weight calculation (assuming 150 pcf concrete)
                        selfWeight = thickness / 12.0 * 150.0;
                    }

                    // Create the slab property in RAM
                    IConcSlabProp slabProp = slabProps.Add(
                        floorProp.Name ?? $"Slab {thickness}\"",
                        thickness,
                        selfWeight);

                    // Store the mapping between Core model ID and RAM ID
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