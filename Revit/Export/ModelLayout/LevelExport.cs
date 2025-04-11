using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using Core.Models.ModelLayout;
using Revit.Utilities;
using System.Diagnostics;

namespace Revit.Export.ModelLayout
{
    public class LevelExport
    {
        private readonly DB.Document _doc;

        public LevelExport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Export(List<Level> levels)
        {
            int count = 0;

            // Get all levels from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.Level> revitLevels = collector.OfClass(typeof(DB.Level))
                .Cast<DB.Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            if (revitLevels.Count == 0)
                return 0;

            // Find levels that are referenced by structural elements
            HashSet<DB.ElementId> referencedLevelIds = GetReferencedLevelIds();

            Debug.WriteLine($"Found {referencedLevelIds.Count} levels referenced by structural elements");

            // If no referenced levels were found, export at least one level
            if (referencedLevelIds.Count == 0)
            {
                // Export at least the base level
                DB.Level baseLevel = revitLevels.OrderBy(l => l.Elevation).First();
                ExportLevel(baseLevel, levels);
                count++;

                Debug.WriteLine($"No referenced levels found, exporting base level: {baseLevel.Name}");
                return count;
            }

            // Export only levels that are referenced
            foreach (var revitLevel in revitLevels)
            {
                if (referencedLevelIds.Contains(revitLevel.Id))
                {
                    ExportLevel(revitLevel, levels);
                    count++;

                    Debug.WriteLine($"Exported referenced level: {revitLevel.Name}, Elevation: {revitLevel.Elevation}");
                }
            }

            return count;
        }

        private HashSet<DB.ElementId> GetReferencedLevelIds()
        {
            HashSet<DB.ElementId> referencedLevelIds = new HashSet<DB.ElementId>();

            // Check structural columns
            CollectLevelReferences(DB.BuiltInCategory.OST_StructuralColumns, referencedLevelIds);

            // Check structural framing (beams, braces)
            CollectLevelReferences(DB.BuiltInCategory.OST_StructuralFraming, referencedLevelIds);

            // Check structural floors
            CollectFloorLevelReferences(referencedLevelIds);

            // Check structural walls
            CollectWallLevelReferences(referencedLevelIds);

            return referencedLevelIds;
        }

        private void CollectLevelReferences(DB.BuiltInCategory category, HashSet<DB.ElementId> levelIds)
        {
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.Element> elements = collector.OfCategory(category)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (var element in elements)
            {
                try
                {
                    // Get base level
                    DB.Parameter baseLevelParam = element.get_Parameter(DB.BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
                    if (baseLevelParam != null && baseLevelParam.HasValue &&
                        baseLevelParam.StorageType == DB.StorageType.ElementId)
                    {
                        DB.ElementId levelId = baseLevelParam.AsElementId();
                        if (levelId != DB.ElementId.InvalidElementId)
                        {
                            levelIds.Add(levelId);
                        }
                    }

                    // Get top level
                    DB.Parameter topLevelParam = element.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                    if (topLevelParam != null && topLevelParam.HasValue &&
                        topLevelParam.StorageType == DB.StorageType.ElementId)
                    {
                        DB.ElementId levelId = topLevelParam.AsElementId();
                        if (levelId != DB.ElementId.InvalidElementId)
                        {
                            levelIds.Add(levelId);
                        }
                    }

                    // Get instance reference level
                    DB.Parameter refLevelParam = element.get_Parameter(DB.BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
                    if (refLevelParam != null && refLevelParam.HasValue &&
                        refLevelParam.StorageType == DB.StorageType.ElementId)
                    {
                        DB.ElementId levelId = refLevelParam.AsElementId();
                        if (levelId != DB.ElementId.InvalidElementId)
                        {
                            levelIds.Add(levelId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing element {element.Id}: {ex.Message}");
                }
            }
        }

        private void CollectFloorLevelReferences(HashSet<DB.ElementId> levelIds)
        {
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.Floor> floors = collector.OfClass(typeof(DB.Floor))
                .WhereElementIsNotElementType()
                .Cast<DB.Floor>()
                .Where(f => {
                    // Check if floor is structural
                    DB.Parameter isStructuralParam = f.get_Parameter(DB.BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
                    return isStructuralParam != null && isStructuralParam.AsInteger() != 0;
                })
                .ToList();

            foreach (var floor in floors)
            {
                try
                {
                    // Get level ID
                    DB.ElementId levelId = floor.LevelId;
                    if (levelId != DB.ElementId.InvalidElementId)
                    {
                        levelIds.Add(levelId);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing floor {floor.Id}: {ex.Message}");
                }
            }
        }

        private void CollectWallLevelReferences(HashSet<DB.ElementId> levelIds)
        {
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.Wall> walls = collector.OfClass(typeof(DB.Wall))
                .WhereElementIsNotElementType()
                .Cast<DB.Wall>()
                .Where(w => {
                    // Check if wall is structural
                    DB.Parameter isStructuralParam = w.get_Parameter(DB.BuiltInParameter.WALL_STRUCTURAL_SIGNIFICANT);
                    if (isStructuralParam != null && isStructuralParam.AsInteger() != 0)
                        return true;

                    // Also check WALL_STRUCTURAL_USAGE_PARAM as backup
                    DB.Parameter structuralUsageParam = w.get_Parameter(DB.BuiltInParameter.WALL_STRUCTURAL_USAGE_PARAM);
                    return structuralUsageParam != null && structuralUsageParam.AsInteger() > 0;
                })
                .ToList();

            foreach (var wall in walls)
            {
                try
                {
                    // Get base level ID
                    DB.Parameter baseLevelParam = wall.get_Parameter(DB.BuiltInParameter.WALL_BASE_CONSTRAINT);
                    if (baseLevelParam != null && baseLevelParam.HasValue &&
                        baseLevelParam.StorageType == DB.StorageType.ElementId)
                    {
                        DB.ElementId levelId = baseLevelParam.AsElementId();
                        if (levelId != DB.ElementId.InvalidElementId)
                        {
                            levelIds.Add(levelId);
                        }
                    }

                    // Get top level ID
                    DB.Parameter topLevelParam = wall.get_Parameter(DB.BuiltInParameter.WALL_HEIGHT_TYPE);
                    if (topLevelParam != null && topLevelParam.HasValue &&
                        topLevelParam.StorageType == DB.StorageType.ElementId)
                    {
                        DB.ElementId levelId = topLevelParam.AsElementId();
                        if (levelId != DB.ElementId.InvalidElementId)
                        {
                            levelIds.Add(levelId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing wall {wall.Id}: {ex.Message}");
                }
            }
        }

        private void ExportLevel(DB.Level revitLevel, List<Level> levels)
        {
            try
            {
                // Create level object
                Level level = new Level
                {
                    Name = revitLevel.Name,
                    Elevation = revitLevel.Elevation * 12.0, // Convert feet to inches
                    FloorTypeId = "FT-default" // Default floor type ID, can be updated later
                };

                levels.Add(level);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting level {revitLevel.Id}: {ex.Message}");
            }
        }
    }
}