using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using Core.Models.Properties;

namespace Revit.Export.Properties
{
    public class FramePropertiesExport
    {
        private readonly DB.Document _doc;

        public FramePropertiesExport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Export(List<FrameProperties> frameProperties)
        {
            int count = 0;

            // Get all structural framing family symbols
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            var framingTypes = collector.OfClass(typeof(DB.FamilySymbol))
                .OfCategory(DB.BuiltInCategory.OST_StructuralFraming)
                .Cast<DB.FamilySymbol>()
                .ToList();

            // Get all structural column family symbols
            DB.FilteredElementCollector columnCollector = new DB.FilteredElementCollector(_doc);
            var columnTypes = columnCollector.OfClass(typeof(DB.FamilySymbol))
                .OfCategory(DB.BuiltInCategory.OST_StructuralColumns)
                .Cast<DB.FamilySymbol>()
                .ToList();

            // Combine all types
            var allStructuralTypes = framingTypes.Concat(columnTypes).ToList();

            foreach (var famSymbol in allStructuralTypes)
            {
                try
                {
                    // Skip if not loaded in the family
                    if (!famSymbol.IsActive)
                    {
                        famSymbol.Activate();
                    }

                    // Determine shape type
                    string shape = "W"; // Default shape
                    if (famSymbol.Family.Name.Contains("HSS") || famSymbol.Name.Contains("HSS"))
                    {
                        shape = "HSS";
                    }
                    else if (famSymbol.Family.Name.Contains("Pipe") || famSymbol.Name.Contains("Pipe"))
                    {
                        shape = "PIPE";
                    }
                    else if (famSymbol.Family.Name.Contains("Channel") || famSymbol.Name.Contains("C"))
                    {
                        shape = "C";
                    }
                    else if (famSymbol.Family.Name.Contains("Angle") || famSymbol.Name.Contains("L"))
                    {
                        shape = "L";
                    }

                    // Create FrameProperties
                    FrameProperties frameProperty = new FrameProperties(
                        famSymbol.Name,
                        GetMaterialIdForFamilySymbol(famSymbol),
                        shape
                    );

                    // Get profile dimensions if possible
                    try
                    {
                        var instance = FindInstanceOfType(famSymbol);
                        if (instance != null)
                        {
                            // Get parameters
                            double? depth = GetParameterValueInInches(instance, "h");
                            double? width = GetParameterValueInInches(instance, "b");
                            double? webThickness = GetParameterValueInInches(instance, "tw");
                            double? flangeThickness = GetParameterValueInInches(instance, "tf");

                            if (depth.HasValue)
                                frameProperty.Dimensions["depth"] = depth.Value;
                            if (width.HasValue)
                                frameProperty.Dimensions["width"] = width.Value;
                            if (webThickness.HasValue)
                                frameProperty.Dimensions["webThickness"] = webThickness.Value;
                            if (flangeThickness.HasValue)
                                frameProperty.Dimensions["flangeThickness"] = flangeThickness.Value;
                        }
                    }
                    catch
                    {
                        // Use default dimensions
                    }

                    frameProperties.Add(frameProperty);
                    count++;
                }
                catch (Exception)
                {
                    // Skip this family symbol and continue with the next one
                }
            }

            return count;
        }

        private string GetMaterialIdForFamilySymbol(DB.FamilySymbol famSymbol)
        {
            try
            {
                // Try to get material from parameters
                var materialParam = famSymbol.get_Parameter(DB.BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                if (materialParam != null && materialParam.HasValue)
                {
                    DB.ElementId materialId = materialParam.AsElementId();
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
            catch
            {
                // Fall back to default material ID
            }

            // Default to steel for structural elements
            return "MAT-Steel";
        }

        private DB.FamilyInstance FindInstanceOfType(DB.FamilySymbol famSymbol)
        {
            try
            {
                DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
                return collector.OfClass(typeof(DB.FamilyInstance))
                    .Cast<DB.FamilyInstance>()
                    .FirstOrDefault(fi => fi.Symbol.Id.Equals(famSymbol.Id));
            }
            catch
            {
                return null;
            }
        }

        private double? GetParameterValueInInches(DB.FamilyInstance instance, string paramName)
        {
            try
            {
                DB.Parameter param = instance.LookupParameter(paramName);
                if (param != null && param.HasValue && param.StorageType == DB.StorageType.Double)
                {
                    return param.AsDouble() * 12.0; // Convert feet to inches
                }
            }
            catch { }
            return null;
        }
    }
}