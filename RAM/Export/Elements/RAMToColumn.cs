// ColumnExporter.cs
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
    public class RAMToColumn : IRAMExporter
    {
        private IModel _model;

        public RAMToColumn(IModel model)
        {
            _model = model;
        }

        public void Export(BaseModel model)
        {
            // Group columns by base level
            var columnsByLevel = model.Elements.Columns
                .Where(c => !string.IsNullOrEmpty(c.BaseLevelId))
                .GroupBy(c => c.BaseLevelId)
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

            // Export columns
            foreach (var levelId in columnsByLevel.Keys)
            {
                // Find corresponding floor type
                if (!levelToFloorType.TryGetValue(levelId, out string floorTypeName) ||
                    !floorTypeMap.TryGetValue(floorTypeName, out IFloorType floorType))
                {
                    Console.WriteLine($"Could not find floor type for level {levelId}");
                    continue;
                }

                // Get layout columns
                ILayoutColumns layoutColumns = floorType.GetLayoutColumns();

                // Export columns for this level
                foreach (var column in columnsByLevel[levelId])
                {
                    try
                    {
                        // Determine material type
                        EMATERIALTYPES materialType = EMATERIALTYPES.ESteelMat;
                        if (!string.IsNullOrEmpty(column.FramePropertiesId))
                        {
                            var frameProp = model.Properties.FrameProperties
                                .FirstOrDefault(fp => fp.Id == column.FramePropertiesId);

                            if (frameProp != null && !string.IsNullOrEmpty(frameProp.MaterialId))
                            {
                                var material = model.Properties.Materials
                                    .FirstOrDefault(m => m.Id == frameProp.MaterialId);

                                if (material != null)
                                {
                                    materialType = RAM.Utilities.RAMModelConverter.ConvertMaterialType(material);
                                }
                            }
                        }

                        // Convert coordinates to inches
                        double x = column.StartPoint.X * 12;
                        double y = column.StartPoint.Y * 12;

                        // Determine column heights
                        double baseZ = 0;
                        double topZ = 0;

                        // If top level is specified, determine top Z coordinate
                        if (!string.IsNullOrEmpty(column.TopLevelId))
                        {
                            var topLevel = model.ModelLayout.Levels
                                .FirstOrDefault(l => l.Id == column.TopLevelId);

                            if (topLevel != null)
                            {
                                topZ = topLevel.Elevation * 12;
                            }
                        }

                        // Add column to layout
                        layoutColumns.Add(materialType, x, y, topZ, baseZ);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error exporting column {column.Id}: {ex.Message}");
                    }
                }
            }
        }
    }
}