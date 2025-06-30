using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Core.Models;
using Core.Models.Properties;
using Revit.Utilities;
using Revit.Import.Properties;
using System.Diagnostics;

namespace Revit.Import.Elements
{
    public class FloorImport
    {
        private readonly DB.Document _doc;
        private readonly FloorPropertiesImport _floorPropertiesImport;

        public FloorImport(DB.Document doc)
        {
            _doc = doc;
            _floorPropertiesImport = new FloorPropertiesImport(doc);
        }

        public int Import(Dictionary<string, DB.ElementId> levelIdMap, BaseModel model)
        {
            if (model?.Elements?.Floors == null || !model.Elements.Floors.Any())
            {
                Debug.WriteLine("No floors to import.");
                return 0;
            }

            Debug.WriteLine($"Starting import of {model.Elements.Floors.Count} floors...");

            int successCount = 0;
            int totalCount = model.Elements.Floors.Count;

            foreach (var jsonFloor in model.Elements.Floors)
            {
                try
                {
                    bool success = ImportFloor(jsonFloor, levelIdMap, model);
                    if (success)
                    {
                        successCount++;
                        Debug.WriteLine($"✓ Successfully imported floor {jsonFloor.Id} ({successCount}/{totalCount})");
                    }
                    else
                    {
                        Debug.WriteLine($"✗ Failed to import floor {jsonFloor.Id} ({successCount}/{totalCount})");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"✗ Exception importing floor {jsonFloor.Id}: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }

            Debug.WriteLine($"Floor import completed: {successCount}/{totalCount} floors imported successfully");
            return successCount;
        }

        private bool ImportFloor(CE.Floor jsonFloor, Dictionary<string, DB.ElementId> levelIdMap, BaseModel model)
        {
            try
            {
                Debug.WriteLine($"Importing floor: {jsonFloor.Id}");

                // Validate floor has points
                if (jsonFloor.Points == null || jsonFloor.Points.Count < 3)
                {
                    Debug.WriteLine($"  Invalid boundary: {jsonFloor.Points?.Count ?? 0} points");
                    return false;
                }

                // Get level
                if (!levelIdMap.ContainsKey(jsonFloor.LevelId))
                {
                    Debug.WriteLine($"  Level not found: {jsonFloor.LevelId}");
                    return false;
                }

                DB.Level level = _doc.GetElement(levelIdMap[jsonFloor.LevelId]) as DB.Level;
                if (level == null)
                {
                    Debug.WriteLine($"  Invalid level element for ID: {jsonFloor.LevelId}");
                    return false;
                }

                // Get floor type using FloorPropertiesImport
                var floorProps = model.Properties?.FloorProperties?
                    .FirstOrDefault(fp => fp.Id == jsonFloor.FloorPropertiesId);

                DB.FloorType floorType = _floorPropertiesImport.FindOrCreateFloorType(floorProps, model);
                if (floorType == null)
                {
                    Debug.WriteLine($"  No suitable floor type found");
                    return false;
                }

                Debug.WriteLine($"  Using floor type: {floorType.Name}");

                // Create geometry
                var floorLoop = CreateFloorBoundary(jsonFloor);
                if (floorLoop == null)
                {
                    Debug.WriteLine($"  Failed to create boundary geometry");
                    return false;
                }

                // Create list of curve loops
                List<DB.CurveLoop> floorBoundary = new List<DB.CurveLoop> { floorLoop };

                // Create floor
                DB.Floor floor = DB.Floor.Create(_doc, floorBoundary, floorType.Id, level.Id);
                if (floor == null)
                {
                    Debug.WriteLine($"  Failed to create floor element");
                    return false;
                }

                // Set the is Structural parameter to true
                DB.Parameter structuralParam = floor.get_Parameter(DB.BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
                if (structuralParam != null && !structuralParam.IsReadOnly)
                {
                    structuralParam.Set(1); // 1 means true for integer parameters
                }

                // Set parameters
                SetFloorParameters(floor, jsonFloor, floorProps);

                Debug.WriteLine($"  Floor created successfully with ID: {floor.Id}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"  Error importing floor {jsonFloor.Id}: {ex.Message}");
                return false;
            }
        }

        private DB.CurveLoop CreateFloorBoundary(CE.Floor jsonFloor)
        {
            try
            {
                DB.CurveLoop floorLoop = new DB.CurveLoop();

                // Convert each point and add to curve loop
                for (int i = 0; i < jsonFloor.Points.Count; i++)
                {
                    DB.XYZ startPoint = Helpers.ConvertToRevitCoordinates(jsonFloor.Points[i]);
                    DB.XYZ endPoint;

                    // If last point, connect back to first point
                    if (i == jsonFloor.Points.Count - 1)
                    {
                        endPoint = Helpers.ConvertToRevitCoordinates(jsonFloor.Points[0]);
                    }
                    else
                    {
                        endPoint = Helpers.ConvertToRevitCoordinates(jsonFloor.Points[i + 1]);
                    }

                    // Validate that start and end points are different
                    if (startPoint.DistanceTo(endPoint) < 0.01) // 0.01 feet = ~1/8 inch
                    {
                        Debug.WriteLine($"Skipping zero-length line segment in floor {jsonFloor.Id}");
                        continue;
                    }

                    DB.Line line = DB.Line.CreateBound(startPoint, endPoint);
                    floorLoop.Append(line);
                }

                // Validate that we have a valid curve loop
                if (floorLoop.NumberOfCurves() < 3)
                {
                    Debug.WriteLine($"Invalid floor boundary: only {floorLoop.NumberOfCurves()} valid curves");
                    return null;
                }

                return floorLoop;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating floor boundary: {ex.Message}");
                return null;
            }
        }

        private void SetFloorParameters(DB.Floor floor, CE.Floor jsonFloor, FloorProperties floorProps)
        {
            try
            {
                // Set basic parameters
                var markParam = floor.get_Parameter(DB.BuiltInParameter.ALL_MODEL_MARK);
                if (markParam != null && !markParam.IsReadOnly)
                {
                    markParam.Set(jsonFloor.Id ?? "");
                }

                var commentsParam = floor.get_Parameter(DB.BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (commentsParam != null && !commentsParam.IsReadOnly && !string.IsNullOrEmpty(jsonFloor.Id))
                {
                    commentsParam.Set(jsonFloor.Id);
                }

                // Set thickness if available and different from type
                if (floorProps != null && floorProps.Thickness > 0)
                {
                    var thicknessParam = floor.get_Parameter(DB.BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
                    if (thicknessParam != null && !thicknessParam.IsReadOnly)
                    {
                        double currentThickness = thicknessParam.AsDouble();
                        if (Math.Abs(currentThickness - floorProps.Thickness) > 0.01)
                        {
                            thicknessParam.Set(floorProps.Thickness);
                        }
                    }
                }

                Debug.WriteLine($"    Parameters set for floor {jsonFloor.Id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"    Error setting parameters: {ex.Message}");
            }
        }
    }
}