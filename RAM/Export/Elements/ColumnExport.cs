using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Elements;
using Core.Models.Geometry;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Export.Elements
{
    public class ColumnExport
    {
        private IModel _model;
        private string _lengthUnit;

        public ColumnExport(IModel model, string lengthUnit = "inches")
        {
            _model = model;
            _lengthUnit = lengthUnit;
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

                    // Find the corresponding level ID for this story using the mapping utility
                    string storyUid = ramStory.lUID.ToString();
                    string topLevelId = ModelMappingUtility.GetLevelIdForStoryUid(storyUid);

                    if (string.IsNullOrEmpty(topLevelId))
                    {
                        Console.WriteLine($"No level mapping found for story {ramStory.strLabel} (UID: {storyUid})");
                        continue;
                    }

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

                        // Find the level below for the base level ID using the mapping utility
                        string baseLevelId = ModelMappingUtility.GetBaseLevelIdForTopLevelId(topLevelId, _model);

                        // If no base level found, use the ground level
                        if (string.IsNullOrEmpty(baseLevelId))
                        {
                            baseLevelId = ModelMappingUtility.GetGroundLevelId();
                            if (string.IsNullOrEmpty(baseLevelId))
                            {
                                Console.WriteLine($"No base level found for column, using top level as fallback");
                                baseLevelId = topLevelId;
                            }
                        }

                        // Use the mapping utility to find the frame property ID
                        string framePropertiesId = ModelMappingUtility.GetFramePropertyIdForSectionLabel(ramColumn.strSectionLabel);

                        // Create column from RAM data
                        Column column = new Column
                        {
                            Id = IdGenerator.Generate(IdGenerator.Elements.COLUMN),
                            StartPoint = new Point2D(
                                UnitConversionUtils.ConvertFromInches(pt1.dXLoc, "inches"),
                                UnitConversionUtils.ConvertFromInches(pt1.dYLoc, "inches")
                            ),
                            EndPoint = new Point2D(
                                UnitConversionUtils.ConvertFromInches(pt2.dXLoc, "inches"),
                                UnitConversionUtils.ConvertFromInches(pt2.dYLoc, "inches")
                            ),
                            BaseLevelId = baseLevelId,
                            TopLevelId = topLevelId,
                            FramePropertiesId = framePropertiesId,
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
    }
}