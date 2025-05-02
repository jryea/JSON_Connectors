using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using CG = Core.Models.Geometry;
using Core.Models;
using Revit.Utilities;

namespace Revit.Export.Elements
{
    public class FloorExport
    {
        private readonly DB.Document _doc;

        public FloorExport(DB.Document doc)
        {
            _doc = doc;
        }

        // Modified to support filtering by level names
        public int Export(List<CE.Floor> floors, BaseModel model, HashSet<string> selectedLevelNames = null)
        {
            int count = 0;

            // Get all floors from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.Floor> revitFloors = collector.OfClass(typeof(DB.Floor))
                .WhereElementIsNotElementType()
                .Cast<DB.Floor>()
                .ToList();

            // Create mappings
            Dictionary<DB.ElementId, string> levelIdMap = CreateLevelMapping(model);
            Dictionary<DB.ElementId, string> floorTypeMap = CreateFloorTypeMapping(model);

            foreach (var revitFloor in revitFloors)
            {
                try
                {
                    // Skip non-structural floors
                    DB.Parameter isStructuralParam = revitFloor.get_Parameter(DB.BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
                    bool isStructural = isStructuralParam != null && isStructuralParam.AsInteger() != 0;

                    if (!isStructural)
                        continue;

                    // Get level name for filtering
                    DB.Element levelElement = null;
                    if (revitFloor.LevelId != null && revitFloor.LevelId != DB.ElementId.InvalidElementId)
                    {
                        levelElement = _doc.GetElement(revitFloor.LevelId);
                    }

                    // Filter by level name if requested
                    if (selectedLevelNames != null && selectedLevelNames.Count > 0)
                    {
                        if (levelElement == null || !selectedLevelNames.Contains(levelElement.Name))
                        {
                            Debug.WriteLine($"Skipping floor as it's not on a selected level");
                            continue; // Skip this floor as it's not on a selected level
                        }
                    }

                    // Create floor object
                    CE.Floor floor = new CE.Floor();

                    // Set level
                    DB.ElementId levelId = revitFloor.LevelId;
                    if (levelIdMap.ContainsKey(levelId))
                        floor.LevelId = levelIdMap[levelId];

                    // Set floor type
                    DB.ElementId floorTypeId = revitFloor.GetTypeId();
                    if (floorTypeMap.ContainsKey(floorTypeId))
                        floor.FloorPropertiesId = floorTypeMap[floorTypeId];

                    // Get floor boundary
                    List<CG.Point2D> floorPoints = new List<CG.Point2D>();

                    try
                    {
                        // Get the top faces of the floor using HostObjectUtils
                        IList<DB.Reference> topFaceReferences = DB.HostObjectUtils.GetTopFaces(revitFloor);

                        if (topFaceReferences != null && topFaceReferences.Count > 0)
                        {
                            // Get the first top face
                            DB.Face topFace = revitFloor.GetGeometryObjectFromReference(topFaceReferences[0]) as DB.Face;

                            if (topFace != null)
                            {
                                // Get the edge loops of the face
                                IList<DB.CurveLoop> curveLoops = topFace.GetEdgesAsCurveLoops();

                                if (curveLoops != null && curveLoops.Count > 0)
                                {
                                    // Get the outer loop (typically the first one)
                                    DB.CurveLoop outerLoop = curveLoops[0];

                                    // Get the points from the curve loop
                                    foreach (DB.Curve curve in outerLoop)
                                    {
                                        // Add the start point
                                        DB.XYZ point = curve.GetEndPoint(0);
                                        floorPoints.Add(new CG.Point2D(point.X * 12.0, point.Y * 12.0)); // Convert to inches
                                    }
                                }
                            }
                        }

                        // If we got points, use them
                        if (floorPoints.Count >= 3)
                        {
                            floor.Points = floorPoints;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error getting floor geometry: {ex.Message}");
                        // Skip floors with errors
                        continue;
                    }

                    // Skip floors with too few points
                    if (floor.Points.Count < 3)
                        continue;

                    // Add floor to model
                    floors.Add(floor);
                    count++;
                    Debug.WriteLine($"Exported floor on level: {levelElement?.Name}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error exporting floor: {ex.Message}");
                    // Skip this floor and continue with the next one
                }
            }

            return count;
        }

        private Dictionary<DB.ElementId, string> CreateLevelMapping(BaseModel model)
        {
            Dictionary<DB.ElementId, string> levelMap = new Dictionary<DB.ElementId, string>();

            // Get all levels from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.Level> revitLevels = collector.OfClass(typeof(DB.Level))
                .Cast<DB.Level>()
                .ToList();

            // Map each Revit level to the corresponding level in the model
            foreach (var revitLevel in revitLevels)
            {
                var modelLevel = model.ModelLayout.Levels.FirstOrDefault(l =>
                    l.Name == revitLevel.Name ||
                    Math.Abs(l.Elevation - (revitLevel.Elevation * 12.0)) < 0.1);

                if (modelLevel != null)
                {
                    levelMap[revitLevel.Id] = modelLevel.Id;
                }
            }

            return levelMap;
        }

        private Dictionary<DB.ElementId, string> CreateFloorTypeMapping(BaseModel model)
        {
            Dictionary<DB.ElementId, string> floorTypeMap = new Dictionary<DB.ElementId, string>();

            // Get all floor types from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.FloorType> revitFloorTypes = collector.OfClass(typeof(DB.FloorType))
                .Cast<DB.FloorType>()
                .ToList();

            // Map each Revit floor type to the corresponding floor property in the model
            foreach (var revitFloorType in revitFloorTypes)
            {
                var modelFloorProperty = model.Properties.FloorProperties.FirstOrDefault(fp =>
                    fp.Name == revitFloorType.Name);

                if (modelFloorProperty != null)
                {
                    floorTypeMap[revitFloorType.Id] = modelFloorProperty.Id;
                }
            }

            return floorTypeMap;
        }
    }
}