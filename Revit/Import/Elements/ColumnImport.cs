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
    /// Imports column elements from JSON into Revit
    /// </summary>
    public class ColumnImport
    {
        private readonly Document _doc;

        public ColumnImport(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Imports columns from the JSON model into Revit
        /// </summary>
        /// <param name="columns">List of columns to import</param>
        /// <param name="levelIdMap">Dictionary of level ID mappings</param>
        /// <param name="framePropertyIdMap">Dictionary of frame property ID mappings</param>
        /// <returns>Number of columns imported</returns>
        public int Import(List<C.Column> columns, Dictionary<string, ElementId> levelIdMap, Dictionary<string, ElementId> framePropertyIdMap)
        {
            int count = 0;

            foreach (var jsonColumn in columns)
            {
                try
                {
                    // Get base and top level IDs
                    ElementId baseLevelId = RevitTypeHelper.GetElementId(levelIdMap, jsonColumn.BaseLevelId, "Base Level");
                    ElementId topLevelId = RevitTypeHelper.GetElementId(levelIdMap, jsonColumn.TopLevelId, "Top Level");

                    // Get family type for this column (from frame properties)
                    ElementId familyTypeId = ElementId.InvalidElementId;
                    if (!string.IsNullOrEmpty(jsonColumn.FramePropertiesId) && framePropertyIdMap.ContainsKey(jsonColumn.FramePropertiesId))
                    {
                        familyTypeId = framePropertyIdMap[jsonColumn.FramePropertiesId];
                    }

                    // Get column insertion point
                    XYZ columnPoint = RevitTypeHelper.ConvertToRevitCoordinates(jsonColumn.StartPoint);

                    // Create the structural column
                    FamilyInstance column = _doc.Create.NewFamilyInstance(
                        columnPoint,
                        familyTypeId,
                        baseLevelId,
                        StructuralType.Column);

                    // Set top level
                    Parameter topLevelParam = column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                    if (topLevelParam != null)
                    {
                        topLevelParam.Set(topLevelId);
                    }

                    // Set lateral flag if applicable
                    if (jsonColumn.IsLateral)
                    {
                        Parameter lateralParam = column.LookupParameter("IsLateral");
                        if (lateralParam != null && lateralParam.StorageType == StorageType.Integer)
                        {
                            lateralParam.Set(1);
                        }
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