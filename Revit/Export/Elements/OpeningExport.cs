using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using CG = Core.Models.Geometry;
using Core.Models;

namespace Revit.Export.Elements
{
    public class OpeningExport
    {
        private readonly DB.Document _doc;

        public OpeningExport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Export(List<CE.Opening> openings, BaseModel model)
        {
            int count = 0;

            // Get all shaft openings from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.Opening> revitShafts = collector.OfCategory(DB.BuiltInCategory.OST_ShaftOpening)
                .WhereElementIsNotElementType()
                .OfClass(typeof(DB.Opening))
                .Cast<DB.Opening>()
                .ToList();

            // Create level mapping
            Dictionary<DB.ElementId, string> levelIdMap = CreateLevelMapping(model);

            foreach (var revitShaft in revitShafts)
            {
                try
                {
                    // Get shaft geometry and create openings for each level it spans
                    var shaftOpenings = ExtractOpeningsFromShaft(revitShaft, levelIdMap, model);
                    openings.AddRange(shaftOpenings);
                    count += shaftOpenings.Count;

                    Debug.WriteLine($"Extracted {shaftOpenings.Count} openings from shaft {revitShaft.Id}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing shaft {revitShaft.Id}: {ex.Message}");
                }
            }

            Debug.WriteLine($"OpeningExport: Exported {count} total openings from {revitShafts.Count} shafts");
            return count;
        }

        private List<CE.Opening> ExtractOpeningsFromShaft(DB.Opening shaft, Dictionary<DB.ElementId, string> levelIdMap, BaseModel model)
        {
            var openings = new List<CE.Opening>();

            try
            {
                // Get shaft boundary
                var boundary = GetShaftBoundary(shaft);
                if (boundary == null || boundary.Count < 4)
                {
                    Debug.WriteLine($"Could not extract valid boundary from shaft {shaft.Id}");
                    return openings;
                }

                // Get shaft extent levels
                var levels = GetShaftLevels(shaft, model);

                // Create opening for each level the shaft spans
                foreach (var level in levels)
                {
                    if (levelIdMap.TryGetValue(level.Id, out string levelId))
                    {
                        var opening = new CE.Opening(levelId, boundary);
                        openings.Add(opening);
                        Debug.WriteLine($"Created opening on level {level.Name} for shaft {shaft.Id}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting openings from shaft {shaft.Id}: {ex.Message}");
            }

            return openings;
        }

        private List<CG.Point2D> GetShaftBoundary(DB.Opening shaft)
        {
            var points = new List<CG.Point2D>();

            try
            {
                // Get the shaft's boundary curves
                var boundaryCurves = shaft.BoundaryCurves;
                if (boundaryCurves == null || boundaryCurves.Size == 0)
                    return points;

                // Extract points from boundary curves
                foreach (DB.Curve curve in boundaryCurves)
                {
                    if (curve is DB.Line line)
                    {
                        var startPt = line.GetEndPoint(0);
                        // Convert feet to inches for core model
                        points.Add(new CG.Point2D(startPt.X * 12.0, startPt.Y * 12.0));
                    }
                }

                // Remove duplicate points (common with closed boundaries)
                points = RemoveDuplicatePoints(points);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting shaft boundary: {ex.Message}");
            }

            return points;
        }

        private List<DB.Level> GetShaftLevels(DB.Opening shaft, BaseModel model)
        {
            var levels = new List<DB.Level>();

            try
            {
                // Get shaft base and top levels
                var baseLevel = GetShaftBaseLevel(shaft);
                var topLevel = GetShaftTopLevel(shaft);

                if (baseLevel == null || topLevel == null)
                    return levels;

                // Get all levels in the document sorted by elevation
                var allLevels = new DB.FilteredElementCollector(_doc)
                    .OfClass(typeof(DB.Level))
                    .Cast<DB.Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                // Find levels between base and top (inclusive)
                double baseElevation = baseLevel.Elevation;
                double topElevation = topLevel.Elevation;

                foreach (var level in allLevels)
                {
                    if (level.Elevation >= baseElevation && level.Elevation <= topElevation)
                    {
                        // Only include levels that are in the model
                        var modelLevel = model.ModelLayout.Levels?.FirstOrDefault(l =>
                            l.Name == level.Name || Math.Abs(l.Elevation - level.Elevation * 12.0) < 0.1);

                        if (modelLevel != null)
                        {
                            levels.Add(level);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting shaft levels: {ex.Message}");
            }

            return levels;
        }

        private DB.Level GetShaftBaseLevel(DB.Opening shaft)
        {
            try
            {
                // Try to get base constraint parameter
                var baseConstraintParam = shaft.get_Parameter(DB.BuiltInParameter.WALL_BASE_CONSTRAINT);
                if (baseConstraintParam != null)
                {
                    var levelId = baseConstraintParam.AsElementId();
                    return _doc.GetElement(levelId) as DB.Level;
                }

                // Fallback: get the level with the lowest elevation that the shaft intersects
                return GetLevelAtElevation(GetShaftBottomElevation(shaft));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting shaft base level: {ex.Message}");
                return null;
            }
        }

        private DB.Level GetShaftTopLevel(DB.Opening shaft)
        {
            try
            {
                // Try to get top constraint parameter
                var topConstraintParam = shaft.get_Parameter(DB.BuiltInParameter.WALL_HEIGHT_TYPE);
                if (topConstraintParam != null)
                {
                    var levelId = topConstraintParam.AsElementId();
                    return _doc.GetElement(levelId) as DB.Level;
                }

                // Fallback: get the level with the highest elevation that the shaft intersects
                return GetLevelAtElevation(GetShaftTopElevation(shaft));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting shaft top level: {ex.Message}");
                return null;
            }
        }

        private double GetShaftBottomElevation(DB.Opening shaft)
        {
            try
            {
                var bbox = shaft.get_BoundingBox(null);
                return bbox?.Min.Z ?? 0.0;
            }
            catch
            {
                return 0.0;
            }
        }

        private double GetShaftTopElevation(DB.Opening shaft)
        {
            try
            {
                var bbox = shaft.get_BoundingBox(null);
                return bbox?.Max.Z ?? 0.0;
            }
            catch
            {
                return 0.0;
            }
        }

        private DB.Level GetLevelAtElevation(double elevation)
        {
            var levels = new DB.FilteredElementCollector(_doc)
                .OfClass(typeof(DB.Level))
                .Cast<DB.Level>()
                .ToList();

            return levels
                .OrderBy(l => Math.Abs(l.Elevation - elevation))
                .FirstOrDefault();
        }

        private List<CG.Point2D> RemoveDuplicatePoints(List<CG.Point2D> points)
        {
            const double tolerance = 0.01; // 0.01 inch tolerance
            var uniquePoints = new List<CG.Point2D>();

            foreach (var point in points)
            {
                bool isDuplicate = uniquePoints.Any(p =>
                    Math.Abs(p.X - point.X) < tolerance &&
                    Math.Abs(p.Y - point.Y) < tolerance);

                if (!isDuplicate)
                {
                    uniquePoints.Add(point);
                }
            }

            return uniquePoints;
        }

        private Dictionary<DB.ElementId, string> CreateLevelMapping(BaseModel model)
        {
            var levelMap = new Dictionary<DB.ElementId, string>();

            // Get all levels from Revit
            var revitLevels = new DB.FilteredElementCollector(_doc)
                .OfClass(typeof(DB.Level))
                .Cast<DB.Level>()
                .ToList();

            // Map each Revit level to corresponding model level
            foreach (var revitLevel in revitLevels)
            {
                var modelLevel = model.ModelLayout.Levels?.FirstOrDefault(l =>
                    l.Name == revitLevel.Name ||
                    Math.Abs(l.Elevation - (revitLevel.Elevation * 12.0)) < 0.1); // Convert feet to inches

                if (modelLevel != null)
                {
                    levelMap[revitLevel.Id] = modelLevel.Id;
                }
            }

            return levelMap;
        }
    }
}