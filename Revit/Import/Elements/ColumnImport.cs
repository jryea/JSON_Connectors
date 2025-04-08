using System;
using System.Collections.Generic;
using System.Diagnostics;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Revit.Utilities;

namespace Revit.Import.Elements
{
    /// Imports column elements from JSON into Revit
    public class ColumnImport
    {
        private readonly DB.Document _doc;

        public ColumnImport(DB.Document doc)
        {
            _doc = doc;
        }

        // Imports columns from the JSON model into Revit
       
        public int Import(List<CE.Column> columns, Dictionary<string, DB.ElementId> levelIdMap, Dictionary<string, DB.ElementId> framePropertyIdMap)
        {
            int count = 0;

            foreach (var jsonColumn in columns)
            {
                try
                {
                    // Get base and top level IDs
                    DB.ElementId baseLevelId = Helpers.GetElementId(levelIdMap, jsonColumn.BaseLevelId);
                    DB.ElementId topLevelId = Helpers.GetElementId(levelIdMap, jsonColumn.TopLevelId);

                    // Get family type for this column (from frame properties)
                    DB.ElementId familyTypeId = DB.ElementId.InvalidElementId;
                    if (!string.IsNullOrEmpty(jsonColumn.FramePropertiesId) && framePropertyIdMap.ContainsKey(jsonColumn.FramePropertiesId))
                    {
                        familyTypeId = framePropertyIdMap[jsonColumn.FramePropertiesId];
                    }

                    // Get column insertion point
                    DB.XYZ columnPoint = Helpers.ConvertToRevitCoordinates(jsonColumn.StartPoint);

                    // Create the structural column
                    DB.FamilyInstance column = _doc.Create.NewFamilyInstance(
                        columnPoint,
                        familyTypeId,
                        baseLevelId,
                        DB.Structure.StructuralType.Column);

                    // Set top level
                    DB.Parameter topLevelParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                    if (topLevelParam != null)
                    {
                        topLevelParam.Set(topLevelId);
                    }

                    count++;
                }
                catch (Exception ex)
                {
                    // Log the exception for this column but continue with the next one
                    Debug.WriteLine($"Error creating column: {ex.Message}");
                }
            }

            return count;
        }
    }
}