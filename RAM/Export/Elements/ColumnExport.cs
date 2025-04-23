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
        private Dictionary<string, string> _levelIdToNameMapping = new Dictionary<string, string>();
        private Dictionary<string, string> _nameToLevelIdMapping = new Dictionary<string, string>();
        private Dictionary<string, string> _framePropMappings = new Dictionary<string, string>();

        public ColumnExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
        }

        public void SetLevelMappings(Dictionary<string, string> levelMappings)
        {
            _levelIdToNameMapping = new Dictionary<string, string>();
            _nameToLevelIdMapping = new Dictionary<string, string>();

            foreach (var kvp in levelMappings)
            {
                _levelIdToNameMapping[kvp.Key] = kvp.Value;
                _nameToLevelIdMapping[kvp.Value] = kvp.Key;
            }
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
                    string storyName = ramStory.strLabel;
                    string levelId = FindLevelIdByStoryName(storyName);

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
                        string baseLevelId = FindBaseLevelIdForStory(ramStory);

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
                            IsLateral = (ramColumn.eFramingType == EFRAMETYPE.MemberIsLateral)
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

        private string FindLevelIdByStoryName(string storyName)
        {
            string levelId = null;

            // Try direct mapping first
            if (_nameToLevelIdMapping.TryGetValue(storyName, out levelId))
                return levelId;

            // Try with "Story" prefix removed
            string cleanName = CleanStoryName(storyName);
            if (_nameToLevelIdMapping.TryGetValue(cleanName, out levelId))
                return levelId;

            // Try with "Story" prefix variations
            if (_nameToLevelIdMapping.TryGetValue($"Story {cleanName}", out levelId) ||
                _nameToLevelIdMapping.TryGetValue($"Story{cleanName}", out levelId))
                return levelId;

            // Return null if no mapping found
            return null;
        }

        private string FindBaseLevelIdForStory(IStory story)
        {
            if (story == null)
                return null;

            // Try to find the level below this story
            IStories ramStories = _model.GetStories();
            IStory belowStory = null;
            double maxElevation = double.MinValue;

            for (int i = 0; i < ramStories.GetCount(); i++)
            {
                IStory checkStory = ramStories.GetAt(i);
                if (checkStory != null && checkStory.dElevation < story.dElevation &&
                    checkStory.dElevation > maxElevation)
                {
                    belowStory = checkStory;
                    maxElevation = checkStory.dElevation;
                }
            }

            if (belowStory != null)
            {
                return FindLevelIdByStoryName(belowStory.strLabel);
            }

            // If no level below found, look for level with elevation 0
            foreach (var entry in _nameToLevelIdMapping)
            {
                if (entry.Key.Equals("0", StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }

            // If still not found, use the same level ID as top level
            return FindLevelIdByStoryName(story.strLabel);
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

        // Removes "Story" prefix if present to normalize names
        private string CleanStoryName(string storyName)
        {
            if (storyName.StartsWith("Story ", StringComparison.OrdinalIgnoreCase))
            {
                return storyName.Substring(6).Trim();
            }
            else if (storyName.StartsWith("Story", StringComparison.OrdinalIgnoreCase))
            {
                return storyName.Substring(5).Trim();
            }
            return storyName;
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