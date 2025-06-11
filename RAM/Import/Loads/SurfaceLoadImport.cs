// SurfaceLoadPropertiesImport.cs
using System;
using System.Collections.Generic;
using Core.Models.Loads;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.Loads
{
    // Imports surface load properties to RAM from the Core model
    public class SurfaceLoadPropertiesImport
    {
        private IModel _model;

        // Initializes a new instance of the SurfaceLoadPropertiesImport class
        public SurfaceLoadPropertiesImport(IModel model)
        {
            _model = model;
        }

        // Imports surface load properties to RAM
        public int Import(IEnumerable<SurfaceLoad> surfaceLoads, IEnumerable<LoadDefinition> loadDefinitions)
        {
            try
            {
                int count = 0;
                ISurfaceLoadPropertySets surfaceLoadProps = _model.GetSurfaceLoadPropertySets();

                // Create a dictionary to look up load definitions by ID
                Dictionary<string, LoadDefinition> loadDefsById = new Dictionary<string, LoadDefinition>();
                foreach (var loadDef in loadDefinitions)
                {
                    if (!string.IsNullOrEmpty(loadDef.Id))
                    {
                        loadDefsById[loadDef.Id] = loadDef;
                    }
                }

                // Process all surface loads
                foreach (var surfaceLoad in surfaceLoads)
                {
                    if (surfaceLoad == null)
                        continue;

                    try
                    {
                        // Set Surface Load name
                        string surfaceLoadName = surfaceLoad.Name;

                        // Create the surface load property set in RAM
                        ISurfaceLoadPropertySet surfaceLoadProp = surfaceLoadProps.Add(surfaceLoadName);

                        // Set default values
                        double deadLoad = surfaceLoad.DeadLoadValue;
                        double liveLoad = surfaceLoad.LiveLoadValue;

                        surfaceLoadProp.dLiveLoad = liveLoad;
                        surfaceLoadProp.dDeadLoad = deadLoad;   

                        count++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating surface load property {surfaceLoad.Name}: {ex.Message}");
                        // Continue with the next surface load instead of failing the whole import
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing surface load properties: {ex.Message}");
                throw;
            }
        }
    }
}