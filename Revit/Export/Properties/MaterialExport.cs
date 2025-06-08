using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using Core.Models.Properties;
using System.Diagnostics;
using Core.Models;

namespace Revit.Export.Properties
{
    public class MaterialExport
    {
        private readonly DB.Document _doc;

        public MaterialExport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Export(List<Material> materials, Dictionary<string, bool> materialFilters = null)
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

            // Track if we've already added steel and concrete materials
            bool hasSteel = false;
            bool hasConcrete = false;
            string steelMaterialId = string.Empty;
            string concreteMaterialId = string.Empty;

            // Get filter settings
            bool exportSteel = true;
            bool exportConcrete = true;

            // Apply material filters if provided
            if (materialFilters != null)
            {
                if (materialFilters.TryGetValue("Steel", out bool steelEnabled))
                    exportSteel = steelEnabled;

                if (materialFilters.TryGetValue("Concrete", out bool concreteEnabled))
                    exportConcrete = concreteEnabled;
            }

            // Export only the materials that are used by structural elements
            foreach (var materialId in usedMaterialIds)
            {
                try
                {
                    DB.Material revitMaterial = _doc.GetElement(materialId) as DB.Material;
                    if (revitMaterial == null || string.IsNullOrEmpty(revitMaterial.Name))
                        continue;

                    // Determine material type
                    MaterialType materialType = DetermineStructuralMaterialType(revitMaterial);

                    // Skip if material type is filtered out
                    if ((materialType == MaterialType.Steel && !exportSteel) ||
                        (materialType == MaterialType.Concrete && !exportConcrete))
                        continue;

                    // Check if we already have this material type
                    if (materialType == MaterialType.Steel && hasSteel)
                        continue;
                    if (materialType == MaterialType.Concrete && hasConcrete)
                        continue;

                    // Create material with the determined type
                    Material material = new Material(revitMaterial.Name, materialType);

                    // Set material properties based on type
                    PopulateMaterialProperties(material, revitMaterial);

                    materials.Add(material);
                    count++;

                    // Mark this material type as added and store its ID
                    if (materialType == MaterialType.Steel)
                    {
                        hasSteel = true;
                        steelMaterialId = material.Id;
                    }
                    else if (materialType == MaterialType.Concrete)
                    {
                        hasConcrete = true;
                        concreteMaterialId = material.Id;
                    }

                    Debug.WriteLine($"Exported material: {material.Name}, Type: {material.Type}, ID: {material.Id}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error exporting material {materialId}: {ex.Message}");
                }
            }

            // Ensure we have at least one steel and one concrete material if they're enabled in filters
            if (exportSteel && !hasSteel)
            {
                Material steelMaterial = new Material("Steel", MaterialType.Steel);
                PopulateDefaultSteelProperties(steelMaterial);
                materials.Add(steelMaterial);
                count++;
                Debug.WriteLine($"Added default Steel material with ID: {steelMaterial.Id}");
            }

            if (exportConcrete && !hasConcrete)
            {
                Material concreteMaterial = new Material("Concrete", MaterialType.Concrete);
                PopulateDefaultConcreteProperties(concreteMaterial);
                materials.Add(concreteMaterial);
                count++;
                Debug.WriteLine($"Added default Concrete material with ID: {concreteMaterial.Id}");
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
                .OfClass(typeof(DB.Wall))
                .Where(w => !(w is DB.DirectShape))
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

        private MaterialType DetermineStructuralMaterialType(DB.Material revitMaterial)
        {
            // Default to Steel
            MaterialType materialType = MaterialType.Steel;

            // Try to determine material type from name first
            string matName = revitMaterial.Name.ToUpper();

            if (matName.Contains("CONCRETE") || matName.Contains("CONC") ||
                matName.Contains("CON"))
            {
                materialType = MaterialType.Concrete;
            }
            else
            {
                materialType = MaterialType.Steel;
            }

            // If not found by name, try by material class
            if (materialType == MaterialType.Steel && !string.IsNullOrEmpty(revitMaterial.MaterialClass))
            {
                string matClass = revitMaterial.MaterialClass.ToUpper();

                if (matClass.Contains("CONCRETE"))
                {
                    materialType = MaterialType.Concrete;
                }
                else
                {
                    materialType = MaterialType.Steel;
                }
            }

            return materialType;
        }

        private void PopulateMaterialProperties(Material material, DB.Material revitMaterial)
        {
            try
            {
                // Set base properties
                material.ElasticModulus = GetMaterialDoubleProperty(revitMaterial, "YoungModulus") ??
                                         (material.Type == MaterialType.Steel ? 29000000.0 : 3600000.0);
                material.PoissonsRatio = GetMaterialDoubleProperty(revitMaterial, "PoissonRatio") ??
                                        (material.Type == MaterialType.Steel ? 0.3 : 0.2);
                material.WeightPerUnitVolume = GetMaterialDoubleProperty(revitMaterial, "Density") ??
                                             (material.Type == MaterialType.Steel ? 490.0 : 150.0);

                // Set symmetric type (all structural materials are isotropic)
                material.DirectionalSymmetryType = DirectionalSymmetryType.Isotropic;

                // Set type-specific properties
                if (material.Type == MaterialType.Concrete)
                {
                    // For concrete, initialize and set concrete properties
                    material.ConcreteProps = new ConcreteProperties();

                    // Try to get concrete strength
                    double? fc = GetMaterialDoubleProperty(revitMaterial, "Compression");
                    if (fc.HasValue && fc.Value > 0)
                    {
                        material.ConcreteProps.Fc = fc.Value;
                    }
                    else
                    {
                        material.ConcreteProps.Fc = 4000.0; // Default 4000 psi
                    }

                    material.ConcreteProps.WeightClass = WeightClass.Normal;
                }
                else if (material.Type == MaterialType.Steel)
                {
                    // For steel, initialize and set steel properties
                    material.SteelProps = new SteelProperties();

                    // Try to get yield strength
                    double? fy = GetMaterialDoubleProperty(revitMaterial, "YieldStress");
                    if (fy.HasValue && fy.Value > 0)
                    {
                        material.SteelProps.Fy = fy.Value;
                    }
                    else
                    {
                        material.SteelProps.Fy = 50000.0; // Default 50 ksi
                    }

                    // Try to get tensile strength
                    double? fu = GetMaterialDoubleProperty(revitMaterial, "TensileStrength");
                    if (fu.HasValue && fu.Value > 0)
                    {
                        material.SteelProps.Fu = fu.Value;
                    }
                    else
                    {
                        material.SteelProps.Fu = 65000.0; // Default 65 ksi
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting properties for material {material.Name}: {ex.Message}");
            }
        }

        private void PopulateDefaultConcreteProperties(Material material)
        {
            material.ElasticModulus = 3600000.0; // psi
            material.PoissonsRatio = 0.2;
            material.WeightPerUnitVolume = 150.0; // pcf
            material.DirectionalSymmetryType = DirectionalSymmetryType.Isotropic;

            material.ConcreteProps = new ConcreteProperties
            {
                Fc = 4000.0, // psi
                WeightClass = WeightClass.Normal
            };
        }

        private void PopulateDefaultSteelProperties(Material material)
        {
            material.ElasticModulus = 29000000.0; // psi
            material.PoissonsRatio = 0.3;
            material.WeightPerUnitVolume = 490.0; // pcf
            material.DirectionalSymmetryType = DirectionalSymmetryType.Isotropic;

            material.SteelProps = new SteelProperties
            {
                Fy = 50000.0, // psi
                Fu = 65000.0  // psi
            };
        }

        private double? GetMaterialDoubleProperty(DB.Material material, string propertyName)
        {
            try
            {
                // Try to get parameter by name
                DB.Parameter param = material.LookupParameter(propertyName);
                if (param != null && param.HasValue && param.StorageType == DB.StorageType.Double)
                {
                    return param.AsDouble();
                }

                // Try structural asset if available
                if (material.StructuralAssetId != DB.ElementId.InvalidElementId)
                {
                    var structAsset = _doc.GetElement(material.StructuralAssetId) as DB.PropertySetElement;
                    if (structAsset != null)
                    {
                        param = structAsset.LookupParameter(propertyName);
                        if (param != null && param.HasValue && param.StorageType == DB.StorageType.Double)
                        {
                            return param.AsDouble();
                        }
                    }
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