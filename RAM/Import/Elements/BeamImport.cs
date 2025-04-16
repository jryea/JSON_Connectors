// BeamImport.cs
using System;
using System.Collections.Generic;
using System.Linq;
using CE = Core.Models.Elements;
using CL = Core.Models.ModelLayout;
using CP = Core.Models.Properties;
using RAM.Utilities;
using RAMDATAACCESSLib;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;

namespace RAM.Import.Elements
{
    public class BeamImport
    {
        private IModel _model;
        private string _lengthUnit;

        public BeamImport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public int Import(IEnumerable<Beam> beams, IEnumerable<Level> levels,
                 IEnumerable<FrameProperties> frameProperties,
                 IEnumerable<Material> materials)
        {
            try
            {
                if (beams == null || !beams.Any() || levels == null || !levels.Any())
                    return 0;

                // Get RAM floor types
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes.GetCount() == 0)
                    return 0;

                // Sort levels by elevation
                var sortedLevels = levels.OrderBy(l => l.Elevation).ToList();

                // Map floor types to their "master" level (first level with that floor type)
                Dictionary<string, CL.Level> masterLevelByFloorTypeId = new Dictionary<string, CL.Level>();

                // Find the first (lowest) level for each floor type - this will be the "master" level
                foreach (var level in sortedLevels)
                {
                    if (!string.IsNullOrEmpty(level.FloorTypeId) && !masterLevelByFloorTypeId.ContainsKey(level.FloorTypeId))
                    {
                        masterLevelByFloorTypeId[level.FloorTypeId] = level;
                        Console.WriteLine($"Floor type {level.FloorTypeId} has master level {level.Name} at elevation {level.Elevation}");
                    }
                }

                // Map Core floor types to RAM floor types
                Dictionary<string, IFloorType> ramFloorTypeByFloorTypeId = new Dictionary<string, IFloorType>();

                // Assign RAM floor types to Core floor types
                for (int i = 0; i < ramFloorTypes.GetCount() && i < masterLevelByFloorTypeId.Count; i++)
                {
                    IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                    string coreFloorTypeId = masterLevelByFloorTypeId.Keys.ElementAt(i);
                    ramFloorTypeByFloorTypeId[coreFloorTypeId] = ramFloorType;
                    Console.WriteLine($"Mapped Core floor type {coreFloorTypeId} to RAM floor type {ramFloorType.strLabel}");
                }

                // Import beams
                int count = 0;
                foreach (CE.Beam beam in beams)
                {
                    if (beam.StartPoint == null || beam.EndPoint == null ||
                        string.IsNullOrEmpty(beam.LevelId))
                        continue;

                    // Get the level this beam belongs to
                    CL.Level beamLevel = levels.FirstOrDefault(l => l.Id == beam.LevelId);
                    if (beamLevel == null)
                        continue;

                    // Get the floor Type ID for this level
                    string floorTypeId = beamLevel.FloorTypeId;
                    if (string.IsNullOrEmpty(floorTypeId))
                        continue;

                    // Check if this is the master level for this floor type
                    if (masterLevelByFloorTypeId.TryGetValue(floorTypeId, out CL.Level masterLevel) &&
                        masterLevel.Id != beamLevel.Id)
                    {
                        // Skip this beam as it's not on the master level for its floor type
                        Console.WriteLine($"Skipping beam on level {beamLevel.Name} - not the master level for floor type {floorTypeId}");
                        continue;
                    }

                    // Get RAM floor type for this floor type
                    if (!ramFloorTypeByFloorTypeId.TryGetValue(floorTypeId, out IFloorType ramFloorType))
                    {
                        // No matching RAM floor type
                        Console.WriteLine($"No RAM floor type found for floor type {floorTypeId}");
                        continue;
                    }

                    // Get material type
                    EMATERIALTYPES beamMaterial = Helpers.GetRAMMaterialType(
                        beam.FramePropertiesId,
                        frameProperties,
                        materials,
                        beam.IsJoist);

                    // Convert coordinates
                    double x1 = Helpers.ConvertToInches(beam.StartPoint.X, _lengthUnit);
                    double y1 = Helpers.ConvertToInches(beam.StartPoint.Y, _lengthUnit);
                    double x2 = Helpers.ConvertToInches(beam.EndPoint.X, _lengthUnit);
                    double y2 = Helpers.ConvertToInches(beam.EndPoint.Y, _lengthUnit);

                    try
                    {
                        ILayoutBeams layoutBeams = ramFloorType.GetLayoutBeams();
                        if (layoutBeams != null)
                        {
                            ILayoutBeam ramBeam = layoutBeams.Add(beamMaterial, x1, y1, 0, x2, y2, 0);
                            if (ramBeam != null)
                            {
                                count++;
                                Console.WriteLine($"Added beam from master level {masterLevel.Name} to floor type {ramFloorType.strLabel}");
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
    }
}