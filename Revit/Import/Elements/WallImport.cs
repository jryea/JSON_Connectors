using System;
using System.Collections.Generic;
using System.Diagnostics;
using DB = Autodesk.Revit.DB;
using C = Core.Models.Elements;
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
       
        public int Import(List<C.Wall> walls, Dictionary<string, DB.ElementId> wallPropertyIdMap)
        {
            int count = 0;

            foreach (var jsonWall in walls)
            {
                try
                {
                    // Skip walls with less than 2 points
                    if (jsonWall.Points == null || jsonWall.Points.Count < 2)
                    {
                        Debug.WriteLine("Wall has less than 2 points, skipping");
                        continue;
                    }

                    // Get wall type
                    DB.ElementId wallTypeId = DB.ElementId.InvalidElementId;
                    if (!string.IsNullOrEmpty(jsonWall.PropertiesId) && wallPropertyIdMap.ContainsKey(jsonWall.PropertiesId))
                    {
                        wallTypeId = wallPropertyIdMap[jsonWall.PropertiesId];
                    }
                    else
                    {
                        // Use default wall type if none specified
                        DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
                        collector.OfClass(typeof(DB.WallType));
                        wallTypeId = collector.FirstElementId();
                    }

                    // Create the wall curve
                    DB.Curve wallCurve = Helpers.CreateRevitCurve(jsonWall.Points[0], jsonWall.Points[1]);

                    // Get wall height (use default if not specified)
                    double wallHeight = 10.0; // Default height in feet

                    // Create the wall
                    DB.Wall wall = DB.Wall.Create(
                        _doc,
                        wallCurve,
                        wallTypeId,
                        DB.ElementId.InvalidElementId, // Level ID (use Active View's level if not specified)
                        wallHeight,
                        0.0, // Offset
                        false, // Flip
                        true); // Structural

                    count++;
                }
                catch (Exception ex)
                {
                    // Log the exception for this wall but continue with the next one
                    Debug.WriteLine($"Error creating wall: {ex.Message}");
                }
            }

            return count;
        }
    }
}