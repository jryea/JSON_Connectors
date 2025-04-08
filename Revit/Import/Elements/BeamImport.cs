using System;
using System.Collections.Generic;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Revit.Utilities;
using Autodesk.Revit.DB;

namespace Revit.Import.Elements
{
    // Imports beam elements from JSON into Revit
    public class BeamImport
    {
        private readonly DB.Document _doc;

        public BeamImport(DB.Document doc)
        {
            _doc = doc;
        }

        // Imports beams from the JSON model into Revit
        public int Import(List<CE.Beam> beams, Dictionary<string, DB.ElementId> levelIdMap, Dictionary<string, DB.ElementId> framePropertyIdMap)
        {
            int count = 0;

            foreach (var jsonBeam in beams)
            {
                try
                {
                    // Get the ElementId for the level
                    if (!levelIdMap.TryGetValue(jsonBeam.LevelId, out DB.ElementId levelId))
                    {
                        continue;
                    }

                    // Get the Level from the ID
                    DB.Level level = _doc.GetElement(levelId) as DB.Level;
                    if (level == null)
                    {
                        continue;
                    }

                    // Get family type for this beam (from frame properties)
                    if (!framePropertyIdMap.TryGetValue(jsonBeam.FramePropertiesId, out DB.ElementId familyTypeId))
                    {
                        continue;
                    }

                    // Get the FamilySymbol from the ID
                    DB.FamilySymbol familySymbol = _doc.GetElement(familyTypeId) as DB.FamilySymbol;
                    if (familySymbol == null)
                    {
                        continue;
                    }

                    // Make sure the family symbol is active
                    if (!familySymbol.IsActive)
                    {
                        familySymbol.Activate();
                    }

                    // Create curve for beam
                    DB.XYZ startPoint = Helpers.ConvertToRevitCoordinates(jsonBeam.StartPoint);
                    DB.XYZ endPoint = Helpers.ConvertToRevitCoordinates(jsonBeam.EndPoint);
                    DB.Line beamLine = DB.Line.CreateBound(startPoint, endPoint);

                    // Create the structural beam
                    DB.FamilyInstance beam = _doc.Create.NewFamilyInstance(
                        beamLine,
                        familySymbol,
                        level,
                        DB.Structure.StructuralType.Beam);

                    // Set beam reference level
                    beam.get_Parameter(DB.BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).Set(levelId);
                    // Set start level offset (in feet)
                    double startOffset = 0; // Example: 2 feet
                    beam.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION).Set(startOffset);
                    // Set end level offset (in feet)
                    double endOffset = 0; // Example: 3 feet
                    beam.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION).Set(endOffset);

                    count++;
                }
                catch (Exception)
                {
                    // Skip this beam and continue with the next one
                }
            }

            return count;
        }
    }
}
