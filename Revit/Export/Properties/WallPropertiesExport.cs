using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using Core.Models.Properties;
using System.Diagnostics;

namespace Revit.Export.Properties
{
    public class WallPropertiesExport
    {
        private readonly DB.Document _doc;
        private Dictionary<DB.ElementId, string> _materialIdMap = new Dictionary<DB.ElementId, string>();

        public WallPropertiesExport(DB.Document doc) 
        {
            _doc = doc;
            CreateMaterialIdMapping();
        }

        private void CreateMaterialIdMapping()
        {
            // Map Revit material IDs to model material IDs
            foreach (DB.Material material in new DB.FilteredElementCollector(_doc)
                .OfClass(typeof(DB.Material))
                .Cast<DB.Material>())
            {
                if (!string.IsNullOrEmpty(material.Name))
                {
                    string materialId = $"MAT-{material.Name.Replace(" ", "")}";
                    _materialIdMap[material.Id] = materialId;
                }
            }
        }

        public int Export(List<WallProperties> wallProperties)
        {
            int count = 0;
            HashSet<DB.ElementId> structuralWallTypeIds = new HashSet<DB.ElementId>();

            // First, collect wall types that are used by structural walls
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.Wall> walls = collector.OfCategory(DB.BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .Where(w => !(w is DB.DirectShape))
                .Cast<DB.Wall>()
                .Where(w => IsStructuralWall(w))
                .ToList();

            foreach (var wall in walls)
            {
                structuralWallTypeIds.Add(wall.GetTypeId());
            }

            Debug.WriteLine($"Found {structuralWallTypeIds.Count} wall types used by structural walls");

            // Export only wall types used by structural walls
            foreach (var typeId in structuralWallTypeIds)
            {
                try
                {
                    DB.WallType wallType = _doc.GetElement(typeId) as DB.WallType;
                    if (wallType == null)
                        continue;

                    DB.CompoundStructure cs = wallType.GetCompoundStructure();
                    if (cs == null)
                        continue;

                    // Get material ID
                    string materialId = GetMaterialId(cs);
                    if (string.IsNullOrEmpty(materialId))
                        materialId = "MAT-default";

                    // Get thickness in inches
                    double thickness = cs.GetWidth() * 12.0;

                    // Create wall property
                    WallProperties wallProperty = new WallProperties(
                        wallType.Name,
                        materialId,
                        thickness
                    );

                    // Set wall function
                    wallProperty.Function = wallType.Function.ToString();

                    // Set modeling type
                    wallProperty.ModelingType = ShellModelingType.ShellThin;

                    wallProperties.Add(wallProperty);
                    count++;

                    Debug.WriteLine($"Exported wall type: {wallType.Name}, Material: {materialId}, Thickness: {thickness}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error exporting wall type: {ex.Message}");
                }
            }

            return count;
        }

        private bool IsStructuralWall(DB.Wall wall)
        {
            // Check WALL_STRUCTURAL_SIGNIFICANT parameter as specified
            DB.Parameter isStructuralParam = wall.get_Parameter(DB.BuiltInParameter.WALL_STRUCTURAL_SIGNIFICANT);
            if (isStructuralParam != null && isStructuralParam.AsInteger() != 0)
                return true;

            // Also check WALL_STRUCTURAL_USAGE_PARAM as a backup
            DB.Parameter structuralUsageParam = wall.get_Parameter(DB.BuiltInParameter.WALL_STRUCTURAL_USAGE_PARAM);
            return structuralUsageParam != null && structuralUsageParam.AsInteger() > 0;
        }

        private string GetMaterialId(DB.CompoundStructure cs)
        {
            int coreLayer = cs.GetFirstCoreLayerIndex();
            if (coreLayer >= 0)
            {
                DB.ElementId materialId = cs.GetMaterialId(coreLayer);
                if (materialId != DB.ElementId.InvalidElementId && _materialIdMap.ContainsKey(materialId))
                {
                    return _materialIdMap[materialId];
                }
            }
            return null;
        }
    }
}