using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using DB = Autodesk.Revit.DB;
using Core.Models;
using Core.Models.Elements;
using Core.Models.Geometry;

namespace Revit.Import.Elements
{
    public class OpeningImport
    {
        private readonly DB.Document _doc;
        private const double COORDINATE_TOLERANCE = 0.01; // 0.01 feet tolerance
        private const double SHAFT_BUFFER = 1.0; // 1 foot buffer above/below

        public OpeningImport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Import(List<Opening> openings, Dictionary<string, DB.ElementId> levelIdMap, BaseModel model)
        {
            if (openings == null || openings.Count == 0)
                return 0;

            Debug.WriteLine($"OpeningImport: Starting import of {openings.Count} openings");

            try
            {
                var openingManager = new OpeningImportManager(_doc, levelIdMap);

                // Process each opening
                foreach (var opening in openings)
                {
                    try
                    {
                        // Skip non-rectangular openings for now
                        if (!IsRectangularOpening(opening))
                        {
                            Debug.WriteLine($"Skipping non-rectangular opening {opening.Id}");
                            continue;
                        }

                        // Get the level
                        if (!levelIdMap.TryGetValue(opening.LevelId, out DB.ElementId levelId))
                        {
                            Debug.WriteLine($"Level not found for opening {opening.Id}: {opening.LevelId}");
                            continue;
                        }

                        DB.Level level = _doc.GetElement(levelId) as DB.Level;
                        if (level == null)
                            continue;

                        // Calculate opening bounds
                        var bounds = CalculateOpeningBounds(opening);

                        // Add to manager for stacking
                        openingManager.AddOpening(opening.Id, bounds, level, opening.LevelId);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing opening {opening.Id}: {ex.Message}");
                    }
                }

                // Create the shafts
                return openingManager.CreateShafts();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Critical error in opening import: {ex.Message}");
                return 0;
            }
        }

        private bool IsRectangularOpening(Opening opening)
        {
            // For now, just check if we have 4 points (can enhance later for better rectangle validation)
            return opening.Points != null && opening.Points.Count == 4;
        }

        private OpeningBounds CalculateOpeningBounds(Opening opening)
        {
            if (opening.Points == null || opening.Points.Count == 0)
                return null;

            double minX = opening.Points.Min(p => p.X);
            double maxX = opening.Points.Max(p => p.X);
            double minY = opening.Points.Min(p => p.Y);
            double maxY = opening.Points.Max(p => p.Y);

            // Convert from inches to feet (Revit units)
            return new OpeningBounds
            {
                MinX = minX / 12.0,
                MaxX = maxX / 12.0,
                MinY = minY / 12.0,
                MaxY = maxY / 12.0,
                CenterX = (minX + maxX) / 2.0 / 12.0,
                CenterY = (minY + maxY) / 2.0 / 12.0,
                Width = (maxX - minX) / 12.0,
                Height = (maxY - minY) / 12.0
            };
        }
    }

    // Helper classes for opening management
    internal class OpeningBounds
    {
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    internal class OpeningData
    {
        public string Id { get; set; }
        public OpeningBounds Bounds { get; set; }
        public DB.Level Level { get; set; }
        public string LevelId { get; set; }
        public DB.ElementId RevitLevelId { get; set; }
    }

    internal class OpeningImportManager
    {
        private readonly DB.Document _doc;
        private readonly Dictionary<string, DB.ElementId> _levelIdMap;
        private readonly List<OpeningData> _openings = new List<OpeningData>();
        private readonly Dictionary<string, DB.Opening> _createdShafts = new Dictionary<string, DB.Opening>();
        private const double SHAFT_BUFFER = 1.0; // 1 foot buffer above/below

        public OpeningImportManager(DB.Document doc, Dictionary<string, DB.ElementId> levelIdMap)
        {
            _doc = doc;
            _levelIdMap = levelIdMap;
        }

        public void AddOpening(string id, OpeningBounds bounds, DB.Level level, string levelId)
        {
            if (bounds == null || level == null)
                return;

            _openings.Add(new OpeningData
            {
                Id = id,
                Bounds = bounds,
                Level = level,
                LevelId = levelId,
                RevitLevelId = level.Id
            });
        }

        public int CreateShafts()
        {
            if (_openings.Count == 0)
                return 0;

            Debug.WriteLine($"OpeningImportManager: Creating shafts for {_openings.Count} openings");

            // Group openings by location for potential stacking
            var locationGroups = _openings.GroupBy(o => new {
                X = Math.Round(o.Bounds.CenterX, 3),
                Y = Math.Round(o.Bounds.CenterY, 3),
                Width = Math.Round(o.Bounds.Width, 3),
                Height = Math.Round(o.Bounds.Height, 3)
            }).ToList();

            int totalCreated = 0;

            foreach (var locationGroup in locationGroups)
            {
                var openingsAtLocation = locationGroup.OrderBy(o => o.Level.Elevation).ToList();

                if (CanStackOpenings(openingsAtLocation))
                {
                    totalCreated += CreateStackedShaft(openingsAtLocation);
                }
                else
                {
                    totalCreated += CreateIndividualShafts(openingsAtLocation);
                }
            }

            Debug.WriteLine($"OpeningImportManager: Created {totalCreated} shafts total");
            return totalCreated;
        }

        private bool CanStackOpenings(List<OpeningData> openings)
        {
            if (openings.Count <= 1)
                return false;

            // Sort by level elevation
            openings = openings.OrderBy(o => o.Level.Elevation).ToList();

            // For now, just check if we have openings on consecutive levels
            // Could enhance to check for actual level continuity
            return openings.Count > 1;
        }

        private int CreateStackedShaft(List<OpeningData> openings)
        {
            if (openings.Count == 0)
                return 0;

            try
            {
                var sortedOpenings = openings.OrderBy(o => o.Level.Elevation).ToList();
                var bottomOpening = sortedOpenings.First();
                var topOpening = sortedOpenings.Last();

                // Calculate shaft extents with buffer
                double bottomElevation = bottomOpening.Level.Elevation - (1.0 / 12.0); // 1 foot buffer in feet
                double topElevation = topOpening.Level.Elevation + (1.0 / 12.0); // 1 foot buffer in feet

                // Create shaft using Revit's opening-by-face method (basic approach)
                return CreateShaftOpening(bottomOpening.Bounds, bottomElevation, topElevation, $"Stacked_{bottomOpening.Id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating stacked shaft: {ex.Message}");
                return 0;
            }
        }

        private int CreateIndividualShafts(List<OpeningData> openings)
        {
            int count = 0;

            foreach (var opening in openings)
            {
                try
                {
                    // Create individual shaft with buffer above and below
                    double bottomElevation = opening.Level.Elevation - (1.0 / 12.0); // 1 foot buffer
                    double topElevation = opening.Level.Elevation + (1.0 / 12.0); // 1 foot buffer

                    count += CreateShaftOpening(opening.Bounds, bottomElevation, topElevation, opening.Id);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating individual shaft for {opening.Id}: {ex.Message}");
                }
            }

            return count;
        }

        private int CreateShaftOpening(OpeningBounds bounds, double bottomElevation, double topElevation, string shaftId)
        {
            try
            {
                // Find the levels corresponding to the opening range
                DB.Level bottomLevel = FindLevelForOpening(bottomElevation, topElevation, true);
                DB.Level topLevel = FindLevelForOpening(bottomElevation, topElevation, false);

                if (bottomLevel == null || topLevel == null)
                {
                    Debug.WriteLine($"Could not find appropriate levels for shaft {shaftId}");
                    return CreateDirectShapeShaft(bounds, bottomElevation, topElevation, shaftId);
                }

                // Create rectangular shaft profile using CurveArray
                var curveArray = new DB.CurveArray();

                // Create rectangle corners in feet (Revit units)
                DB.XYZ p1 = new DB.XYZ(bounds.MinX, bounds.MinY, 0);
                DB.XYZ p2 = new DB.XYZ(bounds.MaxX, bounds.MinY, 0);
                DB.XYZ p3 = new DB.XYZ(bounds.MaxX, bounds.MaxY, 0);
                DB.XYZ p4 = new DB.XYZ(bounds.MinX, bounds.MaxY, 0);

                // Add lines to curve array
                curveArray.Append(DB.Line.CreateBound(p1, p2));
                curveArray.Append(DB.Line.CreateBound(p2, p3));
                curveArray.Append(DB.Line.CreateBound(p3, p4));
                curveArray.Append(DB.Line.CreateBound(p4, p1));

                // Create shaft using correct API
                var shaft = _doc.Create.NewOpening(bottomLevel, topLevel, curveArray);

                if (shaft != null)
                {
                    // Set shaft parameters to control actual extents
                    try
                    {
                        // WALL_BASE_OFFSET controls bottom offset (negative for below)
                        var baseOffsetParam = shaft.get_Parameter(DB.BuiltInParameter.WALL_BASE_OFFSET);
                        if (baseOffsetParam != null && !baseOffsetParam.IsReadOnly)
                        {
                            baseOffsetParam.Set(-SHAFT_BUFFER); // -1 foot below base level
                        }

                        // WALL_TOP_OFFSET controls top offset (positive for above)  
                        var topOffsetParam = shaft.get_Parameter(DB.BuiltInParameter.WALL_TOP_OFFSET);
                        if (topOffsetParam != null && !topOffsetParam.IsReadOnly)
                        {
                            topOffsetParam.Set(SHAFT_BUFFER); // +1 foot above top level
                        }

                        Debug.WriteLine($"Created shaft {shaftId} from {bottomLevel.Name} to {topLevel.Name} with offsets");
                    }
                    catch (Exception paramEx)
                    {
                        Debug.WriteLine($"Warning: Could not set shaft parameters for {shaftId}: {paramEx.Message}");
                    }

                    _createdShafts[shaftId] = shaft;
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating shaft opening {shaftId}: {ex.Message}");
                return CreateDirectShapeShaft(bounds, bottomElevation, topElevation, shaftId);
            }

            return 0;
        }

        private DB.Level FindLevelForOpening(double bottomElevation, double topElevation, bool findBottom)
        {
            // Get all levels and sort by elevation
            var levels = new DB.FilteredElementCollector(_doc)
                .OfClass(typeof(DB.Level))
                .Cast<DB.Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            if (findBottom)
            {
                // For bottom level, find the highest level that's still below our target
                return levels
                    .Where(l => l.Elevation <= bottomElevation + SHAFT_BUFFER)
                    .OrderByDescending(l => l.Elevation)
                    .FirstOrDefault() ?? levels.FirstOrDefault();
            }
            else
            {
                // For top level, find the lowest level that's still above our target  
                return levels
                    .Where(l => l.Elevation >= topElevation - SHAFT_BUFFER)
                    .OrderBy(l => l.Elevation)
                    .FirstOrDefault() ?? levels.LastOrDefault();
            }
        }

        private int CreateDirectShapeShaft(OpeningBounds bounds, double bottomElevation, double topElevation, string shaftId)
        {
            try
            {
                // Create a simple DirectShape as a fallback
                var directShape = DB.DirectShape.CreateElement(_doc, new DB.ElementId(DB.BuiltInCategory.OST_GenericModel));

                // Create solid geometry for the shaft
                var profile = new List<DB.Curve>();

                DB.XYZ p1 = new DB.XYZ(bounds.MinX, bounds.MinY, bottomElevation);
                DB.XYZ p2 = new DB.XYZ(bounds.MaxX, bounds.MinY, bottomElevation);
                DB.XYZ p3 = new DB.XYZ(bounds.MaxX, bounds.MaxY, bottomElevation);
                DB.XYZ p4 = new DB.XYZ(bounds.MinX, bounds.MaxY, bottomElevation);

                profile.Add(DB.Line.CreateBound(p1, p2));
                profile.Add(DB.Line.CreateBound(p2, p3));
                profile.Add(DB.Line.CreateBound(p3, p4));
                profile.Add(DB.Line.CreateBound(p4, p1));

                var curveLoop = DB.CurveLoop.Create(profile);
                var solid = DB.GeometryCreationUtilities.CreateExtrusionGeometry(
                    new List<DB.CurveLoop> { curveLoop },
                    DB.XYZ.BasisZ,
                    topElevation - bottomElevation);

                directShape.SetShape(new DB.GeometryObject[] { solid });
                directShape.Name = $"Shaft_{shaftId}";

                Debug.WriteLine($"Created DirectShape shaft {shaftId} as fallback");
                return 1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating DirectShape shaft {shaftId}: {ex.Message}");
                return 0;
            }
        }
    }
}