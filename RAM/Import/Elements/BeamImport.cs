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
    public class BeamImport
    {
        private IModel _model;
        private string _lengthUnit;

        public BeamImport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public int Import(IEnumerable<Beam> beams, IEnumerable<Level> levels, IEnumerable<FrameProperties> frameProperties)
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
                Dictionary<string, Level> masterLevelByFloorTypeId = new Dictionary<string, Level>();

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

                // Map materials
                var materialMap = MapMaterials(frameProperties);

                // Import beams
                int count = 0;
                foreach (var beam in beams)
                {
                    if (beam.StartPoint == null || beam.EndPoint == null ||
                        string.IsNullOrEmpty(beam.LevelId))
                        continue;

                    // Get the level this beam belongs to
                    Level beamLevel = levels.FirstOrDefault(l => l.Id == beam.LevelId);
                    if (beamLevel == null)
                        continue;

                    string floorTypeId = beamLevel.FloorTypeId;
                    if (string.IsNullOrEmpty(floorTypeId))
                        continue;

                    // Check if this is the master level for this floor type
                    if (masterLevelByFloorTypeId.TryGetValue(floorTypeId, out Level masterLevel) &&
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
                    EMATERIALTYPES beamMaterial = EMATERIALTYPES.ESteelMat;
                    if (!string.IsNullOrEmpty(beam.FramePropertiesId))
                        materialMap.TryGetValue(beam.FramePropertiesId, out beamMaterial);

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

        private Dictionary<string, EMATERIALTYPES> MapMaterials(IEnumerable<FrameProperties> frameProperties)
        {
            var map = new Dictionary<string, EMATERIALTYPES>();

            foreach (var prop in frameProperties ?? Enumerable.Empty<FrameProperties>())
            {
                if (string.IsNullOrEmpty(prop.Id))
                    continue;

                EMATERIALTYPES type = EMATERIALTYPES.ESteelMat;

                bool isJoist = prop.Name?.ToLower().Contains("joist") == true ||
                               prop.Shape?.ToLower().Contains("joist") == true;

                if (isJoist)
                {
                    type = EMATERIALTYPES.ESteelJoistMat;
                }
                else if (prop.MaterialId != null)
                {
                    string materialName = prop.MaterialId.ToLower();
                    if (materialName.Contains("concrete"))
                        type = EMATERIALTYPES.EConcreteMat;
                }

                map[prop.Id] = type;
            }

            return map;
        }
    }
}