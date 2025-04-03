// SlabPropertiesExporter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Properties;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Export
{
    public class RAMToSlabProperties : IRAMExporter
    {
        private IModel _model;

        public RAMToSlabProperties(IModel model)
        {
            _model = model;
        }

        public void Export(BaseModel model)
        {
            // Get concrete slab properties from RAM model
            IConcSlabProps slabProps = _model.GetConcreteSlabProps();

            // Filter for concrete slab properties
            var concreteFloorProps = model.Properties.FloorProperties
                .Where(fp => fp.Type?.ToLower() == "slab" || string.IsNullOrEmpty(fp.Type))
                .ToList();

            // Export each concrete slab property
            foreach (var floorProp in concreteFloorProps)
            {
                try
                {
                    // Default values
                    double selfWeight = 0.0; // RAM will calculate self-weight based on thickness and density

                    // Create the slab property in RAM
                    IConcSlabProp slabProp = slabProps.Add(floorProp.Name, floorProp.Thickness, selfWeight);

                    // Set additional slab properties if available
                    if (floorProp.SlabProperties != null)
                    {
                        // Set concrete strength if specified
                        if (floorProp.SlabProperties.TryGetValue("fc", out object fcValue) && fcValue is double fc)
                        {
                            slabProp.dFcPrime = fc;
                        }

                        // Set elastic modulus if specified
                        if (floorProp.SlabProperties.TryGetValue("elasticModulus", out object eValue) && eValue is double elasticModulus)
                        {
                            slabProp.dElasticMod = elasticModulus;
                        }

                        // Set Poisson's ratio if specified
                        if (floorProp.SlabProperties.TryGetValue("poissonsRatio", out object prValue) && prValue is double poissonsRatio)
                        {
                            slabProp.dPoissonsRatio = poissonsRatio;
                        }

                        // Set unit weight if specified
                        if (floorProp.SlabProperties.TryGetValue("unitWeight", out object uwValue) && uwValue is double unitWeight)
                        {
                            slabProp.dUnitWt = unitWeight;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting slab property {floorProp.Name}: {ex.Message}");
                }
            }
        }
    }
}