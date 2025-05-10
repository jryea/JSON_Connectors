// BeamImport.cs
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

                // Map Core floor types to RAM floor types
                Dictionary<string, IFloorType> ramFloorTypeByFloorTypeId = new Dictionary<string, IFloorType>();

                // Assign RAM floor types to Core floor types
                for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                {
                    IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                    string coreFloorTypeId = levelToFloorTypeMapping.Values.ElementAtOrDefault(i);
                    if (!string.IsNullOrEmpty(coreFloorTypeId))
                    {
                        ramFloorTypeByFloorTypeId[coreFloorTypeId] = ramFloorType;
                        Console.WriteLine($"Mapped Core floor type {coreFloorTypeId} to RAM floor type {ramFloorType.strLabel}");
                    }
                }

                // Track processed beams per floor type to avoid duplicates
                Dictionary<string, HashSet<string>> processedBeamsByFloorType = new Dictionary<string, HashSet<string>>();

                // Import beams
                int count = 0;
                foreach (Beam beam in beams)
                {
                    if (beam.StartPoint == null || beam.EndPoint == null ||
                        string.IsNullOrEmpty(beam.LevelId))
                        continue;

                    // Get the floor type ID for the beam's level
                    if (!levelToFloorTypeMapping.TryGetValue(beam.LevelId, out string floorTypeId) ||
                        string.IsNullOrEmpty(floorTypeId))
                    {
                        Console.WriteLine($"No floor type mapping found for level {beam.LevelId}");
                        continue;
                    }

                    // Convert coordinates with rounding to ensure consistent comparison
                    double x1 = Math.Round(UnitConversionUtils.ConvertToInches(beam.StartPoint.X, _lengthUnit), 6);
                    double y1 = Math.Round(UnitConversionUtils.ConvertToInches(beam.StartPoint.Y, _lengthUnit), 6);
                    double x2 = Math.Round(UnitConversionUtils.ConvertToInches(beam.EndPoint.X, _lengthUnit), 6);
                    double y2 = Math.Round(UnitConversionUtils.ConvertToInches(beam.EndPoint.Y, _lengthUnit), 6);

                    // Create a geometric key for this beam (normalize direction)
                    string beamKey = CreateBeamGeometricKey(x1, y1, x2, y2);

                    // Check if this beam already exists in this floor type
                    if (!processedBeamsByFloorType.TryGetValue(floorTypeId, out var processedBeams))
                    {
                        processedBeams = new HashSet<string>();
                        processedBeamsByFloorType[floorTypeId] = processedBeams;
                    }

                    if (processedBeams.Contains(beamKey))
                    {
                        // Skip this beam as it's a duplicate
                        Console.WriteLine($"Skipping duplicate beam on floor type {floorTypeId}");
                        continue;
                    }

                    // Add the beam to the processed set
                    processedBeams.Add(beamKey);

                    // Get RAM floor type for this floor type
                    if (!ramFloorTypeByFloorTypeId.TryGetValue(floorTypeId, out IFloorType ramFloorType))
                    {
                        // No matching RAM floor type
                        Console.WriteLine($"No RAM floor type found for floor type {floorTypeId}");
                        continue;
                    }

                    // Get material type using MaterialProvider
                    EMATERIALTYPES beamMaterial = _materialProvider.GetRAMMaterialType(
                        beam.FramePropertiesId,
                        frameProperties,
                        beam.IsJoist);

                    try
                    {
                        ILayoutBeams layoutBeams = ramFloorType.GetLayoutBeams();
                        if (layoutBeams != null)
                        {
                            ILayoutBeam ramBeam = layoutBeams.Add(beamMaterial, x1, y1, 0, x2, y2, 0);
                            if (ramBeam != null)
                            {
                                count++;
                                Console.WriteLine($"Added beam to floor type {ramFloorType.strLabel}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating beam: {ex.Message}");
                    }
                }

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