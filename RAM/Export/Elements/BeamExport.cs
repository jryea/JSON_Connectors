using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Models.ModelLayout;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Export.Elements
{
    public class BeamExport
    {
        private IModel _model;
        private string _lengthUnit;
        private Dictionary<string, string> _levelMappings = new Dictionary<string, string>();
        private Dictionary<string, string> _framePropMappings = new Dictionary<string, string>();

        public BeamExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public void SetLevelMappings(Dictionary<string, string> levelMappings)
        {
            _levelMappings = levelMappings ?? new Dictionary<string, string>();
        }

        public void SetFramePropertyMappings(Dictionary<string, string> framePropMappings)
        {
            _framePropMappings = framePropMappings ?? new Dictionary<string, string>();
        }

        public List<Beam> Export()
        {
            var beams = new List<Beam>();

            try
            {
                // Get all floor types from RAM
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes == null || ramFloorTypes.GetCount() == 0)
                    return beams;

                // Process each floor type
                for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                {
                    IFloorType floorType = ramFloorTypes.GetAt(i);
                    if (floorType == null)
                        continue;

                    // Find the corresponding level ID for this floor type
                    string levelId = FindLevelIdForFloorType(floorType);
                    if (string.IsNullOrEmpty(levelId))
                        continue;

                    // Get layout beams for this floor type
                    ILayoutBeams layoutBeams = floorType.GetLayoutBeams();
                    if (layoutBeams == null)
                        continue;

                    // Process each layout beam
                    for (int j = 0; j < layoutBeams.GetCount(); j++)
                    {
                        ILayoutBeam layoutBeam = layoutBeams.GetAt(j);
                        if (layoutBeam == null)
                            continue;

                        // Create beam from RAM data
                        Beam beam = new Beam
                        {
                            Id = IdGenerator.Generate(IdGenerator.Elements.BEAM),
                            StartPoint = new Point2D(
                                ConvertFromInches(layoutBeam.dXStart),
                                ConvertFromInches(layoutBeam.dYStart)
                            ),
                            EndPoint = new Point2D(
                                ConvertFromInches(layoutBeam.dXEnd),
                                ConvertFromInches(layoutBeam.dYEnd)
                            ),
                            LevelId = levelId,
                            FramePropertiesId = FindFramePropertiesId(layoutBeam.strSectionLabel),
                            IsLateral = GetIsLateral(layoutBeam),
                            IsJoist = IsJoistSection(layoutBeam.eMaterialType)
                        };

                        beams.Add(beam);
                    }
                }

                return beams;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting beams from RAM: {ex.Message}");
                return beams;
            }
        }

        private string FindLevelIdForFloorType(IFloorType floorType)
        {
            // Try to find direct mapping by floor type UID
            string key = $"FloorType_{floorType.lUID}";
            if (_levelMappings.TryGetValue(key, out string levelId))
                return levelId;

            // If not found, try by floor type name
            if (_levelMappings.TryGetValue(floorType.strLabel, out levelId))
                return levelId;

            // Return first level ID as fallback
            return _levelMappings.Values.FirstOrDefault();
        }

        private string FindFramePropertiesId(string sectionName)
        {
            if (string.IsNullOrEmpty(sectionName))
                return null;

            // Try to find direct mapping by section name
            if (_framePropMappings.TryGetValue(sectionName, out string framePropsId))
                return framePropsId;

            // Return null if not found
            return null;
        }

        private bool IsJoistSection(EMATERIALTYPES materialType)
        {
            return materialType == EMATERIALTYPES.ESteelJoistMat;
        }

        private double ConvertFromInches(double inches)
        {
            switch (_lengthUnit.ToLower())
            {
                case "feet":
                    return inches / 12.0;
                case "millimeters":
                    return inches * 25.4;
                case "centimeters":
                    return inches * 2.54;
                case "meters":
                    return inches * 0.0254;
                case "inches":
                default:
                    return inches;
            }
        }
    }
}