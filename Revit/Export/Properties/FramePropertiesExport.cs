using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using Core.Models.Properties;
using System.Diagnostics;

namespace Revit.Export.Properties
{
    public class FramePropertiesExport
    {
        private readonly DB.Document _doc;
        private Dictionary<DB.ElementId, string> _materialIdMap = new Dictionary<DB.ElementId, string>();

        public FramePropertiesExport(DB.Document doc)
        {
            _doc = doc;
            CreateMaterialIdMapping();
        }

        private void CreateMaterialIdMapping()
        {
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

        public int Export(List<FrameProperties> frameProperties)
        {
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

                    // Get material ID
                    string materialId = GetMaterialId(famSymbol);
                    if (string.IsNullOrEmpty(materialId))
                        materialId = "MAT-default";

                    // Determine shape type
                    string shape = DetermineShapeType(famSymbol);

                    // Create frame property
                    FrameProperties frameProperty = new FrameProperties(
                        famSymbol.Name,
                        materialId,
                        shape
                    );

                    // Get dimensions
                    PopulateDimensions(frameProperty, famSymbol);

                    frameProperties.Add(frameProperty);
                    count++;

                    Debug.WriteLine($"Exported frame type: {famSymbol.Name}, Material: {materialId}, Shape: {shape}");
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

        private string GetMaterialId(DB.FamilySymbol famSymbol)
        {
            // Try to get material from structural material parameter
            DB.Parameter structMatParam = famSymbol.get_Parameter(DB.BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
            if (structMatParam != null && structMatParam.HasValue &&
                structMatParam.StorageType == DB.StorageType.ElementId)
            {
                DB.ElementId materialId = structMatParam.AsElementId();
                if (materialId != DB.ElementId.InvalidElementId && _materialIdMap.ContainsKey(materialId))
                {
                    return _materialIdMap[materialId];
                }
            }

            // Try to determine material from name
            string symbolName = famSymbol.Name.ToUpper();
            if (symbolName.Contains("STEEL") || symbolName.Contains("METAL") ||
                symbolName.Contains("W") || symbolName.Contains("HSS"))
            {
                // Find a steel material
                var steelMaterial = _materialIdMap.Values
                    .FirstOrDefault(id => id.Contains("Steel") || id.Contains("Metal"));

                if (!string.IsNullOrEmpty(steelMaterial))
                    return steelMaterial;

                return "MAT-Steel";
            }
            else if (symbolName.Contains("CONCRETE") || symbolName.Contains("CONC"))
            {
                // Find a concrete material
                var concreteMaterial = _materialIdMap.Values
                    .FirstOrDefault(id => id.Contains("Concrete"));

                if (!string.IsNullOrEmpty(concreteMaterial))
                    return concreteMaterial;

                return "MAT-Concrete";
            }

            return null;
        }

        private string DetermineShapeType(DB.FamilySymbol famSymbol)
        {
            string famName = famSymbol.FamilyName.ToUpper();
            string typeName = famSymbol.Name.ToUpper();
            string combinedName = $"{famName} {typeName}";

            // Check for W shapes (wide flange)
            if (typeName.StartsWith("W") && typeName.Contains("X"))
                return "W";

            // Check for HSS shapes
            if (combinedName.Contains("HSS") || combinedName.Contains("TUBE"))
                return "HSS";

            // Check for pipe shapes
            if (combinedName.Contains("PIPE"))
                return "PIPE";

            // Check for channel shapes
            if (typeName.StartsWith("C") && typeName.Contains("X") ||
                combinedName.Contains("CHANNEL"))
                return "C";

            // Check for angle shapes
            if (typeName.StartsWith("L") && typeName.Contains("X") ||
                combinedName.Contains("ANGLE"))
                return "L";

            // Default to rectangular shape
            return "RECT";
        }

        private void PopulateDimensions(FrameProperties frameProperty, DB.FamilySymbol famSymbol)
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

            // Default dimensions
            double depth = 0;
            double width = 0;

            // Try to get dimensions from parameters
            if (instance != null)
            {
                // Parameter names vary by family - try common ones
                string[] depthParams = { "h", "d", "Height", "Depth", "DEPTH" };
                string[] widthParams = { "b", "w", "Width", "WIDTH", "bf", "Flange Width" };

                foreach (string paramName in depthParams)
                {
                    double? value = GetParameterValueInInches(instance, paramName);
                    if (value.HasValue)
                    {
                        depth = value.Value;
                        break;
                    }
                }

                foreach (string paramName in widthParams)
                {
                    double? value = GetParameterValueInInches(instance, paramName);
                    if (value.HasValue)
                    {
                        width = value.Value;
                        break;
                    }
                }
            }

            // If we couldn't get dimensions, try to parse from name for W shapes
            if ((depth == 0 || width == 0) && frameProperty.Shape == "W")
            {
                // Parse W-shape name (e.g., W14X90 means 14 inches deep)
                string name = famSymbol.Name;
                if (name.StartsWith("W") && name.Contains("X"))
                {
                    try
                    {
                        string[] parts = name.Substring(1).Split('X');
                        if (parts.Length >= 1)
                        {
                            depth = double.Parse(parts[0]);
                        }
                    }
                    catch
                    {
                        // Ignore parsing errors
                    }
                }
            }

            // Set dimensions (ensuring non-zero)
            frameProperty.Dimensions["depth"] = depth > 0 ? depth : 12.0;
            frameProperty.Dimensions["width"] = width > 0 ? width : 6.0;

            // Set additional dimensions based on shape
            switch (frameProperty.Shape)
            {
                case "W":
                    frameProperty.Dimensions["webThickness"] = 0.375;
                    frameProperty.Dimensions["flangeThickness"] = 0.625;
                    break;
                case "HSS":
                    frameProperty.Dimensions["wallThickness"] = 0.25;
                    break;
                case "PIPE":
                    frameProperty.Dimensions["outerDiameter"] = width > 0 ? width : 6.0;
                    frameProperty.Dimensions["wallThickness"] = 0.25;
                    break;
                case "C":
                    frameProperty.Dimensions["webThickness"] = 0.375;
                    frameProperty.Dimensions["flangeThickness"] = 0.5;
                    break;
                case "L":
                    frameProperty.Dimensions["thickness"] = 0.375;
                    break;
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