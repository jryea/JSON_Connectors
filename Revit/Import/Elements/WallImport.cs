using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;
using C = Core.Models.Elements;
using Revit.Utils;

namespace Revit.Import.Elements
{
    /// <summary>
    /// Imports wall elements from JSON into Revit
    /// </summary>
    public class WallImport
    {
        private readonly Document _doc;

        public WallImport(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Imports walls from the JSON model into Revit
        /// </summary>
        /// <param name="walls">List of walls to import</param>
        /// <param name="wallPropertyIdMap">Dictionary of wall property ID mappings</param>
        /// <returns>Number of walls imported</returns>
        public int Import(List<C.Wall> walls, Dictionary<string, ElementId> wallPropertyIdMap)
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
                    ElementId wallTypeId = ElementId.InvalidElementId;
                    if (!string.IsNullOrEmpty(jsonWall.PropertiesId) && wallPropertyIdMap.ContainsKey(jsonWall.PropertiesId))
                    {
                        wallTypeId = wallPropertyIdMap[jsonWall.PropertiesId];
                    }
                    else
                    {
                        // Use default wall type if none specified
                        FilteredElementCollector collector = new FilteredElementCollector(_doc);
                        collector.OfClass(typeof(WallType));
                        wallTypeId = collector.FirstElementId();
                    }

                    // Create the wall curve
                    Curve wallCurve = RevitTypeHelper.CreateRevitCurve(jsonWall.Points);

                    // Get wall height (use default if not specified)
                    double wallHeight = 10.0; // Default height in feet

                    // Create the wall
                    Wall wall = Wall.Create(
                        _doc,
                        wallCurve,
                        wallTypeId,
                        ElementId.InvalidElementId, // Level ID (use Active View's level if not specified)
                        wallHeight,
                        0.0, // Offset
                        false, // Flip
                        true); // Structural

                    // Set pier/spandrel configuration if specified
                    if (!string.IsNullOrEmpty(jsonWall.PierSpandrelId))
                    {
                        Parameter pierSpandrelParam = wall.LookupParameter("PierSpandrel");
                        if (pierSpandrelParam != null && pierSpandrelParam.StorageType == StorageType.String)
                        {
                            pierSpandrelParam.Set(jsonWall.PierSpandrelId);
                        }
                    }

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