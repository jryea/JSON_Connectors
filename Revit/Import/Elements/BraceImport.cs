using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using C = Core.Models.Elements;
using Revit.Utils;

namespace Revit.Import.Elements
{
    /// <summary>
    /// Imports brace elements from JSON into Revit
    /// </summary>
    public class BraceImport
    {
        private readonly Document _doc;

        public BraceImport(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Imports braces from the JSON model into Revit
        /// </summary>
        /// <param name="braces">List of braces to import</param>
        /// <param name="levelIdMap">Dictionary of level ID mappings</param>
        /// <param name="framePropertyIdMap">Dictionary of frame property ID mappings</param>
        /// <returns>Number of braces imported</returns>
        public int Import(List<C.Brace> braces, Dictionary<string, ElementId> levelIdMap, Dictionary<string, ElementId> framePropertyIdMap)
        {
            int count = 0;

            foreach (var jsonBrace in braces)
            {
                try
                {
                    // Get base level ID
                    ElementId baseLevelId = RevitTypeHelper.GetElementId(levelIdMap, jsonBrace.BaseLevelId, "Base Level");

                    // Get family type for this brace (from frame properties)
                    ElementId familyTypeId = ElementId.InvalidElementId;
                    if (!string.IsNullOrEmpty(jsonBrace.FramePropertiesId) && framePropertyIdMap.ContainsKey(jsonBrace.FramePropertiesId))
                    {
                        familyTypeId = framePropertyIdMap[jsonBrace.FramePropertiesId];
                    }

                    // Create curve for brace
                    XYZ startPoint = RevitTypeHelper.ConvertToRevitCoordinates(jsonBrace.StartPoint);
                    XYZ endPoint = RevitTypeHelper.ConvertToRevitCoordinates(jsonBrace.EndPoint);
                    Line braceLine = Line.CreateBound(startPoint, endPoint);

                    // Create the structural brace
                    FamilyInstance brace = _doc.Create.NewFamilyInstance(
                        braceLine,
                        familyTypeId,
                        baseLevelId,
                        StructuralType.Brace);

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