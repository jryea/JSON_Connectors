using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using Core.Models.Properties;

namespace Revit.Export.Properties
{
    public class WallPropertiesExport
    {
        private readonly DB.Document _doc;

        public WallPropertiesExport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Export(List<WallProperties> wallProperties)
        {
            int count = 0;

            // Get all wall types from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.WallType> wallTypes = collector.OfClass(typeof(DB.WallType))
                .Cast<DB.WallType>()
                .ToList();

            foreach (var wallType in wallTypes)
            {
                try
                {
                    // Get compound structure for thickness
                    DB.CompoundStructure cs = wallType.GetCompoundStructure();
                    if (cs == null)
                        continue;

                    // Get total thickness in inches
                    double thickness = cs.GetWidth() * 12.0;  // Convert feet to inches

                    // Create WallProperties
                    WallProperties wallProperty = new WallProperties(
                        wallType.Name,
                        GetMaterialIdForWallType(wallType),
                        thickness
                    );

                    // Add additional properties
                    wallProperty.Properties["Function"] = wallType.Function.ToString();
                    wallProperty.Properties["FamilyName"] = wallType.FamilyName;

                    wallProperties.Add(wallProperty);
                    count++;
                }
                catch (Exception)
                {
                    // Skip this wall type and continue with the next one
                }
            }

            return count;
        }

        private string GetMaterialIdForWallType(DB.WallType wallType)
        {
            // Try to get the primary material
            try
            {
                DB.CompoundStructure cs = wallType.GetCompoundStructure();
                if (cs != null)
                {
                    int mainLayerIndex = cs.GetFirstCoreLayerIndex();
                    if (mainLayerIndex >= 0 && mainLayerIndex < cs.LayerCount)
                    {
                        DB.ElementId materialId = cs.GetMaterialId(mainLayerIndex);
                        if (materialId != DB.ElementId.InvalidElementId)
                        {
                            DB.Material material = _doc.GetElement(materialId) as DB.Material;
                            if (material != null)
                            {
                                // Create a predictable material ID
                                return $"MAT-{material.Name.Replace(" ", "")}";
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fall back to default material ID
            }

            return "MAT-default";
        }
    }
}