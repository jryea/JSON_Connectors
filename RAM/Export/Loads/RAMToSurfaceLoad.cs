// SurfaceLoadPropertiesExporter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Loads;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Export
{
    public class SurfaceLoadPropertiesExporter : IRAMExporter
    {
        private IModel _model;

        public SurfaceLoadPropertiesExporter(IModel model)
        {
            _model = model;
        }

        public void Export(BaseModel model)
        {
            // Get surface load property sets from RAM model
            ISurfaceLoadPropertySets surfaceLoadProps = _model.GetSurfaceLoadPropertySets();

            // Group surface loads by name to avoid duplicates
            var surfaceLoadsByName = new Dictionary<string, SurfaceLoad>();

            foreach (var surfaceLoad in model.Loads.SurfaceLoads)
            {
                // Generate a name for the surface load if not available
                string loadName = $"SurfLoad_{surfaceLoad.Id.Split('-').Last()}";

                // Add to dictionary if not already present
                if (!surfaceLoadsByName.ContainsKey(loadName))
                {
                    surfaceLoadsByName[loadName] = surfaceLoad;
                }
            }

            // Retrieve load definitions
            var loadDefMap = model.Loads.LoadDefinitions.ToDictionary(ld => ld.Id, ld => ld);

            // Export each surface load property
            foreach (var entry in surfaceLoadsByName)
            {
                string loadName = entry.Key;
                SurfaceLoad surfaceLoad = entry.Value;

                try
                {
                    // Create the surface load property in RAM
                    ISurfaceLoadPropertySet surfaceLoadProp = surfaceLoadProps.Add(loadName);

                    // Set load values
                    double constDeadLoad = 0.0;
                    double constLiveLoad = 0.0;
                    double deadLoad = 0.0;
                    double liveLoad = 0.0;
                    double massDeadLoad = 0.0;
                    double partitionLoad = 0.0;
                    ELoadCaseType liveLoadType = ELoadCaseType.LiveReducibleLCa;

                    // Find dead load value
                    if (!string.IsNullOrEmpty(surfaceLoad.DeadLoadId) && loadDefMap.ContainsKey(surfaceLoad.DeadLoadId))
                    {
                        var deadLoadDef = loadDefMap[surfaceLoad.DeadLoadId];
                        deadLoad = GetLoadValue(deadLoadDef, "dead");
                    }

                    // Find live load value
                    if (!string.IsNullOrEmpty(surfaceLoad.LiveLoadId) && loadDefMap.ContainsKey(surfaceLoad.LiveLoadId))
                    {
                        var liveLoadDef = loadDefMap[surfaceLoad.LiveLoadId];
                        liveLoad = GetLoadValue(liveLoadDef, "live");

                        // Determine if live load is reducible or non-reducible
                        string loadType = liveLoadDef.Type?.ToLower() ?? "";
                        if (loadType.Contains("non-reducible") || loadType.Contains("nonreducible"))
                        {
                            liveLoadType = ELoadCaseType.LiveNonReducibleLCa;
                        }
                    }

                    // Set the load values on the surface load property
                    surfaceLoadProp.SetUniformLoads(constDeadLoad, constLiveLoad, deadLoad, liveLoad, massDeadLoad, partitionLoad, liveLoadType);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting surface load property {loadName}: {ex.Message}");
                }
            }
        }

        // Helper method to extract load value from load definition
        private double GetLoadValue(LoadDefinition loadDef, string loadType)
        {
            // Default values
            double defaultValue = loadType == "dead" ? 20.0 : 40.0; // psf

            // Try to extract value from load definition properties
            // This is a simplified approach - in a real implementation, you'd need to 
            // define a more detailed structure for load values in the LoadDefinition class

            return defaultValue / 144.0; // Convert psf to psi
        }
    }
}