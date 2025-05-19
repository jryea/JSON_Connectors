using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.Elements
{
    public class BeamImport
    {
        private readonly IModel _model;
        private readonly string _lengthUnit;
        private readonly MaterialProvider _materialProvider;

        public BeamImport(
            IModel model,
            MaterialProvider materialProvider,
            string lengthUnit = "inches")
        {
            _model = model;
            _materialProvider = materialProvider;
            _lengthUnit = lengthUnit;
        }

        public int Import(IEnumerable<Beam> beams, IEnumerable<Level> levels,
             IEnumerable<FrameProperties> frameProperties,
             Dictionary<string, string> levelToFloorTypeMapping)
        {
            try
            {
                if (beams == null || !beams.Any() || levels == null || !levels.Any())
                    return 0;

                // Get RAM floor types
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes.GetCount() == 0)
                    return 0;

                Console.WriteLine("Beginning beam import with corrected floor type mapping...");

                // First, create a mapping from level ID to its floor type ID
                var levelIdToFloorTypeId = new Dictionary<string, string>();
                foreach (var level in levels)
                {
                    if (!string.IsNullOrEmpty(level.Id) && !string.IsNullOrEmpty(level.FloorTypeId))
                    {
                        levelIdToFloorTypeId[level.Id] = level.FloorTypeId;
                    }
                }

                // Now create a direct mapping from floor type ID to RAM floor type
                var floorTypeIdToRamFloorType = new Dictionary<string, IFloorType>();

                // Build this map systematically, ensuring correct order
                var sortedFloorTypes = new List<string>(levelToFloorTypeMapping.Values.Distinct());
                Console.WriteLine($"Found {sortedFloorTypes.Count} distinct floor types in mapping");

                // Make sure we have floor types to map from
                if (sortedFloorTypes.Count > 0)
                {
                    // Print out the available RAM floor types
                    Console.WriteLine($"Available RAM floor types ({ramFloorTypes.GetCount()}):");
                    for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                    {
                        IFloorType floorType = ramFloorTypes.GetAt(i);
                        Console.WriteLine($"  {i}: {floorType.strLabel} (UID: {floorType.lUID})");
                    }

                    // Map in order - the key is to match them in the correct order
                    for (int i = 0; i < Math.Min(sortedFloorTypes.Count, ramFloorTypes.GetCount()); i++)
                    {
                        string floorTypeId = sortedFloorTypes[i];
                        IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                        floorTypeIdToRamFloorType[floorTypeId] = ramFloorType;
                        Console.WriteLine($"Mapped floor type ID {floorTypeId} to RAM floor type {ramFloorType.strLabel} (UID: {ramFloorType.lUID})");
                    }
                }
                else
                {
                    Console.WriteLine("ERROR: No floor types found in level-to-floor-type mapping");
                    return 0;
                }

                // Track processed beams per floor type to avoid duplicates
                var processedBeamsByFloorType = new Dictionary<int, HashSet<string>>();

                // Import beams
                int count = 0;
                foreach (var beam in beams)
                {
                    if (beam.StartPoint == null || beam.EndPoint == null || string.IsNullOrEmpty(beam.LevelId))
                        continue;

                    // Get the floor type ID for this beam's level
                    if (!levelIdToFloorTypeId.TryGetValue(beam.LevelId, out string floorTypeId))
                    {
                        Console.WriteLine($"No floor type mapping found for beam level {beam.LevelId}, skipping");
                        continue;
                    }

                    // Get the RAM floor type for this floor type ID
                    if (!floorTypeIdToRamFloorType.TryGetValue(floorTypeId, out IFloorType ramFloorType))
                    {
                        Console.WriteLine($"No RAM floor type found for floor type ID {floorTypeId}, skipping");
                        continue;
                    }

                    Console.WriteLine($"Processing beam on level ID {beam.LevelId}, floor type ID {floorTypeId}, RAM floor type {ramFloorType.strLabel}");

                    // Convert coordinates
                    double x1 = Math.Round(UnitConversionUtils.ConvertToInches(beam.StartPoint.X, _lengthUnit), 6);
                    double y1 = Math.Round(UnitConversionUtils.ConvertToInches(beam.StartPoint.Y, _lengthUnit), 6);
                    double x2 = Math.Round(UnitConversionUtils.ConvertToInches(beam.EndPoint.X, _lengthUnit), 6);
                    double y2 = Math.Round(UnitConversionUtils.ConvertToInches(beam.EndPoint.Y, _lengthUnit), 6);

                    // Create a geometric key for this beam
                    string beamKey = CreateBeamGeometricKey(x1, y1, x2, y2);

                    // Check if this beam already exists in this floor type
                    int floorTypeUid = ramFloorType.lUID;
                    if (!processedBeamsByFloorType.TryGetValue(floorTypeUid, out var processedBeams))
                    {
                        processedBeams = new HashSet<string>();
                        processedBeamsByFloorType[floorTypeUid] = processedBeams;
                    }

                    if (processedBeams.Contains(beamKey))
                    {
                        Console.WriteLine($"Skipping duplicate beam on floor type {ramFloorType.strLabel}");
                        continue;
                    }

                    // Add the beam to the processed set
                    processedBeams.Add(beamKey);

                    // Get material type using MaterialProvider
                    EMATERIALTYPES beamMaterial = _materialProvider.GetRAMMaterialType(
                        beam.FramePropertiesId,
                        frameProperties,
                        beam.IsJoist);

                    try
                    {
                        // Get layout beams for this floor type
                        ILayoutBeams layoutBeams = ramFloorType.GetLayoutBeams();
                        if (layoutBeams != null)
                        {
                            // Add the beam to the layout
                            ILayoutBeam ramBeam = layoutBeams.Add(beamMaterial, x1, y1, 0, x2, y2, 0);
                            if (ramBeam != null)
                            {
                                // Set the beam properties
                                if (beam.IsLateral)
                                {
                                    ramBeam.eFramingType = EFRAMETYPE.MemberIsLateral;
                                }

                                // Set section label if available via frame properties
                                if (!string.IsNullOrEmpty(beam.FramePropertiesId))
                                {
                                    var frameProp = frameProperties?.FirstOrDefault(fp => fp.Id == beam.FramePropertiesId);
                                    if (frameProp != null && !string.IsNullOrEmpty(frameProp.Name))
                                    {
                                        ramBeam.strSectionLabel = frameProp.Name;
                                    }
                                    else
                                    {
                                        ramBeam.strSectionLabel = "W10X12"; // Default if not found
                                    }
                                }

                                count++;
                                Console.WriteLine($"Added beam to floor type {ramFloorType.strLabel} for level ID {beam.LevelId}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating beam: {ex.Message}");
                    }
                }

                Console.WriteLine($"Imported {count} beams");
                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing beams: {ex.Message}");
                throw;
            }
        }

        // Helper method to create a normalized geometric key for a beam
        private string CreateBeamGeometricKey(double x1, double y1, double x2, double y2)
        {
            // Normalize beam direction (smaller X or Y coordinates first)
            if ((Math.Abs(x2 - x1) > Math.Abs(y2 - y1) && x2 < x1) ||
                (Math.Abs(y2 - y1) >= Math.Abs(x2 - x1) && y2 < y1))
            {
                // Swap points to ensure consistent direction
                double tempX = x1;
                double tempY = y1;
                x1 = x2;
                y1 = y2;
                x2 = tempX;
                y2 = tempY;
            }

            // Return the formatted key
            return $"{x1},{y1}_{x2},{y2}";
        }
    }
}