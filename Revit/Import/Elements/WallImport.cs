using System;
using System.Collections.Generic;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Revit.Utilities;

namespace Revit.Import.Elements
{
    // Imports wall elements from JSON into Revit
    public class WallImport
    {
        private readonly DB.Document _doc;

        public WallImport(DB.Document doc)
        {
            _doc = doc;
        }

        // Imports walls from the JSON model into Revit
        public int Import(List<CE.Wall> walls, Dictionary<string, DB.ElementId> levelIdMap, Dictionary<string, DB.ElementId> wallPropertyIdMap)
        {
            int count = 0;

            foreach (var jsonWall in walls)
            {
                try
                {
                    // Skip walls with less than 2 points
                    if (jsonWall.Points == null || jsonWall.Points.Count < 2)
                    {
                        continue;
                    }

                    // Get base and top levels
                    if (!levelIdMap.TryGetValue(jsonWall.BaseLevelId, out DB.ElementId baseLevelId) ||
                        !levelIdMap.TryGetValue(jsonWall.TopLevelId, out DB.ElementId topLevelId))
                    {
                        continue;
                    }

                    // Get the Levels from the IDs
                    DB.Level baseLevel = _doc.GetElement(baseLevelId) as DB.Level;
                    DB.Level topLevel = _doc.GetElement(topLevelId) as DB.Level;
                    if (baseLevel == null || topLevel == null)
                    {
                        continue;
                    }

                    // Get wall type
                    DB.ElementId wallTypeId = DB.ElementId.InvalidElementId;
                    if (!string.IsNullOrEmpty(jsonWall.PropertiesId) &&
                        wallPropertyIdMap.TryGetValue(jsonWall.PropertiesId, out wallTypeId))
                    {
                        // Wall type found in mapping
                    }
                    else
                    {
                        // Use default wall type if none specified
                        DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
                        collector.OfClass(typeof(DB.WallType));
                        wallTypeId = collector.FirstElementId();
                    }

                    // Skip if no wall type is available
                    if (wallTypeId == DB.ElementId.InvalidElementId)
                    {
                        continue;
                    }

                    // Create the wall curve
                    DB.Curve wallCurve = Helpers.CreateRevitCurve(jsonWall.Points[0], jsonWall.Points[1]);

                    // Calculate wall height as difference between base and top levels
                    double baseElevation = baseLevel.Elevation;
                    double topElevation = topLevel.Elevation;
                    double wallHeight = topElevation - baseElevation;

                    // Create the wall
                    DB.Wall wall = DB.Wall.Create(
                        _doc,
                        wallCurve,
                        wallTypeId,
                        baseLevelId,
                        wallHeight,
                        0.0, // Offset
                        false, // Flip
                        true); // Structural

                    // Set top level constraint
                    wall.get_Parameter(DB.BuiltInParameter.WALL_HEIGHT_TYPE).Set(topLevelId);

                    // Set pier/spandrel if specified
                    if (!string.IsNullOrEmpty(jsonWall.PierSpandrelId))
                    {
                        DB.Parameter pierSpandrelParam = wall.LookupParameter("PierSpandrel");
                        if (pierSpandrelParam != null && pierSpandrelParam.StorageType == DB.StorageType.String)
                        {
                            pierSpandrelParam.Set(jsonWall.PierSpandrelId);
                        }
                    }

                    count++;
                }
                catch (Exception)
                {
                    // Skip this wall and continue with the next one
                }
            }

            return count;
        }
    }
}