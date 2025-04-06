// BeamImport.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.Elements
{
    /// <summary>
    /// Imports beam elements to RAM from the Core model
    /// </summary>
    public class BeamImport
    {
        private IModel _model;
        private string _lengthUnit;
        private Dictionary<string, IFloorType> _floorTypeByLevelId = new Dictionary<string, IFloorType>();

        /// <summary>
        /// Initializes a new instance of the BeamImport class
        /// </summary>
        /// <param name="model">The RAM model</param>
        /// <param name="lengthUnit">The length unit used in the model</param>
        public BeamImport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        /// <summary>
        /// Imports beams to RAM model for each level with specific floor types
        /// </summary>
        /// <param name="beams">The collection of beams to import</param>
        /// <param name="levels">The collection of levels in the model</param>
        /// <param name="frameProperties">The collection of frame properties in the model</param>
        /// <returns>The number of beams successfully imported</returns>
        public int Import(IEnumerable<Beam> beams, IEnumerable<Level> levels, IEnumerable<FrameProperties> frameProperties)
        {
            try
            {
                // First, ensure we have valid input
                if (beams == null || !beams.Any())
                {
                    Console.WriteLine("No beams to import.");
                    return 0;
                }

                if (levels == null || !levels.Any())
                {
                    Console.WriteLine("No levels available for beam import.");
                    return 0;
                }

                // Get all available floor types from RAM
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes.GetCount() == 0)
                {
                    Console.WriteLine("Error: No floor types found in RAM model");
                    return 0;
                }

                // Output debugging info about available floor types
                Console.WriteLine($"Available RAM floor types: {ramFloorTypes.GetCount()}");
                for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                {
                    IFloorType ft = ramFloorTypes.GetAt(i);
                    Console.WriteLine($"  Floor Type {i + 1}: {ft.strLabel} (ID: {ft.lUID})");
                }

                // Create a default floor type to use if specific mapping fails
                IFloorType defaultFloorType = ramFloorTypes.GetAt(0);
                Console.WriteLine($"Using default floor type: {defaultFloorType.strLabel} (ID: {defaultFloorType.lUID})");

                // Create mappings for levels and materials
                Dictionary<string, Level> levelMap = new Dictionary<string, Level>();
                Dictionary<string, EMATERIALTYPES> materialsMap = new Dictionary<string, EMATERIALTYPES>();

                // Build level mapping
                foreach (var level in levels)
                {
                    if (!string.IsNullOrEmpty(level.Id))
                    {
                        levelMap[level.Id] = level;
                        Console.WriteLine($"Level mapped: {level.Id} -> {level.Name}, FloorTypeId: {level.FloorTypeId}");
                    }
                }

                // Build materials mapping
                foreach (var frameProp in frameProperties)
                {
                    if (!string.IsNullOrEmpty(frameProp.Id))
                    {
                        EMATERIALTYPES materialType = EMATERIALTYPES.ESteelMat; // Default to steel

                        // Check if it's a joist
                        bool isJoist = frameProp.Name?.ToLower().Contains("joist") == true ||
                                       frameProp.Shape?.ToLower().Contains("joist") == true;

                        if (isJoist)
                        {
                            materialType = EMATERIALTYPES.ESteelJoistMat;
                        }
                        else if (frameProp.MaterialId != null)
                        {
                            // Try to determine material from material ID
                            string materialName = frameProp.MaterialId?.ToLower() ?? "";
                            if (materialName.Contains("concrete"))
                            {
                                materialType = EMATERIALTYPES.EConcreteMat;
                            }
                        }

                        materialsMap[frameProp.Id] = materialType;
                        Console.WriteLine($"Material mapped: {frameProp.Id} -> {materialType}");
                    }
                }

                // Setup floor type mapping for each level
                foreach (var level in levels)
                {
                    if (string.IsNullOrEmpty(level.FloorTypeId))
                    {
                        Console.WriteLine($"Level {level.Name} has no floor type ID, using default");
                        _floorTypeByLevelId[level.Id] = defaultFloorType;
                        continue;
                    }

                    // Try to find a floor type in RAM that matches the level's floor type
                    bool foundMatch = false;

                    // For now, simply use the first floor type
                    // In a more refined implementation, you would loop through all floor types
                    // and match by name or other criteria
                    if (ramFloorTypes.GetCount() > 0)
                    {
                        IFloorType ft = ramFloorTypes.GetAt(0);
                        _floorTypeByLevelId[level.Id] = ft;
                        foundMatch = true;
                        Console.WriteLine($"Mapped level {level.Name} to floor type {ft.strLabel}");
                    }

                    if (!foundMatch)
                    {
                        Console.WriteLine($"No matching floor type found for level {level.Name}, using default");
                        _floorTypeByLevelId[level.Id] = defaultFloorType;
                    }
                }

                // Now process and import the beams
                int count = 0;
                foreach (var beam in beams)
                {
                    if (beam.StartPoint == null || beam.EndPoint == null)
                    {
                        Console.WriteLine($"Skipping beam {beam.Id}: Missing start or end point");
                        continue;
                    }

                    if (string.IsNullOrEmpty(beam.LevelId))
                    {
                        Console.WriteLine($"Skipping beam {beam.Id}: Missing level ID");
                        continue;
                    }

                    // Find the floor type for this beam's level
                    IFloorType floorType = null;

                    if (_floorTypeByLevelId.TryGetValue(beam.LevelId, out floorType))
                    {
                        Console.WriteLine($"Found floor type for beam {beam.Id}: {floorType.strLabel}");
                    }
                    else if (levelMap.TryGetValue(beam.LevelId, out Level level))
                    {
                        // Try to get the floor type using the level
                        Console.WriteLine($"Trying to find floor type for beam {beam.Id} through level {level.Name}");

                        if (_floorTypeByLevelId.TryGetValue(level.Id, out floorType))
                        {
                            Console.WriteLine($"Found floor type via level: {floorType.strLabel}");
                        }
                        else
                        {
                            Console.WriteLine($"No floor type found for level {level.Name}, using default");
                            floorType = defaultFloorType;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No level found for beam {beam.Id}, using default floor type");
                        floorType = defaultFloorType;
                    }

                    // Ensure we have a floor type
                    if (floorType == null)
                    {
                        Console.WriteLine($"Critical error: No floor type could be determined for beam {beam.Id}");
                        floorType = defaultFloorType; // Final fallback
                    }

                    // Get beam material
                    EMATERIALTYPES beamMaterial = EMATERIALTYPES.ESteelMat; // Default
                    if (!string.IsNullOrEmpty(beam.FramePropertiesId) &&
                        materialsMap.TryGetValue(beam.FramePropertiesId, out EMATERIALTYPES material))
                    {
                        beamMaterial = material;
                    }

                    // Convert coordinates to inches
                    double beamX1 = Helpers.ConvertToInches(beam.StartPoint.X, _lengthUnit);
                    double beamY1 = Helpers.ConvertToInches(beam.StartPoint.Y, _lengthUnit);
                    double beamZ1 = 0; // Z coordinate is typically determined by the level
                    double beamX2 = Helpers.ConvertToInches(beam.EndPoint.X, _lengthUnit);
                    double beamY2 = Helpers.ConvertToInches(beam.EndPoint.Y, _lengthUnit);
                    double beamZ2 = 0;

                    Console.WriteLine($"Creating beam at ({beamX1},{beamY1}) to ({beamX2},{beamY2}) with material {beamMaterial}");

                    try
                    {
                        // Create the beam in RAM
                        ILayoutBeams layoutBeams = floorType.GetLayoutBeams();
                        if (layoutBeams == null)
                        {
                            Console.WriteLine($"Error: GetLayoutBeams() returned null for floor type {floorType.strLabel}");
                            continue;
                        }

                        ILayoutBeam ramBeam = layoutBeams.Add(beamMaterial, beamX1, beamY1, beamZ1, beamX2, beamY2, beamZ2);

                        if (ramBeam != null)
                        {
                            count++;
                            Console.WriteLine($"Successfully created beam {count}");
                        }
                        else
                        {
                            Console.WriteLine("Error: RAM returned null beam");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating beam: {ex.Message}");
                        // Continue with next beam instead of failing the whole import
                    }
                }

                Console.WriteLine($"Successfully imported {count} beams");
                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing beams: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}