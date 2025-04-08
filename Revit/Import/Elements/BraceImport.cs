using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;
using DB = Autodesk.Revit.DB.Structure;
using C = Core.Models.Elements;
using Revit.Utilities;

namespace Revit.Import.Elements
{
    // Imports brace elements from JSON into Revit
    public class BraceImport
    {
        private readonly Document _doc;

        public BraceImport(Document doc)
        {
            _doc = doc;
        }

        // Imports braces from the JSON model into Revit
        public int Import(List<C.Brace> braces, Dictionary<string, ElementId> levelIdMap, Dictionary<string, ElementId> framePropertyIdMap)
        {
            int count = 0;

            foreach (var jsonBrace in braces)
            {
                try
                {
                    // Get base level ID
                    ElementId baseLevelId = Helpers.GetElementId(levelIdMap, jsonBrace.BaseLevelId);

                    // Get family type for this brace (from frame properties)
                    ElementId familyTypeId = ElementId.InvalidElementId;
                    if (!string.IsNullOrEmpty(jsonBrace.FramePropertiesId) && framePropertyIdMap.ContainsKey(jsonBrace.FramePropertiesId))
                    {
                        familyTypeId = framePropertyIdMap[jsonBrace.FramePropertiesId];
                    }

                    // Create curve for brace
                    XYZ startPoint = Helpers.ConvertToRevitCoordinates(jsonBrace.StartPoint);
                    XYZ endPoint = Helpers.ConvertToRevitCoordinates(jsonBrace.EndPoint);
                    Line braceLine = Line.CreateBound(startPoint, endPoint);

                    // Create the structural brace
                    FamilyInstance brace = _doc.Create.NewFamilyInstance(
                        braceLine,
                        familyTypeId,
                        baseLevelId,
                        DB.StructuralType.Brace);

                    count++;
                }
                catch (Exception ex)
                {
                    // Log the exception for this brace but continue with the next one
                    Debug.WriteLine($"Error creating brace: {ex.Message}");
                }
            }

            return count;
        }
    }
}