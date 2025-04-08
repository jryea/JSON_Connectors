using System;
using System.Collections.Generic;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Revit.Utilities;
using Autodesk.Revit.DB;

namespace Revit.Import.Elements
{
    // Imports floor elements from JSON into Revit
    public class FloorImport
    {
        private readonly DB.Document _doc;

        public FloorImport(DB.Document doc)
        {
            _doc = doc;
        }

        // Imports floors from the JSON model into Revit
        public int Import(List<CE.Floor> floors, Dictionary<string, DB.ElementId> levelIdMap, Dictionary<string, DB.ElementId> floorPropertyIdMap)
        {
            int count = 0;

            foreach (var jsonFloor in floors)
            {
                try
                {
                    // Skip floors with less than 3 points (minimum for floor profile)
                    if (jsonFloor.Points == null || jsonFloor.Points.Count < 3)
                    {
                        continue;
                    }

                    // Get level for this floor
                    if (!levelIdMap.TryGetValue(jsonFloor.LevelId, out DB.ElementId levelId))
                    {
                        continue;
                    }

                    // Get the Level from the ID
                    DB.Level level = _doc.GetElement(levelId) as DB.Level;
                    if (level == null)
                    {
                        continue;
                    }

                    // Get floor type
                    DB.ElementId floorTypeId = DB.ElementId.InvalidElementId;
                    if (!string.IsNullOrEmpty(jsonFloor.FloorPropertiesId) &&
                        floorPropertyIdMap.TryGetValue(jsonFloor.FloorPropertiesId, out floorTypeId))
                    {
                        // Floor type found in mapping
                    }
                    else
                    {
                        // Use default floor type if none specified
                        DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
                        collector.OfClass(typeof(DB.FloorType));
                        floorTypeId = collector.FirstElementId();
                    }

                    // Skip if no floor type is available
                    if (floorTypeId == DB.ElementId.InvalidElementId)
                    {
                        continue;
                    }

                    // Create a curve loop for the floor boundary
                    DB.CurveLoop floorLoop = new DB.CurveLoop();

                    // Convert each point and add to curve loop
                    for (int i = 0; i < jsonFloor.Points.Count; i++)
                    {
                        DB.XYZ startPoint = Helpers.ConvertToRevitCoordinates(jsonFloor.Points[i]);
                        DB.XYZ endPoint;

                        // If it's the last point, connect back to the first point
                        if (i == jsonFloor.Points.Count - 1)
                        {
                            endPoint = Helpers.ConvertToRevitCoordinates(jsonFloor.Points[0]);
                        }
                        else
                        {
                            endPoint = Helpers.ConvertToRevitCoordinates(jsonFloor.Points[i + 1]);
                        }

                        DB.Line line = DB.Line.CreateBound(startPoint, endPoint);
                        floorLoop.Append(line);
                    }

                    // Create list of curve loops
                    List<DB.CurveLoop> floorBoundary = new List<DB.CurveLoop>
                    {
                        floorLoop
                    };

                    // Create the floor
                    DB.Floor floor = DB.Floor.Create(_doc, floorBoundary, floorTypeId, levelId);
                    
                    count++;
                }
                catch (Exception)
                {
                    // Skip this floor and continue with the next one
                }
            }

            return count;
        }
    }
}