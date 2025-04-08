using System;
using System.Collections.Generic;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Revit.Utilities;
using Autodesk.Revit.DB;

namespace Revit.Import.Elements
{
    // Imports brace elements from JSON into Revit
    public class BraceImport
    {
        private readonly DB.Document _doc;

        public BraceImport(DB.Document doc)
        {
            _doc = doc;
        }

        // Imports braces from the JSON model into Revit
        public int Import(List<CE.Brace> braces, Dictionary<string, DB.ElementId> levelIdMap, Dictionary<string, DB.ElementId> framePropertyIdMap)
        {
            int count = 0;

            foreach (var jsonBrace in braces)
            {
                try
                {
                    // Get the base level ElementId
                    if (!levelIdMap.TryGetValue(jsonBrace.BaseLevelId, out DB.ElementId baseLevelId))
                    {
                        continue;
                    }

                    // Get base level
                    DB.Level baseLevel = _doc.GetElement(baseLevelId) as DB.Level;
                    if (baseLevel == null)
                    {
                        continue;
                    }

                    // Get family type for this brace (from frame properties)
                    if (!framePropertyIdMap.TryGetValue(jsonBrace.FramePropertiesId, out DB.ElementId familyTypeId))
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

                    // Create curve for brace
                    DB.XYZ startPoint = Helpers.ConvertToRevitCoordinates(jsonBrace.StartPoint);
                    DB.XYZ endPoint = Helpers.ConvertToRevitCoordinates(jsonBrace.EndPoint);
                    DB.Line braceLine = DB.Line.CreateBound(startPoint, endPoint);

                    // Create the structural brace
                    DB.FamilyInstance brace = _doc.Create.NewFamilyInstance(
                        braceLine,
                        familySymbol,
                        baseLevel,
                        DB.Structure.StructuralType.Brace);

                    // Set top level if available
                    if (levelIdMap.TryGetValue(jsonBrace.TopLevelId, out DB.ElementId topLevelId))
                    {
                        DB.Parameter topLevelParam = brace.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                        if (topLevelParam != null)
                        {
                            topLevelParam.Set(topLevelId);
                        }
                    }

                    count++;
                }
                catch (Exception)
                {
                    // Skip this brace and continue with the next one
                }
            }

            return count;
        }
    }
}