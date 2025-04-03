// WallExporter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using RAMDATAACCESSLib;

namespace RAM.Export
{
    public class WallExporter : IRAMExporter
    {
        private IModel _model;

        public WallExporter(IModel model)
        {
            _model = model;
        }

        public void Export(BaseModel model)
        {
            // Group walls by base level
            var wallsByLevel = model.Elements.Walls
                .Where(w => !string.IsNullOrEmpty(w.BaseLevelId) && w.Points.Count >= 2)
                .GroupBy(w => w.BaseLevelId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Map levels to floor types
            var levelToFloorType = new Dictionary<string, string>();
            foreach (var level in model.ModelLayout.Levels)
            {
                if (!string.IsNullOrEmpty(level.FloorTypeId))
                {
                    var floorType = model.ModelLayout.FloorTypes
                        .FirstOrDefault(ft => ft.Id == level.FloorTypeId);

                    if (floorType != null)
                    {
                        levelToFloorType[level.Id] = floorType.Name;
                    }
                }
            }

            // Match floor types to RAM floor types
            IFloorTypes ramFloorTypes = _model.GetFloorTypes();
            var floorTypeMap = new Dictionary<string, IFloorType>();

            for (int i = 0; i < ramFloorTypes.GetCount(); i++)
            {
                IFloorType floorType = ramFloorTypes.GetAt(i);
                floorTypeMap[floorType.strLabel] = floorType;
            }

            // Create mapping of wall property IDs to wall thicknesses
            var wallThicknesses = new Dictionary<string, double>();
            foreach (var wallProp in model.Properties.WallProperties)
            {
                wallThicknesses[wallProp.Id] = wallProp.Thickness;
            }

            // Export walls
            foreach (var levelId in wallsByLevel.Keys)
            {
                // Find corresponding floor type
                if (!levelToFloorType.TryGetValue(levelId, out string floorTypeName) ||
                    !floorTypeMap.TryGetValue(floorTypeName, out IFloorType floorType))
                {
                    Console.WriteLine($"Could not find floor type for level {levelId}");
                    continue;
                }

                // Get layout walls
                ILayoutWalls layoutWalls = floorType.GetLayoutWalls();

                // Export walls for this level
                foreach (var wall in wallsByLevel[levelId])
                {
                    try
                    {
                        // Get wall thickness
                        double thickness = 8.0; // Default 8 inch thickness
                        if (!string.IsNullOrEmpty(wall.PropertiesId) && wallThicknesses.ContainsKey(wall.PropertiesId))
                        {
                            thickness = wallThicknesses[wall.PropertiesId];
                        }

                        // Determine material type
                        EMATERIALTYPES materialType = EMATERIALTYPES.EConcreteMat; // Walls are typically concrete

                        // For each segment of the wall (defined by consecutive points)
                        for (int i = 0; i < wall.Points.Count - 1; i++)
                        {
                            var startPoint = wall.Points[i];
                            var endPoint = wall.Points[i + 1];

                            // Convert coordinates to inches
                            double startX = startPoint.X * 12;
                            double startY = startPoint.Y * 12;
                            double endX = endPoint.X * 12;
                            double endY = endPoint.Y * 12;

                            // Add wall segment
                            layoutWalls.Add(materialType, startX, startY, 0.0, 0.0, endX, endY, 0.0, 0.0, thickness);
                        }

                        // If wall is a closed polygon, connect last point to first point
                        if (wall.Points.Count > 2 &&
                            (wall.Points[0].X != wall.Points[wall.Points.Count - 1].X ||
                             wall.Points[0].Y != wall.Points[wall.Points.Count - 1].Y))
                        {
                            var startPoint = wall.Points[wall.Points.Count - 1];
                            var endPoint = wall.Points[0];

                            // Convert coordinates to inches
                            double startX = startPoint.X * 12;
                            double startY = startPoint.Y * 12;
                            double endX = endPoint.X * 12;
                            double endY = endPoint.Y * 12;

                            // Add final wall segment to close the polygon
                            layoutWalls.Add(materialType, startX, startY, 0.0, 0.0, endX, endY, 0.0, 0.0, thickness);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error exporting wall {wall.Id}: {ex.Message}");
                    }
                }
            }
        }
    }
}