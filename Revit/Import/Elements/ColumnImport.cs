using System;
using System.Collections.Generic;
using System.Diagnostics;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Revit.Utilities;
using Autodesk.Revit.DB;

namespace Revit.Import.Elements
{
    // Imports column elements from JSON into Revit
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
                    // Skip if any required data is missing
                    if (string.IsNullOrEmpty(jsonColumn.BaseLevelId) ||
                        string.IsNullOrEmpty(jsonColumn.TopLevelId) ||
                        string.IsNullOrEmpty(jsonColumn.FramePropertiesId) ||
                        jsonColumn.StartPoint == null)
                    {
                        Debug.WriteLine($"Skipping column {jsonColumn.Id} due to missing data.");
                        continue;
                    }

                    // Get the base and top level ElementIds
                    if (!levelIdMap.TryGetValue(jsonColumn.BaseLevelId, out DB.ElementId baseLevelId) ||
                        !levelIdMap.TryGetValue(jsonColumn.TopLevelId, out DB.ElementId topLevelId))
                    {
                        Debug.WriteLine($"Skipping column {jsonColumn.Id} due to missing level mapping.");
                        continue;
                    }

                    // Get the levels
                    DB.Level baseLevel = _doc.GetElement(baseLevelId) as DB.Level;
                    DB.Level topLevel = _doc.GetElement(topLevelId) as DB.Level;
                    if (baseLevel == null || topLevel == null)
                    {
                        Debug.WriteLine($"Skipping column {jsonColumn.Id} due to missing levels.");
                        continue;
                    }

                    // Log level elevations for debugging
                    double baseElevation = baseLevel.ProjectElevation;
                    double topElevation = topLevel.ProjectElevation;
                    Debug.WriteLine($"Base Level: {baseLevel.Name}, Elevation: {baseElevation}");
                    Debug.WriteLine($"Top Level: {topLevel.Name}, Elevation: {topElevation}");

                    // Skip if top level isn't higher than base level
                    if (topElevation <= baseElevation)
                    {
                        Debug.WriteLine($"Skipping column {jsonColumn.Id} because top level elevation is not higher than base level elevation.");
                        continue;
                    }

                    // Get family type for this column
                    if (!framePropertyIdMap.TryGetValue(jsonColumn.FramePropertiesId, out DB.ElementId familyTypeId))
                    {
                        Debug.WriteLine($"Skipping column {jsonColumn.Id} due to missing frame property mapping.");
                        continue;
                    }

                    // Get the FamilySymbol
                    DB.FamilySymbol familySymbol = _doc.GetElement(familyTypeId) as DB.FamilySymbol;
                    if (familySymbol == null)
                    {
                        Debug.WriteLine($"Skipping column {jsonColumn.Id} because family symbol is null.");
                        continue;
                    }

                    // Make sure the family symbol is active
                    if (!familySymbol.IsActive)
                    {
                        familySymbol.Activate();
                    }

                    // Get column insertion point
                    DB.XYZ columnPoint = new DB.XYZ(
                        jsonColumn.StartPoint.X,
                        jsonColumn.StartPoint.Y,
                        baseLevel.ProjectElevation); // Set z to the base level's elevation

                    // Check for existing columns at the same point
                    var existingColumns = new DB.FilteredElementCollector(_doc)
                        .OfClass(typeof(DB.FamilyInstance))
                        .OfCategory(DB.BuiltInCategory.OST_StructuralColumns)
                        .WhereElementIsNotElementType()
                        .ToElements();

                    foreach (var existingColumn in existingColumns)
                    {
                        var location = existingColumn.Location as DB.LocationPoint;
                        if (location != null && location.Point.IsAlmostEqualTo(columnPoint))
                        {
                            Debug.WriteLine($"A column already exists at point {columnPoint}. Skipping column {jsonColumn.Id}.");
                            continue;
                        }
                    }

                    // Create the column
                    DB.FamilyInstance column = _doc.Create.NewFamilyInstance(
                        columnPoint,
                        familySymbol,
                        baseLevel,
                        DB.Structure.StructuralType.Column);

                    if (column == null)
                    {
                        Debug.WriteLine($"Failed to create column {jsonColumn.Id}.");
                        continue;
                    }

                    // Set top level
                    DB.Parameter topLevelParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                    if (topLevelParam != null && !topLevelParam.IsReadOnly)
                    {
                        topLevelParam.Set(topLevelId);
                    }

                    // Set offsets if applicable
                    if (jsonColumn.EndPoint != null)
                    {
                        double topOffset = 100;
                        DB.Parameter topOffsetParam = column.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                        if (topOffsetParam != null && !topOffsetParam.IsReadOnly)
                        {
                            topOffsetParam.Set(topOffset);
                        }
                    }

                    // Log column parameters
                    Debug.WriteLine($"Column {jsonColumn.Id} created with Base Level: {baseLevel.Name}, Top Level: {topLevel.Name}");

                    count++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating column {jsonColumn.Id}: {ex.Message}");
                }
            }

            return count;
        }
    }
}