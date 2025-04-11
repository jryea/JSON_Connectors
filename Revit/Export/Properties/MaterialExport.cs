using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using Core.Models.Properties;
using System.Diagnostics;

namespace Revit.Export.Properties
{
    public class MaterialExport
    {
        private readonly DB.Document _doc;

        public MaterialExport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Export(List<Material> materials)
        {
            int count = 0;

            // Collect all materials used by structural elements
            HashSet<DB.ElementId> usedMaterialIds = new HashSet<DB.ElementId>();

            // Collect materials from structural columns
            CollectMaterialsFromCategory(DB.BuiltInCategory.OST_StructuralColumns, usedMaterialIds);

            // Collect materials from structural framing (beams, braces)
            CollectMaterialsFromCategory(DB.BuiltInCategory.OST_StructuralFraming, usedMaterialIds);

            // Collect materials from structural floors
            CollectMaterialsFromCategory(DB.BuiltInCategory.OST_Floors, usedMaterialIds);

            // Collect materials from structural walls
            CollectMaterialsFromStructuralWalls(usedMaterialIds);

            Debug.WriteLine($"Found {usedMaterialIds.Count} unique materials used by structural elements");

            // Export only the materials that are used by structural elements
            foreach (var materialId in usedMaterialIds)
            {
                try
                {
                    DB.Material revitMaterial = _doc.GetElement(materialId) as DB.Material;
                    if (revitMaterial == null || string.IsNullOrEmpty(revitMaterial.Name))
                        continue;

                    // Determine material type
                    string materialType = DetermineStructuralMaterialType(revitMaterial);

                    // Create material
                    Material material = new Material(revitMaterial.Name, materialType);

                    // Set material properties based on type
                    PopulateMaterialProperties(material, revitMaterial);

                    materials.Add(material);
                    count++;

                    Debug.WriteLine($"Exported material: {material.Name}, Type: {material.Type}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error exporting material {materialId}: {ex.Message}");
                }
            }

            return count;
        }

        private void CollectMaterialsFromCategory(DB.BuiltInCategory category, HashSet<DB.ElementId> materialIds)
        {
            var collector = new DB.FilteredElementCollector(_doc);
            var elements = collector.OfCategory(category)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (var element in elements)
            {
                try
                {
                    // Get material from element
                    DB.ElementId materialId = GetElementMaterialId(element);
                    if (materialId != null && materialId != DB.ElementId.InvalidElementId)
                    {
                        materialIds.Add(materialId);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting material for element {element.Id}: {ex.Message}");
                }
            }
        }

        private void CollectMaterialsFromStructuralWalls(HashSet<DB.ElementId> materialIds)
        {
            var collector = new DB.FilteredElementCollector(_doc);
            var walls = collector.OfCategory(DB.BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .Cast<DB.Wall>()
                .ToList();

            foreach (var wall in walls)
            {
                try
                {
                    // Check if wall is structural
                    DB.Parameter isStructuralParam = wall.get_Parameter(DB.BuiltInParameter.WALL_STRUCTURAL_USAGE_PARAM);
                    if (isStructuralParam != null && isStructuralParam.AsInteger() > 0)
                    {
                        // Get material from wall
                        DB.ElementId materialId = GetElementMaterialId(wall);
                        if (materialId != null && materialId != DB.ElementId.InvalidElementId)
                        {
                            materialIds.Add(materialId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting material for wall {wall.Id}: {ex.Message}");
                }
            }
        }

        private DB.ElementId GetElementMaterialId(DB.Element element)
        {
            // First try to get structural material parameter
            DB.Parameter structMatParam = element.get_Parameter(DB.BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
            if (structMatParam != null && structMatParam.HasValue &&
                structMatParam.StorageType == DB.StorageType.ElementId)
            {
                return structMatParam.AsElementId();
            }

            // For family instances, try getting material from family symbol
            DB.FamilyInstance famInstance = element as DB.FamilyInstance;
            if (famInstance != null)
            {
                DB.FamilySymbol symbol = famInstance.Symbol;
                if (symbol != null)
                {
                    structMatParam = symbol.get_Parameter(DB.BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                    if (structMatParam != null && structMatParam.HasValue &&
                        structMatParam.StorageType == DB.StorageType.ElementId)
                    {
                        return structMatParam.AsElementId();
                    }
                }
            }

            // For walls and floors, try to get material from compound structure
            try
            {
                if (element is DB.Wall)
                {
                    DB.Wall wall = element as DB.Wall;
                    DB.WallType wallType = wall.WallType;
                    if (wallType != null)
                    {
                        DB.CompoundStructure cs = wallType.GetCompoundStructure();
                        if (cs != null && cs.LayerCount > 0)
                        {
                            int structuralLayer = cs.GetFirstCoreLayerIndex();
                            if (structuralLayer >= 0)
                            {
                                return cs.GetMaterialId(structuralLayer);
                            }
                        }
                    }
                }
                else if (element is DB.Floor)
                {
                    DB.Floor floor = element as DB.Floor;
                    DB.FloorType floorType = floor.FloorType;
                    if (floorType != null)
                    {
                        DB.CompoundStructure cs = floorType.GetCompoundStructure();
                        if (cs != null && cs.LayerCount > 0)
                        {
                            int structuralLayer = cs.GetFirstCoreLayerIndex();
                            if (structuralLayer >= 0)
                            {
                                return cs.GetMaterialId(structuralLayer);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting material from compound structure: {ex.Message}");
            }

            return DB.ElementId.InvalidElementId;
        }

        private string DetermineStructuralMaterialType(DB.Material revitMaterial)
        {
            // Default to Generic
            string materialType = "Generic";

            // Try to determine material type from name first
            string matName = revitMaterial.Name.ToUpper();

            if (matName.Contains("CONCRETE") || matName.Contains("CONC") ||
                matName.Contains("CON") || matName.Contains("CST") ||
                matName.Contains("4000") || matName.Contains("5000") ||
                matName.Contains("6000") || matName.Contains("PSI"))
            {
                materialType = "Concrete";
            }
            else if (matName.Contains("STEEL") || matName.Contains("METAL") ||
                    matName.Contains("A992") || matName.Contains("A36") ||
                    matName.Contains("HSS") || matName.Contains("AISC") ||
                    matName.Contains("FY50") || matName.Contains("FY36") ||
                    (matName.StartsWith("W") && matName.Contains("X")))
            {
                materialType = "Steel";
            }

            // If not found by name, try by material class
            if (materialType == "Generic" && !string.IsNullOrEmpty(revitMaterial.MaterialClass))
            {
                string matClass = revitMaterial.MaterialClass.ToUpper();

                if (matClass.Contains("CONCRETE"))
                {
                    materialType = "Concrete";
                }
                else if (matClass.Contains("METAL"))
                {
                    materialType = "Steel";
                }
            }

            return materialType;
        }

        private void PopulateMaterialProperties(Material material, DB.Material revitMaterial)
        {
            try
            {
                // Common properties
                if (material.Type == "Concrete")
                {
                    // Try to get concrete strength
                    double? fc = GetMaterialPropertyValue(revitMaterial, "Concrete Compressive Strength");
                    if (fc.HasValue)
                    {
                        material.DesignData["fc"] = fc.Value;
                    }
                    else
                    {
                        // Default concrete strength (4000 psi)
                        material.DesignData["fc"] = 4000.0;
                    }

                    // Default elastic modulus if not available
                    material.DesignData["elasticModulus"] = 3600000.0; // psi
                    material.DesignData["poissonsRatio"] = 0.2;
                }
                else if (material.Type == "Steel")
                {
                    // Try to get steel yield strength
                    double? fy = GetMaterialPropertyValue(revitMaterial, "Steel Yield Strength");
                    if (fy.HasValue)
                    {
                        material.DesignData["fy"] = fy.Value;
                    }
                    else
                    {
                        // Default steel yield strength (50 ksi)
                        material.DesignData["fy"] = 50000.0;
                    }

                    // Default tensile strength if not available
                    material.DesignData["fu"] = 65000.0; // psi

                    // Default elastic modulus if not available
                    material.DesignData["elasticModulus"] = 29000000.0; // psi
                    material.DesignData["poissonsRatio"] = 0.3;
                }

                
                // Default densities
                if (material.Type == "Concrete")
                {
                    material.DesignData["weightDensity"] = 150.0; // pcf
                }
                else if (material.Type == "Steel")
                {
                    material.DesignData["weightDensity"] = 490.0; // pcf
                }
                

                // Set symmetry type for structural materials
                material.DirectionalSymmetryType = "Isotropic";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting properties for material {material.Name}: {ex.Message}");
            }
        }

        private double? GetMaterialPropertyValue(DB.Material material, string propertyName)
        {
            try
            {
                // Try to get parameter by name
                DB.Parameter param = material.LookupParameter(propertyName);
                if (param != null && param.HasValue && param.StorageType == DB.StorageType.Double)
                {
                    return param.AsDouble();
                }
            }
            catch
            {
                // Ignore errors and return null
            }

            return null;
        }
    }
}