using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Export.Elements
{
    /// <summary>
    /// Exports braces from RAM model to Core model
    /// </summary>
    public class BraceExport
    {
        private IModel _model;
        private string _lengthUnit;

        public BraceExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public List<Brace> Export()
        {
            var braces = new List<Brace>();

            try
            {
                // Get vertical braces from RAM model
                IVerticalBraces verticalBraces = _model.GetVerticalBraces();
                if (verticalBraces == null || verticalBraces.GetCount() == 0)
                {
                    Console.WriteLine("No vertical braces found in RAM model");
                    return braces;
                }

                // Process each vertical brace
                for (int i = 0; i < verticalBraces.GetCount(); i++)
                {
                    IVerticalBrace ramBrace = verticalBraces.GetAt(i);
                    if (ramBrace == null)
                        continue;

                    // Get top and base stories for this brace
                    string topStoryUid = ramBrace.lStoryAtTopID.ToString();
                    string baseStoryUid = ramBrace.lStoryAtBotID.ToString();

                    // Get level IDs from the mapping utility
                    string topLevelId = ModelMappingUtility.GetLevelIdForStoryUid(topStoryUid);
                    string baseLevelId;

                    // Handle special case: -1 story ID means foundation/ground level
                    if (baseStoryUid == "-1")
                    {
                        baseLevelId = ModelMappingUtility.GetGroundLevelId();
                        Console.WriteLine($"Brace {i + 1} extends to ground level (story ID -1)");
                    }
                    else
                    {
                        baseLevelId = ModelMappingUtility.GetLevelIdForStoryUid(baseStoryUid);
                    }

                    // Check if we have valid level mappings
                    if (string.IsNullOrEmpty(topLevelId))
                    {
                        Console.WriteLine($"Could not find top level mapping for brace {i + 1} (story UID: {topStoryUid})");
                        continue;
                    }

                    if (string.IsNullOrEmpty(baseLevelId))
                    {
                        Console.WriteLine($"Could not find base level mapping for brace {i + 1} (story UID: {baseStoryUid})");
                        continue;
                    }

                    // Get brace coordinates
                    SCoordinate topPoint = new SCoordinate();
                    SCoordinate basePoint = new SCoordinate();
                    ramBrace.GetEndCoordinates(ref topPoint, ref basePoint);

                    // Find the corresponding frame properties ID for this brace
                    
                    string framePropertiesId = ModelMappingUtility.GetFramePropertyIdForSectionLabel(ramBrace.strSectionLabel);

                    // Create brace from RAM data
                    Brace brace = new Brace
                    {
                        Id = IdGenerator.Generate(IdGenerator.Elements.BRACE),
                        StartPoint = new Point2D(
                            UnitConversionUtils.ConvertFromInches(basePoint.dXLoc, _lengthUnit),
                            UnitConversionUtils.ConvertFromInches(basePoint.dYLoc, _lengthUnit)
                        ),
                        EndPoint = new Point2D(
                            UnitConversionUtils.ConvertFromInches(topPoint.dXLoc, _lengthUnit),
                            UnitConversionUtils.ConvertFromInches(topPoint.dYLoc, _lengthUnit)
                        ),
                        BaseLevelId = baseLevelId,
                        TopLevelId = topLevelId,
                        FramePropertiesId = framePropertiesId
                    };

                    braces.Add(brace);
                    Console.WriteLine($"Exported brace {i + 1} from story {baseStoryUid} to {topStoryUid}");
                }

                return braces;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting braces from RAM: {ex.Message}");
                return braces;
            }
        }
    }
}