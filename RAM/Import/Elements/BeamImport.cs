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

                Console.WriteLine("Beginning beam import with top-down floor type mapping...");

                // Group levels by floor type ID
                var levelsByFloorType = new Dictionary<string, List<Level>>();
                foreach (var level in levels)
                {
                    if (string.IsNullOrEmpty(level.Id) || string.IsNullOrEmpty(level.FloorTypeId))
                        continue;

                    if (!levelsByFloorType.ContainsKey(level.FloorTypeId))
                    {
                        levelsByFloorType[level.FloorTypeId] = new List<Level>();
                    }

                    levelsByFloorType[level.FloorTypeId].Add(level);
                }

                // For each floor type, identify the highest level
                var highestLevelByFloorType = new Dictionary<string, Level>();
                foreach (var entry in levelsByFloorType)
                {
                    var floorTypeId = entry.Key;
                    var levelsWithThisFloorType = entry.Value;

                    if (levelsWithThisFloorType.Count > 0)
                    {
                        // Find the highest level (highest elevation)
                        var highestLevel = levelsWithThisFloorType.OrderByDescending(l => l.Elevation).First();
                        highestLevelByFloorType[floorTypeId] = highestLevel;

                        Console.WriteLine($"FloorType {floorTypeId} uses highest level: {highestLevel.Name} (Elevation: {highestLevel.Elevation})");
                    }
                }

                // Create mapping from RAM floor type UID to RAM floor type
                var ramFloorTypeByUID = new Dictionary<int, IFloorType>();
                for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                {
                    IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                    ramFloorTypeByUID[ramFloorType.lUID] = ramFloorType;
                }

                // Map Core floor type IDs to RAM floor types
                var coreFloorTypeToRamFloorType = new Dictionary<string, IFloorType>();
                int ftIndex = 0;
                foreach (var floorTypeId in highestLevelByFloorType.Keys)
                {
                    if (ftIndex < ramFloorTypes.GetCount())
                    {
                        IFloorType ramFloorType = ramFloorTypes.GetAt(ftIndex);
                        coreFloorTypeToRamFloorType[floorTypeId] = ramFloorType;
                        Console.WriteLine($"Mapped Core floor type {floorTypeId} to RAM floor type {ramFloorType.strLabel} (UID: {ramFloorType.lUID})");
                        ftIndex++;
                    }
                }

                // Create a mapping from level ID to RAM floor type (only for highest levels of each floor type)
                var levelIdToRamFloorType = new Dictionary<string, IFloorType>();
                foreach (var entry in highestLevelByFloorType)
                {
                    string floorTypeId = entry.Key;
                    Level highestLevel = entry.Value;

                    if (coreFloorTypeToRamFloorType.TryGetValue(floorTypeId, out IFloorType ramFloorType))
                    {
                        levelIdToRamFloorType[highestLevel.Id] = ramFloorType;
                        Console.WriteLine($"Level {highestLevel.Name} (ID: {highestLevel.Id}) will use RAM floor type {ramFloorType.strLabel}");
                    }
                }

                // Track processed beams per floor type to avoid duplicates
                var processedBeamsByFloorType = new Dictionary<int, HashSet<string>>();

                // Import beams
                int count = 0;
                foreach (var beam in beams)
                {
                    if (beam.StartPoint == null || beam.EndPoint == null || string.IsNullOrEmpty(beam.LevelId))
                        continue;

                    // Only process beams on levels that are the highest for their floor type
                    if (!levelIdToRamFloorType.TryGetValue(beam.LevelId, out IFloorType ramFloorType))
                    {
                        // Skip beams not on highest level for their floor type
                        continue;
                    }

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
                                count++;
                                Console.WriteLine($"Added beam to floor type {ramFloorType.strLabel} for level {beam.LevelId}");
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