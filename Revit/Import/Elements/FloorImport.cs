using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;
using C = Core.Models.Elements;
using Revit.Utils;

namespace Revit.Import.Elements
{
    /// <summary>
    /// Imports floor elements from JSON into Revit
    /// </summary>
    public class FloorImport
    {
        private readonly Document _doc;

        public FloorImport(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Imports floors from the JSON model into Revit
        /// </summary>
        /// <param name="floors">List of floors to import</param>
        /// <param name="levelIdMap">Dictionary of level ID mappings</param>
        /// <param name="floorPropertyIdMap">Dictionary of floor property ID mappings</param>
        /// <returns>Number of floors imported</returns>
        public int Import(List<C.Floor> floors, Dictionary<string, ElementId> levelIdMap, Dictionary<string, ElementId> floorPropertyIdMap)
        {
            int count = 0;

            foreach (var jsonFloor in floors)
            {
                try
                {
                    // Skip floors with less than 3 points (minimum for floor profile)
                    if (jsonFloor.Points == null || jsonFloor.Points.Count < 3)
                    {
                        Debug.WriteLine("Floor has less than 3 points, skipping");
                        continue;
                    }

                    // Get level for this floor
                    ElementId levelId = RevitTypeHelper.GetElementId(levelIdMap, jsonFloor.LevelId, "Level");

                    // Get floor type
                    ElementId floorTypeId = ElementId.InvalidElementId;
                    if (!string.IsNullOrEmpty(jsonFloor.FloorPropertiesId) && floorPropertyIdMap.ContainsKey(jsonFloor.FloorPropertiesId))
                    {
                        floorTypeId = floorPropertyIdMap[jsonFloor.FloorPropertiesId];
                    }
                    else
                    {
                        // Use default floor type if none specified
                        FilteredElementCollector collector = new FilteredElementCollector(_doc);
                        collector.OfClass(typeof(FloorType));
                        floorTypeId = collector.FirstElementId();
                    }

                    // Create a curve loop for the floor boundary
                    CurveLoop floorLoop = new CurveLoop();

                    // Convert each point and add to curve loop
                    for (int i = 0; i < jsonFloor.Points.Count; i++)
                    {
                        XYZ startPoint = RevitTypeHelper.ConvertToRevitCoordinates(jsonFloor.Points[i]);
                        XYZ endPoint;

                        // If it's the last point, connect back to the first point
                        if (i == jsonFloor.Points.Count - 1)
                        {
                            endPoint = RevitTypeHelper.ConvertToRevitCoordinates(jsonFloor.Points[0]);
                        }
                        else
                        {
                            endPoint = RevitTypeHelper.ConvertToRevitCoordinates(jsonFloor.Points[i + 1]);
                        }

                        Line line = Line.CreateBound(startPoint, endPoint);
                        floorLoop.Append(line);
                    }

                    // Create list of curve loops
                    List<CurveLoop> floorBoundary = new List<CurveLoop>
                    {
                        floorLoop
                    };

                    // Create the floor
                    Floor floor = Floor.Create(_doc, floorBoundary, floorTypeId, levelId);

                    // Set diaphragm if specified
                    if (!string.IsNullOrEmpty(jsonFloor.DiaphragmId))
                    {
                        Parameter diaphragmParam = floor.LookupParameter("Diaphragm");
                        if (diaphragmParam != null && diaphragmParam.StorageType == StorageType.String)
                        {
                            diaphragmParam.Set(jsonFloor.DiaphragmId);
                        }
                    }

                    // Set surface load if specified
                    if (!string.IsNullOrEmpty(jsonFloor.SurfaceLoadId))
                    {
                        Parameter surfaceLoadParam = floor.LookupParameter("SurfaceLoad");
                        if (surfaceLoadParam != null && surfaceLoadParam.StorageType == StorageType.String)
                        {
                            surfaceLoadParam.Set(jsonFloor.SurfaceLoadId);
                        }
                    }

                    count++;
                }
                catch (Exception ex)
                {
                    // Log the exception for this floor but continue with the next one
                    Debug.WriteLine($"Error creating floor: {ex.Message}");
                }
            }

            return count;
        }
    }
}