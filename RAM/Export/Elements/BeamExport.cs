using System;
using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Export.Elements
{
    public class BeamExport
    {
        private IModel _model;
        private string _lengthUnit;

        public BeamExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
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

                    // Find the corresponding level ID for this story using the mapping utility
                    string storyUid = ramStory.lUID.ToString();
                    string levelId = ModelMappingUtility.GetLevelIdForStoryUid(storyUid);

                    if (string.IsNullOrEmpty(levelId))
                    {
                        Console.WriteLine($"No level mapping found for story {ramStory.strLabel} (UID: {storyUid})");
                        continue;
                    }

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

                      
                        // Use the mapping utility to find the frame property ID
                        string framePropertiesId = ModelMappingUtility.GetFramePropertyIdForSectionLabel(ramBeam.strSectionLabel);
                        

                        // Create beam from RAM data
                        Beam beam = new Beam
                        {
                            Id = IdGenerator.Generate(IdGenerator.Elements.BEAM),
                            StartPoint = new Point2D(
                                UnitConversionUtils.ConvertFromInches(pt1.dXLoc, "inches"),
                                UnitConversionUtils.ConvertFromInches(pt1.dYLoc, "inches")
                            ),
                            EndPoint = new Point2D(
                                UnitConversionUtils.ConvertFromInches(pt2.dXLoc, "inches"),
                                UnitConversionUtils.ConvertFromInches(pt2.dYLoc, "inches")
                            ),
                            LevelId = levelId,
                            FramePropertiesId = framePropertiesId,
                            IsLateral = (ramBeam.eFramingType == EFRAMETYPE.MemberIsLateral),
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
    }
}