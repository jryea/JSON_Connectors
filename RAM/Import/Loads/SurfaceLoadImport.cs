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

                    // Generate a name for the surface load
                    string surfaceLoadName = $"SurfLoad_{count + 1}";
                    if (!string.IsNullOrEmpty(surfaceLoad.Id))
                    {
                        // Try to extract a more meaningful name from the ID
                        string[] idParts = surfaceLoad.Id.Split('-');
                        if (idParts.Length > 1)
                        {
                            surfaceLoadName = $"SurfLoad_{idParts[idParts.Length - 1]}";
                        }
                    }

                    try
                    {
                        // Create the surface load property set in RAM
                        ISurfaceLoadPropertySet surfaceLoadProp = surfaceLoadProps.Add(surfaceLoadName);

                        // Set default values
                        double deadLoad = 0.0;
                        double liveLoad = 0.0;

                        // Try to get the dead load value if available
                        if (!string.IsNullOrEmpty(surfaceLoad.DeadLoadId) &&
                            loadDefsById.TryGetValue(surfaceLoad.DeadLoadId, out LoadDefinition deadLoadDef))
                        {
                            deadLoad = 20.0; // Default value in psf
                            // In a more complete implementation, this would extract exact load values
                            // from load definition properties
                        }

                        // Try to get the live load value if available
                        if (!string.IsNullOrEmpty(surfaceLoad.LiveLoadId) &&
                            loadDefsById.TryGetValue(surfaceLoad.LiveLoadId, out LoadDefinition liveLoadDef))
                        {
                            liveLoad = 40.0; // Default value in psf
                            // In a more complete implementation, this would extract exact load values
                            // from load definition properties
                        }

                        // Set the load values on the RAM surface load property set
                        // Note: These properties and methods would be based on the actual RAM API
                        // For now, we're just showing a typical approach without actual API calls
                        // surfaceLoadProp.SetDeadLoad(deadLoad);
                        // surfaceLoadProp.SetLiveLoad(liveLoad);

                        count++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating surface load property {surfaceLoadName}: {ex.Message}");
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