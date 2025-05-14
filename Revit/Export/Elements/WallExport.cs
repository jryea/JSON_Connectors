using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using CG = Core.Models.Geometry;
using Core.Models;
using Core.Models.Properties;
using Revit.Utilities;

namespace Revit.Export.Elements
{
    public class WallExport
    {
        private readonly DB.Document _doc;

        public WallExport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Export(List<CE.Wall> walls, BaseModel model)
        {
            int count = 0;

            // Get all walls from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.Wall> revitWalls = collector.OfCategory(DB.BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .Where(w => !(w is DB.DirectShape))
                .Cast<DB.Wall>()
                .ToList();

            // Create a mapping of levels to level IDs in the model
            Dictionary<DB.ElementId, string> levelIdMap = CreateLevelMapping(model);

            // Create a mapping of wall types to wall property IDs in the model
            Dictionary<DB.ElementId, string> wallTypeMap = CreateWallTypeMapping(model);

            foreach (var revitWall in revitWalls)
            {
                try
                {
                    // Skip non-structural walls
                    DB.Parameter isStructuralParam = revitWall.get_Parameter(DB.BuiltInParameter.WALL_STRUCTURAL_USAGE_PARAM);
                    bool isStructural = isStructuralParam != null && isStructuralParam.AsInteger() > 0;

                    if (!isStructural)
                        continue;

                    // Get wall location
                    DB.LocationCurve location = revitWall.Location as DB.LocationCurve;
                    if (location == null)
                        continue;

                    DB.Curve curve = location.Curve;
                    if (!(curve is DB.Line))
                        continue; // Skip curved walls

                    DB.Line line = curve as DB.Line;

                    // Create wall object
                    CE.Wall wall = new CE.Wall();

                    // Set base and top levels
                    DB.ElementId baseLevelId = revitWall.get_Parameter(DB.BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId();
                    DB.ElementId topLevelId = revitWall.get_Parameter(DB.BuiltInParameter.WALL_HEIGHT_TYPE).AsElementId();

                    if (levelIdMap.ContainsKey(baseLevelId))
                        wall.BaseLevelId = levelIdMap[baseLevelId];

                    if (levelIdMap.ContainsKey(topLevelId))
                        wall.TopLevelId = levelIdMap[topLevelId];
                    else
                        wall.TopLevelId = wall.BaseLevelId; // Default to base level if top level not found

                    // Set wall type
                    DB.ElementId wallTypeId = revitWall.GetTypeId();
                    if (wallTypeMap.ContainsKey(wallTypeId))
                        wall.PropertiesId = wallTypeMap[wallTypeId];

                    // Set wall geometry
                    DB.XYZ startPoint = line.GetEndPoint(0);
                    DB.XYZ endPoint = line.GetEndPoint(1);

                    wall.Points.Add(new CG.Point2D(startPoint.X * 12.0, startPoint.Y * 12.0)); // Convert feet to inches
                    wall.Points.Add(new CG.Point2D(endPoint.X * 12.0, endPoint.Y * 12.0));
                    walls.Add(wall);
                    count++;
                }
                catch (Exception)
                {
                    // Skip this wall and continue with the next one
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
                    Math.Abs(l.Elevation - (revitLevel.Elevation * 12.0)) < 0.1); // Convert feet to inches with small tolerance

                if (modelLevel != null)
                {
                    levelMap[revitLevel.Id] = modelLevel.Id;
                }
            }

            return levelMap;
        }

        private Dictionary<DB.ElementId, string> CreateWallTypeMapping(BaseModel model)
        {
            Dictionary<DB.ElementId, string> wallTypeMap = new Dictionary<DB.ElementId, string>();

            // Get all wall types from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.WallType> revitWallTypes = collector.OfClass(typeof(DB.WallType))
                .Cast<DB.WallType>()
                .ToList();

            // Map each Revit wall type to the corresponding wall property in the model
            foreach (var revitWallType in revitWallTypes)
            {
                var modelWallProperty = model.Properties.WallProperties.FirstOrDefault(wp =>
                    wp.Name == revitWallType.Name);

                if (modelWallProperty != null)
                {
                    wallTypeMap[revitWallType.Id] = modelWallProperty.Id;
                }
            }

            return wallTypeMap;
        }
    }
}