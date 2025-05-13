using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Models.Properties;
using Core.Models.ModelLayout;
using Core.Models.Loads;

namespace Core.Utilities
{
    public static class ModelDuplicateHandler
    {
        // Tolerance for coordinate comparison
        private const double CoordinateTolerance = 1e-6;

        // Class to hold mapping records and deduplicated collections
        public class DeduplicationResult<T> where T : IIdentifiable
        {
            public List<T> UniqueItems { get; set; } = new List<T>();
            public Dictionary<string, string> IdMapping { get; set; } = new Dictionary<string, string>();
        }

        #region Element Duplicate Removal

        // Removes duplicate beams and returns mapping of removed -> retained IDs
        public static DeduplicationResult<Beam> RemoveDuplicateBeams(IEnumerable<Beam> beams)
        {
            var result = new DeduplicationResult<Beam>();
            if (beams == null) return result;

            var uniqueBeams = new List<Beam>();
            var processedKeys = new Dictionary<string, Beam>();

            foreach (var beam in beams)
            {
                if (beam == null || beam.StartPoint == null || beam.EndPoint == null)
                    continue;

                string beamKey = GetBeamGeometricKey(beam);

                if (!processedKeys.ContainsKey(beamKey))
                {
                    processedKeys[beamKey] = beam;
                    uniqueBeams.Add(beam);
                }
                else
                {
                    // Record mapping from this duplicate to the retained beam
                    result.IdMapping[beam.Id] = processedKeys[beamKey].Id;
                }
            }

            result.UniqueItems = uniqueBeams;
            return result;
        }

        // Similar pattern for other element types...
        public static DeduplicationResult<Column> RemoveDuplicateColumns(IEnumerable<Column> columns)
        {
            var result = new DeduplicationResult<Column>();
            if (columns == null) return result;

            var uniqueColumns = new List<Column>();
            var processedKeys = new Dictionary<string, Column>();

            foreach (var column in columns)
            {
                if (column == null || column.StartPoint == null)
                    continue;

                string columnKey = GetColumnGeometricKey(column);

                if (!processedKeys.ContainsKey(columnKey))
                {
                    processedKeys[columnKey] = column;
                    uniqueColumns.Add(column);
                }
                else
                {
                    result.IdMapping[column.Id] = processedKeys[columnKey].Id;
                }
            }

            result.UniqueItems = uniqueColumns;
            return result;
        }

        public static DeduplicationResult<Wall> RemoveDuplicateWalls(IEnumerable<Wall> walls)
        {
            var result = new DeduplicationResult<Wall>();
            if (walls == null) return result;

            var uniqueWalls = new List<Wall>();
            var processedKeys = new Dictionary<string, Wall>();

            foreach (var wall in walls)
            {
                if (wall == null || wall.Points == null || wall.Points.Count < 2)
                    continue;

                string wallKey = GetWallGeometricKey(wall);

                if (!processedKeys.ContainsKey(wallKey))
                {
                    processedKeys[wallKey] = wall;
                    uniqueWalls.Add(wall);
                }
                else
                {
                    result.IdMapping[wall.Id] = processedKeys[wallKey].Id;
                }
            }

            result.UniqueItems = uniqueWalls;
            return result;
        }

        public static DeduplicationResult<Floor> RemoveDuplicateFloors(IEnumerable<Floor> floors)
        {
            var result = new DeduplicationResult<Floor>();
            if (floors == null) return result;

            var uniqueFloors = new List<Floor>();
            var processedKeys = new Dictionary<string, Floor>();

            foreach (var floor in floors)
            {
                if (floor == null || floor.Points == null || floor.Points.Count < 3)
                    continue;

                string floorKey = GetFloorGeometricKey(floor);

                if (!processedKeys.ContainsKey(floorKey))
                {
                    processedKeys[floorKey] = floor;
                    uniqueFloors.Add(floor);
                }
                else
                {
                    result.IdMapping[floor.Id] = processedKeys[floorKey].Id;
                }
            }

            result.UniqueItems = uniqueFloors;
            return result;
        }

        public static DeduplicationResult<Brace> RemoveDuplicateBraces(IEnumerable<Brace> braces)
        {
            var result = new DeduplicationResult<Brace>();
            if (braces == null) return result;

            var uniqueBraces = new List<Brace>();
            var processedKeys = new Dictionary<string, Brace>();

            foreach (var brace in braces)
            {
                if (brace == null || brace.StartPoint == null || brace.EndPoint == null)
                    continue;

                string braceKey = GetBraceGeometricKey(brace);

                if (!processedKeys.ContainsKey(braceKey))
                {
                    processedKeys[braceKey] = brace;
                    uniqueBraces.Add(brace);
                }
                else
                {
                    result.IdMapping[brace.Id] = processedKeys[braceKey].Id;
                }
            }

            result.UniqueItems = uniqueBraces;
            return result;
        }

        public static DeduplicationResult<IsolatedFooting> RemoveDuplicateIsolatedFootings(IEnumerable<IsolatedFooting> footings)
        {
            var result = new DeduplicationResult<IsolatedFooting>();
            if (footings == null) return result;

            var uniqueFootings = new List<IsolatedFooting>();
            var processedKeys = new Dictionary<string, IsolatedFooting>();

            foreach (var footing in footings)
            {
                if (footing == null || footing.Point == null)
                    continue;

                string footingKey = GetIsolatedFootingGeometricKey(footing);

                if (!processedKeys.ContainsKey(footingKey))
                {
                    processedKeys[footingKey] = footing;
                    uniqueFootings.Add(footing);
                }
                else
                {
                    result.IdMapping[footing.Id] = processedKeys[footingKey].Id;
                }
            }

            result.UniqueItems = uniqueFootings;
            return result;
        }

        #endregion

        #region Property Duplicate Removal

        public static DeduplicationResult<Material> RemoveDuplicateMaterials(IEnumerable<Material> materials)
        {
            var result = new DeduplicationResult<Material>();
            if (materials == null) return result;

            var uniqueMaterials = new List<Material>();
            var processedNames = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

            foreach (var material in materials)
            {
                if (material == null || string.IsNullOrEmpty(material.Name))
                    continue;

                if (!processedNames.ContainsKey(material.Name))
                {
                    processedNames[material.Name] = material;
                    uniqueMaterials.Add(material);
                }
                else
                {
                    result.IdMapping[material.Id] = processedNames[material.Name].Id;
                }
            }

            result.UniqueItems = uniqueMaterials;
            return result;
        }

        public static DeduplicationResult<WallProperties> RemoveDuplicateWallProperties(IEnumerable<WallProperties> properties)
        {
            var result = new DeduplicationResult<WallProperties>();
            if (properties == null) return result;

            var uniqueProperties = new List<WallProperties>();
            var processedNames = new Dictionary<string, WallProperties>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in properties)
            {
                if (property == null || string.IsNullOrEmpty(property.Name))
                    continue;

                if (!processedNames.ContainsKey(property.Name))
                {
                    processedNames[property.Name] = property;
                    uniqueProperties.Add(property);
                }
                else
                {
                    result.IdMapping[property.Id] = processedNames[property.Name].Id;
                }
            }

            result.UniqueItems = uniqueProperties;
            return result;
        }

        public static DeduplicationResult<FloorProperties> RemoveDuplicateFloorProperties(IEnumerable<FloorProperties> properties)
        {
            var result = new DeduplicationResult<FloorProperties>();
            if (properties == null) return result;

            var uniqueProperties = new List<FloorProperties>();
            var processedNames = new Dictionary<string, FloorProperties>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in properties)
            {
                if (property == null || string.IsNullOrEmpty(property.Name))
                    continue;

                if (!processedNames.ContainsKey(property.Name))
                {
                    processedNames[property.Name] = property;
                    uniqueProperties.Add(property);
                }
                else
                {
                    result.IdMapping[property.Id] = processedNames[property.Name].Id;
                }
            }

            result.UniqueItems = uniqueProperties;
            return result;
        }

        public static DeduplicationResult<FrameProperties> RemoveDuplicateFrameProperties(IEnumerable<FrameProperties> properties)
        {
            var result = new DeduplicationResult<FrameProperties>();
            if (properties == null) return result;

            var uniqueProperties = new List<FrameProperties>();
            var processedNames = new Dictionary<string, FrameProperties>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in properties)
            {
                if (property == null || string.IsNullOrEmpty(property.Name))
                    continue;

                if (!processedNames.ContainsKey(property.Name))
                {
                    processedNames[property.Name] = property;
                    uniqueProperties.Add(property);
                }
                else
                {
                    result.IdMapping[property.Id] = processedNames[property.Name].Id;
                }
            }

            result.UniqueItems = uniqueProperties;
            return result;
        }

        public static DeduplicationResult<Diaphragm> RemoveDuplicateDiaphragms(IEnumerable<Diaphragm> diaphragms)
        {
            var result = new DeduplicationResult<Diaphragm>();
            if (diaphragms == null) return result;

            var uniqueDiaphragms = new List<Diaphragm>();
            var processedNames = new Dictionary<string, Diaphragm>(StringComparer.OrdinalIgnoreCase);

            foreach (var diaphragm in diaphragms)
            {
                if (diaphragm == null || string.IsNullOrEmpty(diaphragm.Name))
                    continue;

                if (!processedNames.ContainsKey(diaphragm.Name))
                {
                    processedNames[diaphragm.Name] = diaphragm;
                    uniqueDiaphragms.Add(diaphragm);
                }
                else
                {
                    result.IdMapping[diaphragm.Id] = processedNames[diaphragm.Name].Id;
                }
            }

            result.UniqueItems = uniqueDiaphragms;
            return result;
        }

        #endregion

        #region Geometric Key Generation
        // These methods remain the same as before
        private static string GetBeamGeometricKey(Beam beam)
        {
            double x1 = beam.StartPoint.X;
            double y1 = beam.StartPoint.Y;
            double x2 = beam.EndPoint.X;
            double y2 = beam.EndPoint.Y;

            if ((Math.Abs(x2 - x1) > Math.Abs(y2 - y1) && x2 < x1) ||
                (Math.Abs(y2 - y1) >= Math.Abs(x2 - x1) && y2 < y1))
            {
                double tempX = x1;
                double tempY = y1;
                x1 = x2;
                y1 = y2;
                x2 = tempX;
                y2 = tempY;
            }

            return $"{Math.Round(x1, 6)},{Math.Round(y1, 6)}_" +
                   $"{Math.Round(x2, 6)},{Math.Round(y2, 6)}_" +
                   $"{beam.LevelId ?? ""}";
        }

        private static string GetColumnGeometricKey(Column column)
        {
            return $"{Math.Round(column.StartPoint.X, 6)},{Math.Round(column.StartPoint.Y, 6)}_" +
                   $"{column.BaseLevelId ?? ""}_" +
                   $"{column.TopLevelId ?? ""}";
        }

        private static string GetWallGeometricKey(Wall wall)
        {
            if (wall.Points.Count == 2)
            {
                double x1 = wall.Points[0].X;
                double y1 = wall.Points[0].Y;
                double x2 = wall.Points[1].X;
                double y2 = wall.Points[1].Y;

                if ((Math.Abs(x2 - x1) > Math.Abs(y2 - y1) && x2 < x1) ||
                    (Math.Abs(y2 - y1) >= Math.Abs(x2 - x1) && y2 < y1))
                {
                    return $"{Math.Round(x2, 6)},{Math.Round(y2, 6)}_" +
                           $"{Math.Round(x1, 6)},{Math.Round(y1, 6)}_" +
                           $"{wall.BaseLevelId ?? ""}_" +
                           $"{wall.TopLevelId ?? ""}";
                }

                return $"{Math.Round(x1, 6)},{Math.Round(y1, 6)}_" +
                       $"{Math.Round(x2, 6)},{Math.Round(y2, 6)}_" +
                       $"{wall.BaseLevelId ?? ""}_" +
                       $"{wall.TopLevelId ?? ""}";
            }

            var pointStrings = new List<string>();
            foreach (var point in wall.Points)
            {
                pointStrings.Add($"{Math.Round(point.X, 6)},{Math.Round(point.Y, 6)}");
            }

            pointStrings.Sort();

            return string.Join("_", pointStrings) +
                   $"_{wall.BaseLevelId ?? ""}_" +
                   $"{wall.TopLevelId ?? ""}";
        }

        private static string GetFloorGeometricKey(Floor floor)
        {
            var pointStrings = new List<string>();
            foreach (var point in floor.Points)
            {
                pointStrings.Add($"{Math.Round(point.X, 6)},{Math.Round(point.Y, 6)}");
            }

            pointStrings.Sort();

            return string.Join("_", pointStrings) +
                   $"_{floor.LevelId ?? ""}";
        }

        private static string GetBraceGeometricKey(Brace brace)
        {
            double x1 = brace.StartPoint.X;
            double y1 = brace.StartPoint.Y;
            double x2 = brace.EndPoint.X;
            double y2 = brace.EndPoint.Y;

            if ((Math.Abs(x2 - x1) > Math.Abs(y2 - y1) && x2 < x1) ||
                (Math.Abs(y2 - y1) >= Math.Abs(x2 - x1) && y2 < y1))
            {
                double tempX = x1;
                double tempY = y1;
                x1 = x2;
                y1 = y2;
                x2 = tempX;
                y2 = tempY;
            }

            return $"{Math.Round(x1, 6)},{Math.Round(y1, 6)}_" +
                   $"{Math.Round(x2, 6)},{Math.Round(y2, 6)}_" +
                   $"{brace.BaseLevelId ?? ""}_" +
                   $"{brace.TopLevelId ?? ""}";
        }

        private static string GetIsolatedFootingGeometricKey(IsolatedFooting footing)
        {
            return $"{Math.Round(footing.Point.X, 6)},{Math.Round(footing.Point.Y, 6)},{Math.Round(footing.Point.Z, 6)}_" +
                   $"{footing.LevelId ?? ""}";
        }
        #endregion

        // Determines if two points are equal within tolerance
        public static bool ArePointsEqual(Point2D p1, Point2D p2)
        {
            if (p1 == null || p2 == null)
                return false;

            return Math.Abs(p1.X - p2.X) < CoordinateTolerance &&
                   Math.Abs(p1.Y - p2.Y) < CoordinateTolerance;
        }

        #region Model Processing

        // Process a complete model, removing duplicates and updating references
        public static void RemoveDuplicates(BaseModel model)
        {
            if (model == null) return;

            // Create a composite ID mapping that will track all duplicate -> original mappings
            Dictionary<string, string> idMappings = new Dictionary<string, string>();

            // Process properties first (since elements reference properties)
            if (model.Properties != null)
            {
                var materialResult = RemoveDuplicateMaterials(model.Properties.Materials);
                model.Properties.Materials = materialResult.UniqueItems;
                AddMappings(idMappings, materialResult.IdMapping);

                var wallPropsResult = RemoveDuplicateWallProperties(model.Properties.WallProperties);
                model.Properties.WallProperties = wallPropsResult.UniqueItems;
                AddMappings(idMappings, wallPropsResult.IdMapping);

                var floorPropsResult = RemoveDuplicateFloorProperties(model.Properties.FloorProperties);
                model.Properties.FloorProperties = floorPropsResult.UniqueItems;
                AddMappings(idMappings, floorPropsResult.IdMapping);

                var framePropsResult = RemoveDuplicateFrameProperties(model.Properties.FrameProperties);
                model.Properties.FrameProperties = framePropsResult.UniqueItems;
                AddMappings(idMappings, framePropsResult.IdMapping);

                var diaphragmResult = RemoveDuplicateDiaphragms(model.Properties.Diaphragms);
                model.Properties.Diaphragms = diaphragmResult.UniqueItems;
                AddMappings(idMappings, diaphragmResult.IdMapping);
            }

            // Process elements
            if (model.Elements != null)
            {
                var beamResult = RemoveDuplicateBeams(model.Elements.Beams);
                model.Elements.Beams = beamResult.UniqueItems;
                AddMappings(idMappings, beamResult.IdMapping);

                var columnResult = RemoveDuplicateColumns(model.Elements.Columns);
                model.Elements.Columns = columnResult.UniqueItems;
                AddMappings(idMappings, columnResult.IdMapping);

                var wallResult = RemoveDuplicateWalls(model.Elements.Walls);
                model.Elements.Walls = wallResult.UniqueItems;
                AddMappings(idMappings, wallResult.IdMapping);

                var floorResult = RemoveDuplicateFloors(model.Elements.Floors);
                model.Elements.Floors = floorResult.UniqueItems;
                AddMappings(idMappings, floorResult.IdMapping);

                var braceResult = RemoveDuplicateBraces(model.Elements.Braces);
                model.Elements.Braces = braceResult.UniqueItems;
                AddMappings(idMappings, braceResult.IdMapping);

                var footingResult = RemoveDuplicateIsolatedFootings(model.Elements.IsolatedFootings);
                model.Elements.IsolatedFootings = footingResult.UniqueItems;
                AddMappings(idMappings, footingResult.IdMapping);
            }

            // Now update all references using the ID mappings
            UpdateAllReferences(model, idMappings);
        }

        // Helper to combine mapping dictionaries
        private static void AddMappings(Dictionary<string, string> allMappings, Dictionary<string, string> newMappings)
        {
            foreach (var mapping in newMappings)
            {
                allMappings[mapping.Key] = mapping.Value;
            }
        }

        // Update all references throughout the model
        private static void UpdateAllReferences(BaseModel model, Dictionary<string, string> idMappings)
        {
            if (model == null || idMappings.Count == 0) return;

            // Update element references
            if (model.Elements != null)
            {
                // Update beam references
                if (model.Elements.Beams != null)
                {
                    foreach (var beam in model.Elements.Beams)
                    {
                        UpdateElementReferences(beam, idMappings);
                    }
                }

                // Update column references
                if (model.Elements.Columns != null)
                {
                    foreach (var column in model.Elements.Columns)
                    {
                        UpdateElementReferences(column, idMappings);
                    }
                }

                // Update wall references
                if (model.Elements.Walls != null)
                {
                    foreach (var wall in model.Elements.Walls)
                    {
                        UpdateElementReferences(wall, idMappings);
                    }
                }

                // Update floor references
                if (model.Elements.Floors != null)
                {
                    foreach (var floor in model.Elements.Floors)
                    {
                        UpdateElementReferences(floor, idMappings);
                    }
                }

                // Update brace references
                if (model.Elements.Braces != null)
                {
                    foreach (var brace in model.Elements.Braces)
                    {
                        UpdateElementReferences(brace, idMappings);
                    }
                }

                // Update isolated footing references
                if (model.Elements.IsolatedFootings != null)
                {
                    foreach (var footing in model.Elements.IsolatedFootings)
                    {
                        UpdateElementReferences(footing, idMappings);
                    }
                }
            }

            // Update property references
            if (model.Properties != null)
            {
                // Update wall property references
                if (model.Properties.WallProperties != null)
                {
                    foreach (var prop in model.Properties.WallProperties)
                    {
                        UpdatePropertyReferences(prop, idMappings);
                    }
                }

                // Update floor property references
                if (model.Properties.FloorProperties != null)
                {
                    foreach (var prop in model.Properties.FloorProperties)
                    {
                        UpdatePropertyReferences(prop, idMappings);
                    }
                }

                // Update frame property references
                if (model.Properties.FrameProperties != null)
                {
                    foreach (var prop in model.Properties.FrameProperties)
                    {
                        UpdatePropertyReferences(prop, idMappings);
                    }
                }
            }

            // Update model layout references
            if (model.ModelLayout != null)
            {
                // Update level references
                if (model.ModelLayout.Levels != null)
                {
                    foreach (var level in model.ModelLayout.Levels)
                    {
                        UpdateLevelReferences(level, idMappings);
                    }
                }
            }

            // Update load references
            if (model.Loads != null)
            {
                // Update surface load references
                if (model.Loads.SurfaceLoads != null)
                {
                    foreach (var load in model.Loads.SurfaceLoads)
                    {
                        UpdateLoadReferences(load, idMappings);
                    }
                }

                // Update load combination references
                if (model.Loads.LoadCombinations != null)
                {
                    foreach (var combo in model.Loads.LoadCombinations)
                    {
                        UpdateLoadCombinationReferences(combo, idMappings);
                    }
                }
            }
        }

        // Helper methods to update various element types
        private static void UpdateElementReferences(Beam beam, Dictionary<string, string> idMappings)
        {
            if (beam == null) return;

            beam.LevelId = UpdateId(beam.LevelId, idMappings);
            beam.FramePropertiesId = UpdateId(beam.FramePropertiesId, idMappings);
        }

        private static void UpdateElementReferences(Column column, Dictionary<string, string> idMappings)
        {
            if (column == null) return;

            column.BaseLevelId = UpdateId(column.BaseLevelId, idMappings);
            column.TopLevelId = UpdateId(column.TopLevelId, idMappings);
            column.FramePropertiesId = UpdateId(column.FramePropertiesId, idMappings);
        }

        private static void UpdateElementReferences(Wall wall, Dictionary<string, string> idMappings)
        {
            if (wall == null) return;

            wall.BaseLevelId = UpdateId(wall.BaseLevelId, idMappings);
            wall.TopLevelId = UpdateId(wall.TopLevelId, idMappings);
            wall.PropertiesId = UpdateId(wall.PropertiesId, idMappings);
            //wall.PierSpandrelId = UpdateId(wall.PierSpandrelId, idMappings);
        }

        private static void UpdateElementReferences(Floor floor, Dictionary<string, string> idMappings)
        {
            if (floor == null) return;

            floor.LevelId = UpdateId(floor.LevelId, idMappings);
            floor.FloorPropertiesId = UpdateId(floor.FloorPropertiesId, idMappings);
            floor.DiaphragmId = UpdateId(floor.DiaphragmId, idMappings);
            floor.SurfaceLoadId = UpdateId(floor.SurfaceLoadId, idMappings);
        }

        private static void UpdateElementReferences(Brace brace, Dictionary<string, string> idMappings)
        {
            if (brace == null) return;

            brace.BaseLevelId = UpdateId(brace.BaseLevelId, idMappings);
            brace.TopLevelId = UpdateId(brace.TopLevelId, idMappings);
            brace.FramePropertiesId = UpdateId(brace.FramePropertiesId, idMappings);
            brace.MaterialId = UpdateId(brace.MaterialId, idMappings);
        }

        private static void UpdateElementReferences(IsolatedFooting footing, Dictionary<string, string> idMappings)
        {
            if (footing == null) return;

            footing.LevelId = UpdateId(footing.LevelId, idMappings);
            footing.MaterialId = UpdateId(footing.MaterialId, idMappings);
        }

        private static void UpdatePropertyReferences(WallProperties prop, Dictionary<string, string> idMappings)
        {
            if (prop == null) return;

            prop.MaterialId = UpdateId(prop.MaterialId, idMappings);
        }

        private static void UpdatePropertyReferences(FloorProperties prop, Dictionary<string, string> idMappings)
        {
            if (prop == null) return;

            prop.MaterialId = UpdateId(prop.MaterialId, idMappings);
        }

        private static void UpdatePropertyReferences(FrameProperties prop, Dictionary<string, string> idMappings)
        {
            if (prop == null) return;

            prop.MaterialId = UpdateId(prop.MaterialId, idMappings);
        }

        private static void UpdateLevelReferences(Level level, Dictionary<string, string> idMappings)
        {
            if (level == null) return;

            level.FloorTypeId = UpdateId(level.FloorTypeId, idMappings);
        }

        private static void UpdateLoadReferences(SurfaceLoad load, Dictionary<string, string> idMappings)
        {
            if (load == null) return;

            load.DeadLoadId = UpdateId(load.DeadLoadId, idMappings);
            load.LiveLoadId = UpdateId(load.LiveLoadId, idMappings);
            load.LayoutTypeId = UpdateId(load.LayoutTypeId, idMappings);
        }

        private static void UpdateLoadCombinationReferences(LoadCombination combo, Dictionary<string, string> idMappings)
        {
            if (combo == null || combo.LoadDefinitionIds == null) return;

            for (int i = 0; i < combo.LoadDefinitionIds.Count; i++)
            {
                string id = combo.LoadDefinitionIds[i];
                if (idMappings.TryGetValue(id, out string newId))
                {
                    combo.LoadDefinitionIds[i] = newId;
                }
            }
        }

        // Helper method to update an ID if it's mapped
        private static string UpdateId(string id, Dictionary<string, string> idMappings)
        {
            if (string.IsNullOrEmpty(id)) return id;

            // If this ID is mapped, return the mapped ID
            if (idMappings.TryGetValue(id, out string newId))
            {
                return newId;
            }

            // Otherwise return the original ID
            return id;
        }
        #endregion
    }
}