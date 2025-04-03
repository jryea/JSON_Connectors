// StoryExporter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Core.Models;
using Core.Models.ModelLayout;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Export
{
    public class StoryExporter : IRAMExporter
    {
        private IModel _model;

        public StoryExporter(IModel model)
        {
            _model = model;
        }

        public void Export(BaseModel model)
        {
            // Sort levels by elevation (ascending)
            var sortedLevels = model.ModelLayout.Levels.OrderBy(l => l.Elevation).ToList();

            // Get floor types
            IFloorTypes floorTypes = _model.GetFloorTypes();
            Dictionary<string, IFloorType> floorTypeMap = new Dictionary<string, IFloorType>();

            for (int i = 0; i < floorTypes.GetCount(); i++)
            {
                IFloorType floorType = floorTypes.GetAt(i);
                floorTypeMap[floorType.strLabel] = floorType;
            }

            // Get stories
            IStories stories = _model.GetStories();

            double totalHeight = 0;

            for (int i = 0; i < sortedLevels.Count; i++)
            {
                Level level = sortedLevels[i];

                // Calculate story height
                double elevation = level.Elevation * 12; // Convert to inches
                double storyHeight = elevation - totalHeight;
                totalHeight = elevation;

                // Find corresponding floor type
                IFloorType floorType = null;
                if (!string.IsNullOrEmpty(level.FloorTypeId))
                {
                    var floorTypeName = model.ModelLayout.FloorTypes
                        .FirstOrDefault(ft => ft.Id == level.FloorTypeId)?.Name;

                    if (floorTypeName != null && floorTypeMap.ContainsKey(floorTypeName))
                    {
                        floorType = floorTypeMap[floorTypeName];
                    }
                }

                // If floor type not found, use the first available
                if (floorType == null && floorTypes.GetCount() > 0)
                {
                    floorType = floorTypes.GetAt(0);
                }

                // Add story
                if (floorType != null)
                {
                    try
                    {
                        stories.Add(floorType.lUID, level.Name, storyHeight);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error exporting story {level.Name}: {ex.Message}");
                    }
                }
            }
        }
    }
}