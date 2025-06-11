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

                    // Use the surface load name, or generate one if not provided
                    string surfaceLoadName = !string.IsNullOrEmpty(surfaceLoad.Name)
                        ? surfaceLoad.Name
                        : $"SurfLoad_{count + 1}";

                    // If no name and has ID, try to extract a meaningful name from the ID
                    if (string.IsNullOrEmpty(surfaceLoad.Name) && !string.IsNullOrEmpty(surfaceLoad.Id))
                    {
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

                        if (surfaceLoadProp != null)
                        {
                            // Set dead load value from the SurfaceLoad.DeadLoadValue property
                            double deadLoad = surfaceLoad.DeadLoadValue;

                            // Set live load value from the SurfaceLoad.LiveLoadValue property  
                            double liveLoad = surfaceLoad.LiveLoadValue;

                            // UNIT CONVERSION FIX:
                            // RAM's EUnits.eUnitsEnglish system applies automatic conversions that result in
                            // a 144,000x multiplier (144 for ft²→in² + 1000x unit scaling factor)
                            // We need to apply the inverse to get correct PSF values in RAM
                            const double RAM_ENGLISH_UNITS_CORRECTION = 1.0 / 144000.0;

                            double ramDeadLoad = deadLoad * RAM_ENGLISH_UNITS_CORRECTION;
                            double ramLiveLoad = liveLoad * RAM_ENGLISH_UNITS_CORRECTION;

                            // Apply the corrected load values to RAM
                            try
                            {
                                surfaceLoadProp.dDeadLoad = ramDeadLoad;
                                surfaceLoadProp.dLiveLoad = ramLiveLoad;

                                Console.WriteLine($"Created surface load property '{surfaceLoadName}':");
                                Console.WriteLine($"  Model Dead Load: {deadLoad} psf -> RAM Input: {ramDeadLoad:E3}");
                                Console.WriteLine($"  Model Live Load: {liveLoad} psf -> RAM Input: {ramLiveLoad:E3}");
                                Console.WriteLine($"  Applied correction factor {RAM_ENGLISH_UNITS_CORRECTION:E3} to counteract RAM's automatic 144,000x conversion");
                            }
                            catch (Exception propEx)
                            {
                                Console.WriteLine($"Error setting load values for '{surfaceLoadName}': {propEx.Message}");
                                // Continue with creation even if property setting fails
                            }

                            count++;
                        }
                        else
                        {
                            Console.WriteLine($"Failed to create surface load property set for '{surfaceLoadName}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating surface load property {surfaceLoadName}: {ex.Message}");
                        // Continue with the next surface load instead of failing the whole import
                    }
                }

                Console.WriteLine($"Successfully imported {count} surface load properties");
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