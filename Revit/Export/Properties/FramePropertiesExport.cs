using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using Core.Models.Properties;
using Core.Models;
using System.Diagnostics;

namespace Revit.Export.Properties
{
    public class FramePropertiesExport
    {
        private readonly DB.Document _doc;
        private Dictionary<MaterialType, string> _materialTypeToIdMap;

        public FramePropertiesExport(DB.Document doc)
        {
            _doc = doc;
            _materialTypeToIdMap = new Dictionary<MaterialType, string>();
        }

        // This method should be called after materials have been exported
        // to set up the mapping between material types and their IDs
        public void SetupMaterialMapping(List<Material> exportedMaterials)
        {
            _materialTypeToIdMap.Clear();

            foreach (var material in exportedMaterials)
            {
                if (material.Type == MaterialType.Steel || material.Type == MaterialType.Concrete)
                {
                    _materialTypeToIdMap[material.Type] = material.Id;
                }
            }

            // Ensure we have defaults even if they weren't in the exported materials
            if (!_materialTypeToIdMap.ContainsKey(MaterialType.Steel))
            {
                _materialTypeToIdMap[MaterialType.Steel] = "MAT-Steel";
            }
            if (!_materialTypeToIdMap.ContainsKey(MaterialType.Concrete))
            {
                _materialTypeToIdMap[MaterialType.Concrete] = "MAT-Concrete";
            }

            Debug.WriteLine($"Material mapping set up: Steel ID = {_materialTypeToIdMap[MaterialType.Steel]}, Concrete ID = {_materialTypeToIdMap[MaterialType.Concrete]}");
        }

        public int Export(List<FrameProperties> frameProperties, List<Material> exportedMaterials)
        {
            // Set up material ID mapping
            SetupMaterialMapping(exportedMaterials);

            int count = 0;
            HashSet<DB.ElementId> structuralFrameTypeIds = new HashSet<DB.ElementId>();

            // Collect family symbols used by structural columns
            CollectStructuralTypes(DB.BuiltInCategory.OST_StructuralColumns, structuralFrameTypeIds);

            // Collect family symbols used by structural framing (beams, braces)
            CollectStructuralTypes(DB.BuiltInCategory.OST_StructuralFraming, structuralFrameTypeIds);

            Debug.WriteLine($"Found {structuralFrameTypeIds.Count} frame types used by structural elements");

            // Export only the family symbols that are used by structural elements
            foreach (var typeId in structuralFrameTypeIds)
            {
                try
                {
                    DB.FamilySymbol famSymbol = _doc.GetElement(typeId) as DB.FamilySymbol;
                    if (famSymbol == null || !famSymbol.IsActive)
                    {
                        try { famSymbol.Activate(); } catch { }
                        if (famSymbol == null)
                            continue;
                    }

                    // Determine material type and get corresponding material ID
                    MaterialType materialType = DetermineMaterialType(famSymbol);
                    string materialId = _materialTypeToIdMap[materialType];

                    // Create frame property
                    FrameProperties frameProperty = new FrameProperties(
                        famSymbol.Name,
                        materialId,
                        materialType == MaterialType.Steel ? FrameMaterialType.Steel : FrameMaterialType.Concrete
                    );

                    // Set shape-specific properties based on material type
                    if (materialType == MaterialType.Steel)
                    {
                        SteelSectionType sectionType = DetermineSteelSectionType(famSymbol);
                        frameProperty.SteelProps = new SteelFrameProperties
                        {
                            SectionType = sectionType,
                            SectionName = famSymbol.Name
                        };
                    }
                    else if (materialType == MaterialType.Concrete)
                    {
                        ConcreteSectionType sectionType = DetermineConcreteSectionType(famSymbol);
                        frameProperty.ConcreteProps = new ConcreteFrameProperties
                        {
                            SectionType = sectionType,
                            SectionName = famSymbol.Name
                        };

                        // Add dimensions to the dictionary
                        GetConcreteDimensions(famSymbol, frameProperty.ConcreteProps);
                    }

                    frameProperties.Add(frameProperty);
                    count++;

                    Debug.WriteLine($"Exported frame type: {famSymbol.Name}, Material: {materialType}, MaterialID: {materialId}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error exporting frame type: {ex.Message}");
                }
            }

            return count;
        }

        private void CollectStructuralTypes(DB.BuiltInCategory category, HashSet<DB.ElementId> typeIds)
        {
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.Element> elements = collector.OfCategory(category)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (var element in elements)
            {
                DB.ElementId typeId = element.GetTypeId();
                if (typeId == null || typeId == DB.ElementId.InvalidElementId)
                    continue;
                try
                {
                    // Add the type ID to our collection
                    typeIds.Add(typeId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing element {element.Id}: {ex.Message}");
                }
            }
        }

        private MaterialType DetermineMaterialType(DB.FamilySymbol famSymbol)
        {
            // Try to get material from structural material parameter
            DB.Parameter structMatParam = famSymbol.get_Parameter(DB.BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
            if (structMatParam != null && structMatParam.HasValue &&
                structMatParam.StorageType == DB.StorageType.ElementId)
            {
                DB.ElementId materialId = structMatParam.AsElementId();
                if (materialId != DB.ElementId.InvalidElementId)
                {
                    DB.Material material = _doc.GetElement(materialId) as DB.Material;
                    if (material != null)
                    {
                        return DetermineMaterialTypeFromMaterial(material);
                    }
                }
            }

            // If no material parameter, determine material from shape and name
            string symbolName = famSymbol.Name.ToUpper();
            string familyName = famSymbol.FamilyName.ToUpper();

            // For steel shapes
            if (symbolName.StartsWith("W") || symbolName.Contains("HSS") ||
                symbolName.Contains("STEEL") || symbolName.Contains("METAL") ||
                familyName.Contains("STEEL") || familyName.Contains("METAL") ||
                symbolName.StartsWith("L") || symbolName.StartsWith("C"))
            {
                return MaterialType.Steel;
            }

            // For concrete shapes
            if (symbolName.Contains("CONCRETE") || symbolName.Contains("CONC") ||
                familyName.Contains("CONCRETE") || familyName.Contains("CONC"))
            {
                return MaterialType.Concrete;
            }

            // Default to steel for most structural members if nothing else matches
            return MaterialType.Steel;
        }

        private MaterialType DetermineMaterialTypeFromMaterial(DB.Material material)
        {
            // Try to determine material type from name first
            string matName = material.Name.ToUpper();

            if (matName.Contains("CONCRETE") || matName.Contains("CONC") ||
                matName.Contains("CON") || matName.Contains("CST") ||
                matName.Contains("4000") || matName.Contains("5000") ||
                matName.Contains("6000") || matName.Contains("PSI"))
            {
                return MaterialType.Concrete;
            }
            else if (matName.Contains("STEEL") || matName.Contains("METAL") ||
                    matName.Contains("A992") || matName.Contains("A36") ||
                    matName.Contains("HSS") || matName.Contains("AISC") ||
                    matName.Contains("FY50") || matName.Contains("FY36") ||
                    (matName.StartsWith("W") && matName.Contains("X")))
            {
                return MaterialType.Steel;
            }

            // If not found by name, try by material class
            if (!string.IsNullOrEmpty(material.MaterialClass))
            {
                string matClass = material.MaterialClass.ToUpper();

                if (matClass.Contains("CONCRETE"))
                {
                    return MaterialType.Concrete;
                }
                else if (matClass.Contains("METAL"))
                {
                    return MaterialType.Steel;
                }
            }

            // Default to Steel if nothing else matches
            return MaterialType.Steel;
        }

        private SteelSectionType DetermineSteelSectionType(DB.FamilySymbol famSymbol)
        {
            string famName = famSymbol.FamilyName.ToUpper();
            string typeName = famSymbol.Name.ToUpper();
            string combinedName = $"{famName} {typeName}";

            // Check for W shapes (wide flange)
            if (typeName.StartsWith("W") && typeName.Contains("X"))
                return SteelSectionType.W;

            // Check for HSS shapes
            if (combinedName.Contains("HSS") || combinedName.Contains("TUBE"))
                return SteelSectionType.HSS;

            // Check for pipe shapes
            if (combinedName.Contains("PIPE"))
                return SteelSectionType.PIPE;

            // Check for channel shapes
            if (typeName.StartsWith("C") && typeName.Contains("X") ||
                combinedName.Contains("CHANNEL"))
                return SteelSectionType.C;

            // Check for angle shapes
            if (typeName.StartsWith("L") && typeName.Contains("X") ||
                combinedName.Contains("ANGLE"))
                return SteelSectionType.L;

            // Default to W shape if nothing else matches
            return SteelSectionType.W;
        }

        private ConcreteSectionType DetermineConcreteSectionType(DB.FamilySymbol famSymbol)
        {
            string famName = famSymbol.FamilyName.ToUpper();
            string typeName = famSymbol.Name.ToUpper();
            string combinedName = $"{famName} {typeName}";

            // Check for circular shapes
            if (combinedName.Contains("CIRCULAR") || combinedName.Contains("ROUND"))
                return ConcreteSectionType.Circular;

            // Check for T-shaped sections
            if (combinedName.Contains("T-SHAPED") || combinedName.Contains("T SHAPED") ||
                combinedName.Contains("TEE"))
                return ConcreteSectionType.TShaped;

            // Check for L-shaped sections
            if (combinedName.Contains("L-SHAPED") || combinedName.Contains("L SHAPED"))
                return ConcreteSectionType.LShaped;

            // Default to rectangular for most concrete columns
            return ConcreteSectionType.Rectangular;
        }

        private void GetConcreteDimensions(DB.FamilySymbol famSymbol, ConcreteFrameProperties concreteProps)
        {
            // Try to find an instance to get parameter values
            DB.FamilyInstance instance = null;
            try
            {
                DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
                instance = collector.OfClass(typeof(DB.FamilyInstance))
                    .Cast<DB.FamilyInstance>()
                    .FirstOrDefault(fi => fi.Symbol.Id.Equals(famSymbol.Id));
            }
            catch
            {
                // Ignore errors finding instance
            }

            // Try to get dimensions from parameters
            if (instance != null)
            {
                // Parameter names vary by family - try common ones
                string[] dimensionParams = {
                    "b", "h", "d", "Height", "Width", "Depth",
                    "DEPTH", "WIDTH", "Diameter", "Radius"
                };

                foreach (string paramName in dimensionParams)
                {
                    double? value = GetParameterValueInInches(instance, paramName);
                    if (value.HasValue)
                    {
                        // Store in dictionary using parameter name
                        concreteProps.Dimensions[paramName] = value.Value.ToString();
                    }
                }
            }

            // If dimensions dictionary is empty, add some defaults based on section type
            if (concreteProps.Dimensions.Count == 0)
            {
                switch (concreteProps.SectionType)
                {
                    case ConcreteSectionType.Rectangular:
                        concreteProps.Dimensions["b"] = "12";  // Width
                        concreteProps.Dimensions["h"] = "12";  // Height
                        break;
                    case ConcreteSectionType.Circular:
                        concreteProps.Dimensions["Diameter"] = "12";
                        break;
                    case ConcreteSectionType.TShaped:
                    case ConcreteSectionType.LShaped:
                        concreteProps.Dimensions["b"] = "12";  // Width
                        concreteProps.Dimensions["h"] = "12";  // Height
                        concreteProps.Dimensions["bf"] = "24"; // Flange width
                        concreteProps.Dimensions["tf"] = "6";  // Flange thickness
                        break;
                }
            }
        }

        private double? GetParameterValueInInches(DB.FamilyInstance instance, string paramName)
        {
            try
            {
                DB.Parameter param = instance.LookupParameter(paramName);
                if (param != null && param.HasValue && param.StorageType == DB.StorageType.Double)
                {
                    return param.AsDouble() * 12.0; // Convert from feet to inches
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