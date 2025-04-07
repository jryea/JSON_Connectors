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

                // Map Core floor types to RAM floor types
                Dictionary<string, IFloorType> ramFloorTypeByFloorTypeId = new Dictionary<string, IFloorType>();
                Dictionary<string, string> levelToFloorTypeId = new Dictionary<string, string>();

                // Map levels to floor types
                foreach (var level in levels)
                {
                    if (!string.IsNullOrEmpty(level.Id) && !string.IsNullOrEmpty(level.FloorTypeId))
                    {
                        levelToFloorTypeId[level.Id] = level.FloorTypeId;
                    }
                }

                // Map floor types to RAM floor types
                for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                {
                    IFloorType ramFloorType = ramFloorTypes.GetAt(i);
                    string ramFloorTypeName = ramFloorType.strLabel;

                    // Find all Core floor types in the model
                    var coreFloorTypes = levels
                        .Select(l => l.FloorTypeId)
                        .Where(id => !string.IsNullOrEmpty(id))
                        .Distinct()
                        .ToList();

                    // Map Core floor types to RAM floor types
                    for (int j = 0; j < coreFloorTypes.Count && j < ramFloorTypes.GetCount(); j++)
                    {
                        string coreFloorTypeId = coreFloorTypes[j];
                        ramFloorTypeByFloorTypeId[coreFloorTypeId] = ramFloorTypes.GetAt(j);
                        Console.WriteLine($"Mapped Core floor type {coreFloorTypeId} to RAM floor type {ramFloorTypes.GetAt(j).strLabel}");
                    }
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

                    // Get floor type ID for this beam's level
                    if (!levelToFloorTypeId.TryGetValue(beam.LevelId, out string floorTypeId) ||
                        string.IsNullOrEmpty(floorTypeId))
                        continue;

                    // Get RAM floor type for this floor type
                    if (!ramFloorTypeByFloorTypeId.TryGetValue(floorTypeId, out IFloorType ramFloorType))
                        continue;

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