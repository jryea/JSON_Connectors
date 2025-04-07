using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Models.ModelLayout;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Export.Elements
{
    public class ColumnExport
    {
        private IModel _model;
        private string _lengthUnit;
        private Dictionary<string, string> _levelMappings = new Dictionary<string, string>();
        private Dictionary<string, string> _framePropMappings = new Dictionary<string, string>();

        public ColumnExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public void SetLevelMappings(Dictionary<string, string> levelMappings)
        {
            _levelMappings = levelMappings ?? new Dictionary<string, string>();
        }

        public void SetFramePropertyMappings(Dictionary<string, string> framePropMappings)
        {
            _framePropMappings = framePropMappings ?? new Dictionary<string, string>();
        }

        public List<Column> Export()
        {
            var columns = new List<Column>();

            try
            {
                // Get all floor types from RAM
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();
                if (ramFloorTypes == null || ramFloorTypes.GetCount() == 0)
                    return columns;

                // Find base level and top level
                string baseLevelId = _levelMappings.Values.FirstOrDefault();
                string topLevelId = _levelMappings.Values.LastOrDefault();

                // Create level ID list in ascending order for mapping column spans
                var levelIds = new List<string>();
                if (_levelMappings.Count > 0)
                {
                    // For simplicity, we're assuming the values are already added in order
                    // In a real implementation, you'd need to sort by level elevation
                    levelIds.AddRange(_levelMappings.Values);
                }

                // Process each floor type
                for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                {
                    IFloorType floorType = ramFloorTypes.GetAt(i);
                    if (floorType == null)
                        continue;

                    // Find the corresponding level ID for this floor type
                    string currentLevelId = FindLevelIdForFloorType(floorType);
                    if (string.IsNullOrEmpty(currentLevelId))
                        continue;

                    // Get layout columns for this floor type
                    ILayoutColumns layoutColumns = floorType.GetLayoutColumns();
                    if (layoutColumns == null)
                        continue;

                    // Process each layout column
                    for (int j = 0; j < layoutColumns.GetCount(); j++)
                    {
                        ILayoutColumn layoutColumn = layoutColumns.GetAt(j);
                        if (layoutColumn == null)
                            continue;

                        // Determine column base and top levels
                        // For simplicity, we'll use current level as top and previous level as base
                        // In a real implementation, you would need to determine the actual column spans
                        int levelIndex = levelIds.IndexOf(currentLevelId);
                        string columnBaseLevelId = levelIndex > 0 ? levelIds[levelIndex - 1] : currentLevelId;
                        string columnTopLevelId = currentLevelId;

                        // Create points for column (both have same X, Y but different elevations)
                        Point2D point = new Point2D(
                            ConvertFromInches(layoutColumn.dXStart),
                            ConvertFromInches(layoutColumn.dYStart)
                        );

                        // Create column from RAM data
                        Column column = new Column
                        {
                            Id = IdGenerator.Generate(IdGenerator.Elements.COLUMN),
                            StartPoint = point,  // Start point (base)
                            EndPoint = point,    // End point (top) - same X,Y as start
                            BaseLevelId = columnBaseLevelId,
                            TopLevelId = columnTopLevelId,
                            FramePropertiesId = FindFramePropertiesId(layoutColumn.strSectionLabel),
                            IsLateral = GetIsLateral(layoutColumn)
                        };

                        columns.Add(column);
                    }
                }

                return columns;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting columns from RAM: {ex.Message}");
                return columns;
            }
        }

        private string FindLevelIdForFloorType(IFloorType floorType)
        {
            // Try to find direct mapping by floor type UID
            string key = $"FloorType_{floorType.lUID}";
            if (_levelMappings.TryGetValue(key, out string levelId))
                return levelId;

            // If not found, try by floor type name
            if (_levelMappings.TryGetValue(floorType.strLabel, out levelId))
                return levelId;

            // Return first level ID as fallback
            return _levelMappings.Values.FirstOrDefault();
        }

        private string FindFramePropertiesId(string sectionName)
        {
            if (string.IsNullOrEmpty(sectionName))
                return null;

            // Try to find direct mapping by section name
            if (_framePropMappings.TryGetValue(sectionName, out string framePropsId))
                return framePropsId;

            // Return null if not found
            return null;
        }

        private double ConvertFromInches(double inches)
        {
            switch (_lengthUnit.ToLower())
            {
                case "feet":
                    return inches / 12.0;
                case "millimeters":
                    return inches * 25.4;
                case "centimeters":
                    return inches * 2.54;
                case "meters":
                    return inches * 0.0254;
                case "inches":
                default:
                    return inches;
            }
        }
    }
}