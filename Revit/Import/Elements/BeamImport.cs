using System;
using System.Collections.Generic;
using System.Diagnostics;
using DB = Autodesk.Revit.DB;
using C = Core.Models.Elements;
using Revit.Utilities;

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
        public int Import(List<C.Beam> beams, Dictionary<string, DB.ElementId> levelIdMap, Dictionary<string, DB.ElementId> framePropertyIdMap)
        {
            int count = 0;

            foreach (var jsonBeam in beams)
            {
                try
                {
                    // Get the level for this beam
                    DB.ElementId levelId = Helpers.GetElementId(levelIdMap, jsonBeam.LevelId);

                    // Get family type for this beam (from frame properties)
                    DB.ElementId familyTypeId = DB.ElementId.InvalidElementId;
                    if (!string.IsNullOrEmpty(jsonBeam.FramePropertiesId) && framePropertyIdMap.ContainsKey(jsonBeam.FramePropertiesId))
                    {
                        familyTypeId = framePropertyIdMap[jsonBeam.FramePropertiesId];
                    }

                    // Create curve for beam
                    DB.XYZ startPoint = Helpers.ConvertToRevitCoordinates(jsonBeam.StartPoint);
                    DB.XYZ endPoint = Helpers.ConvertToRevitCoordinates(jsonBeam.EndPoint);
                    DB.Line beamLine = DB.Line.CreateBound(startPoint, endPoint);

                    // Get the FamilySymbol from the ID
                    DB.FamilySymbol familySymbol = _doc.GetElement(familyTypeId) as DB.FamilySymbol;

                    // Get the Level from the ID
                    DB.Level level = _doc.GetElement(levelId) as DB.Level;

                    // Make sure the family symbol is active
                    if (familySymbol != null && !familySymbol.IsActive)
                    {
                        familySymbol.Activate();
                    }

                    // Create the structural beam
                    DB.FamilyInstance beam = _doc.Create.NewFamilyInstance(
                        beamLine,
                        familySymbol,
                        level,
                        DB.Structure.StructuralType.Beam);

                    count++;
                }
                catch (Exception ex)
                {
                    // Log the exception for this beam but continue with the next one
                    Debug.WriteLine($"Error creating beam: {ex.Message}");
                }
            }

            return count;
        }
    }
}