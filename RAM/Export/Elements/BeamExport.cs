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
                // Get all stories from RAM
                IStories ramStories = _model.GetStories();
                if (ramStories == null || ramStories.GetCount() == 0)
                    return beams;

                // Process each story
                for (int i = 0; i < ramStories.GetCount(); i++)
                {
                    IStory ramStory = ramStories.GetAt(i);
                    if (ramStory == null)
                        continue;

                    // Find the corresponding level ID for this story
                    string levelId = Helpers.FindLevelIdForStory(ramStory, _levelMappings);
                    if (string.IsNullOrEmpty(levelId))
                        continue;

                    // Get beams for this story
                    IBeams storyBeams = ramStory.GetBeams();
                    if (storyBeams == null || storyBeams.GetCount() == 0)
                        continue;

                    // Process each beam in the story
                    for (int j = 0; j < storyBeams.GetCount(); j++)
                    {
                        IBeam ramBeam = storyBeams.GetAt(j);
                        if (ramBeam == null)
                            continue;

                        // Get beam coordinates
                        SCoordinate pt1 = new SCoordinate();
                        SCoordinate pt2 = new SCoordinate();
                        ramBeam.GetCoordinates(EBeamCoordLoc.eBeamEnds, ref pt1, ref pt2);

                        // Create beam from RAM data
                        Beam beam = new Beam
                        {
                            Id = IdGenerator.Generate(IdGenerator.Elements.BEAM),
                            StartPoint = new Point2D(
                                ConvertFromInches(pt1.dXLoc),
                                ConvertFromInches(pt1.dYLoc)
                            ),
                            EndPoint = new Point2D(
                                ConvertFromInches(pt2.dXLoc),
                                ConvertFromInches(pt2.dYLoc)
                            ),
                            LevelId = levelId,
                            FramePropertiesId = Helpers.FindFramePropertiesId(ramBeam.strSectionLabel, _framePropMappings),
                            IsLateral = (ramBeam.eFramingType == EFRAMETYPE.MemberIsLateral), // Assuming 1 means lateral
                            IsJoist = (ramBeam.eMaterial == EMATERIALTYPES.ESteelJoistMat)
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