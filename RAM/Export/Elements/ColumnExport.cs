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
                // Get all stories from RAM
                IStories ramStories = _model.GetStories();
                if (ramStories == null || ramStories.GetCount() == 0)
                    return columns;

                // Process each story
                for (int i = 0; i < ramStories.GetCount(); i++)
                {
                    IStory ramStory = ramStories.GetAt(i);
                    if (ramStory == null)
                        continue;

                    // Find the corresponding level ID for this story
                    string levelId = ImportHelpers.FindLevelIdForStory(ramStory, _levelMappings);
                    if (string.IsNullOrEmpty(levelId))
                        continue;

                    // Get columns for this story
                    IColumns storyColumns = ramStory.GetColumns();
                    if (storyColumns == null || storyColumns.GetCount() == 0)
                        continue;

                    // Process each column in the story
                    for (int j = 0; j < storyColumns.GetCount(); j++)
                    {
                        IColumn ramColumn = storyColumns.GetAt(j);
                        if (ramColumn == null)
                            continue;

                        // Get column coordinates
                        SCoordinate pt1 = new SCoordinate();
                        SCoordinate pt2 = new SCoordinate();
                        ramColumn.GetEndCoordinates(ref pt1, ref pt2);

                        // Find the level below for the base level ID
                        string baseLevelId = ImportHelpers.FindBaseLevelIdForStory(ramStory, _model, _levelMappings);

                        // Create column from RAM data
                        Column column = new Column
                        {
                            Id = IdGenerator.Generate(IdGenerator.Elements.COLUMN),
                            StartPoint = new Point2D(
                                ConvertFromInches(pt1.dXLoc),
                                ConvertFromInches(pt1.dYLoc)
                            ),
                            EndPoint = new Point2D(
                                ConvertFromInches(pt2.dXLoc),
                                ConvertFromInches(pt2.dYLoc)
                            ),
                            BaseLevelId = baseLevelId,
                            TopLevelId = levelId,
                            FramePropertiesId = FindFramePropertiesId(ramColumn.strSectionLabel),
                            IsLateral = (ramColumn.eFramingType == EFRAMETYPE.MemberIsLateral) // Assuming 1 means lateral
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