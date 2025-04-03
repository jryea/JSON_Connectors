// BeamExporter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using JSON_Connectors.Connectors.RAM.Export;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Export
{
    public class BeamExporter : IRAMExporter
    {
        private IModel _model;

        public BeamExporter(IModel model)
        {
            _model = model;
        }

        public void Export(BaseModel model)
        {
            // Group beams by level
            var beamsByLevel = model.Elements.Beams
                .Where(b => !string.IsNullOrEmpty(b.LevelId))
                .GroupBy(b => b.LevelId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Map levels to floor types
            var levelToFloorType = new Dictionary<string, string>();
            foreach (var level in model.ModelLayout.Levels)
            {
                if (!string.IsNullOrEmpty(level.FloorTypeId))
                {
                    var floorType = model.ModelLayout.FloorTypes
                        .FirstOrDefault(ft => ft.Id == level.FloorTypeId);

                    if (floorType != null)
                    {
                        levelToFloorType[level.Id] = floorType.Name;
                    }
                }
            }

            // Match floor types to RAM floor types
            IFloorTypes ramFloorTypes = _model.GetFloorTypes();
            var floorTypeMap = new Dictionary<string, IFloorType>();

            for (int i = 0; i < ramFloorTypes.GetCount(); i++)
            {
                IFloorType floorType = ramFloorTypes.GetAt(i);
                floorTypeMap[floorType.strLabel] = floorType;
            }

            // Export beams
            foreach (var levelId in beamsByLevel.Keys)
            {
                // Find corresponding floor type
                if (!levelToFloorType.TryGetValue(levelId, out string floorTypeName) ||
                    !floorTypeMap.TryGetValue(floorTypeName, out IFloorType floorType))
                {
                    Console.WriteLine($"Could not find floor type for level {levelId}");
                    continue;
                }

                // Get layout beams
                ILayoutBeams layoutBeams = floorType.GetLayoutBeams();

                // Export beams for this level
                foreach (var beam in beamsByLevel[levelId])
                {
                    try
                    {
                        // Determine material type
                        EMATERIALTYPES materialType = EMATERIALTYPES.ESteelMat;
                        if (!string.IsNullOrEmpty(beam.FramePropertiesId))
                        {
                            var frameProp = model.Properties.FrameProperties
                                .FirstOrDefault(fp => fp.Id == beam.FramePropertiesId);

                            if (frameProp != null && !string.IsNullOrEmpty(frameProp.MaterialId))
                            {
                                var material = model.Properties.Materials
                                    .FirstOrDefault(m => m.Id == frameProp.MaterialId);

                                if (material != null && material.Type.ToLower() == "concrete")
                                {
                                    materialType = EMATERIALTYPES.EConcreteMat;
                                }
                                else if (material != null && material.Type.ToLower().Contains("joist"))
                                {
                                    materialType = EMATERIALTYPES.ESteelJoistMat;
                                }
                            }
                        }

                        // Convert coordinates to inches
                        double x1 = beam.StartPoint.X * 12;
                        double y1 = beam.StartPoint.Y * 12;
                        double z1 = 0;
                        double x2 = beam.EndPoint.X * 12;
                        double y2 = beam.EndPoint.Y * 12;
                        double z2 = 0;

                        // Add beam
                        layoutBeams.Add(materialType, x1, y1, z1, x2, y2, z2);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error exporting beam {beam.Id}: {ex.Message}");
                    }
                }
            }
        }
    }
}