using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using CG = Core.Models.Geometry;
using Core.Models;
using Revit.Utilities;

namespace Revit.Export.Elements
{
    public class BraceExport
    {
        private readonly DB.Document _doc;

        public BraceExport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Export(List<CE.Brace> braces, BaseModel model)
        {
            int count = 0;

            // Get all braces from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.FamilyInstance> revitBraces = collector.OfClass(typeof(DB.FamilyInstance))
                .OfCategory(DB.BuiltInCategory.OST_StructuralFraming)
                .Cast<DB.FamilyInstance>()
                .Where(f => f.StructuralType == DB.Structure.StructuralType.Brace)
                .ToList();

            // Create mappings
            Dictionary<DB.ElementId, string> levelIdMap = CreateLevelMapping(model);
            Dictionary<DB.ElementId, string> framePropertiesMap = CreateFramePropertiesMapping(model);

            // Debug the mappings
            Debug.WriteLine($"Found {framePropertiesMap.Count} frame property mappings for braces");
            foreach (var kvp in framePropertiesMap)
            {
                var element = _doc.GetElement(kvp.Key);
                Debug.WriteLine($"Frame property mapping for brace: {element?.Name} -> {kvp.Value}");
            }

            foreach (var revitBrace in revitBraces)
            {
                try
                {
                    // Get brace location
                    DB.LocationCurve location = revitBrace.Location as DB.LocationCurve;
                    if (location == null)
                        continue;

                    DB.Curve curve = location.Curve;
                    if (!(curve is DB.Line))
                        continue; // Skip curved braces

                    DB.Line line = curve as DB.Line;

                    // Create brace object
                    CE.Brace brace = new CE.Brace();

                    // Set brace geometry
                    DB.XYZ startPoint = line.GetEndPoint(0);
                    DB.XYZ endPoint = line.GetEndPoint(1);

                    brace.StartPoint = new CG.Point2D(startPoint.X * 12.0, startPoint.Y * 12.0); // Convert to inches
                    brace.EndPoint = new CG.Point2D(endPoint.X * 12.0, endPoint.Y * 12.0);

                    // Check for zero or near-zero length braces
                    if (!IsValidBrace(brace.StartPoint, brace.EndPoint))
                    {
                        Debug.WriteLine($"Skipping zero-length or too short brace ({brace.StartPoint.X}, {brace.StartPoint.Y}) to ({brace.EndPoint.X}, {brace.EndPoint.Y})");
                        continue;
                    }

                    // Get the reference level for the brace
                    DB.ElementId referenceLevelId = revitBrace.get_Parameter(DB.BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).AsElementId();
                    if (levelIdMap.ContainsKey(referenceLevelId))
                    {
                        // Find closest levels to the start and end points
                        string refLevelId = levelIdMap[referenceLevelId];

                        // Find level closest to the bottom point
                        string baseLevelId = FindClosestLevel(model, Math.Min(startPoint.Z, endPoint.Z) * 12.0); // Convert to inches
                        if (!string.IsNullOrEmpty(baseLevelId))
                            brace.BaseLevelId = baseLevelId;
                        else
                            brace.BaseLevelId = refLevelId; // Default to reference level

                        // Find level closest to the top point
                        string topLevelId = FindClosestLevel(model, Math.Max(startPoint.Z, endPoint.Z) * 12.0); // Convert to inches
                        if (!string.IsNullOrEmpty(topLevelId))
                            brace.TopLevelId = topLevelId;
                        else
                            brace.TopLevelId = refLevelId; // Default to reference level
                    }

                    // Set brace type - get the type ID and look it up in the mapping
                    DB.ElementId typeId = revitBrace.GetTypeId();

                    // Debug the type
                    DB.FamilySymbol symbol = _doc.GetElement(typeId) as DB.FamilySymbol;
                    Debug.WriteLine($"Brace type: ID {typeId}, Name: {symbol?.Name}");

                    if (framePropertiesMap.ContainsKey(typeId))
                    {
                        brace.FramePropertiesId = framePropertiesMap[typeId];
                        Debug.WriteLine($"Found frame properties ID for brace: {brace.FramePropertiesId}");
                    }
                    else
                    {
                        // Try to find a matching frame property by name
                        if (symbol != null)
                        {
                            // Capitalize any 'x' between numbers in the name (for consistent matching)
                            string capitalizedName = CapitalizeXBetweenNumbers(symbol.Name);

                            var frameProperty = model.Properties.FrameProperties.FirstOrDefault(fp =>
                                string.Equals(fp.Name, capitalizedName, StringComparison.OrdinalIgnoreCase));

                            if (frameProperty != null)
                            {
                                brace.FramePropertiesId = frameProperty.Id;
                                Debug.WriteLine($"Found frame properties by name match for brace: {brace.FramePropertiesId}");
                            }
                            else
                            {
                                Debug.WriteLine($"Could not find frame properties for brace type: {capitalizedName}");

                                // Try to find by family name and type name combined
                                string combinedName = $"{symbol.FamilyName}_{symbol.Name}";
                                combinedName = CapitalizeXBetweenNumbers(combinedName);

                                frameProperty = model.Properties.FrameProperties.FirstOrDefault(fp =>
                                    fp.Name.IndexOf(symbol.Name, StringComparison.OrdinalIgnoreCase) >= 0);

                                if (frameProperty != null)
                                {
                                    brace.FramePropertiesId = frameProperty.Id;
                                    Debug.WriteLine($"Found frame properties by partial name match for brace: {brace.FramePropertiesId}");
                                }
                                else
                                {
                                    // Last resort: try to match by shape
                                    string shape = DetermineShapeType(symbol);
                                    frameProperty = model.Properties.FrameProperties.FirstOrDefault(fp =>
                                        fp.Shape == shape);

                                    if (frameProperty != null)
                                    {
                                        brace.FramePropertiesId = frameProperty.Id;
                                        Debug.WriteLine($"Found frame properties by shape match for brace: {brace.FramePropertiesId}");
                                    }
                                }
                            }
                        }
                    }

                    // Set material
                    try
                    {
                        DB.Parameter materialParam = revitBrace.Symbol.get_Parameter(DB.BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                        if (materialParam != null && materialParam.HasValue)
                        {
                            DB.ElementId materialId = materialParam.AsElementId();
                            if (materialId != DB.ElementId.InvalidElementId)
                            {
                                DB.Material material = _doc.GetElement(materialId) as DB.Material;
                                if (material != null)
                                {
                                    brace.MaterialId = $"MAT-{material.Name.Replace(" ", "")}";
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip material if error occurs
                    }

                    // Log the brace information
                    Debug.WriteLine($"Exporting brace from ({brace.StartPoint.X}, {brace.StartPoint.Y}) to ({brace.EndPoint.X}, {brace.EndPoint.Y}), FramePropertiesId: {brace.FramePropertiesId}");

                    braces.Add(brace);
                    count++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error exporting brace: {ex.Message}");
                    // Skip this brace and continue with the next one
                }
            }

            return count;
        }

        // Helper method to capitalize any lowercase 'x' between numbers for consistent matching
        private string CapitalizeXBetweenNumbers(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string result = input;

            // Keep replacing until no more changes occur
            bool changed;
            do
            {
                string newResult = System.Text.RegularExpressions.Regex.Replace(
                    result,
                    @"(\d+)x(\d+)",
                    m => $"{m.Groups[1].Value}X{m.Groups[2].Value}"
                );

                changed = newResult != result;
                result = newResult;
            } while (changed);

            return result;
        }

        // Determine the shape type from a family symbol
        private string DetermineShapeType(DB.FamilySymbol famSymbol)
        {
            // Capitalize any lowercase 'x' between numbers in the family symbol name
            string typeName = CapitalizeXBetweenNumbers(famSymbol.Name).ToUpper();
            string famName = famSymbol.FamilyName.ToUpper();
            string combinedName = $"{famName} {typeName}";

            // Check for W shapes (wide flange)
            if (typeName.StartsWith("W") && typeName.Contains("X"))
                return typeName; // Return the full type name for steel shapes

            // Check for HSS shapes
            if (combinedName.IndexOf("HSS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combinedName.IndexOf("TUBE", StringComparison.OrdinalIgnoreCase) >= 0)
                return typeName;

            // Check for pipe shapes
            if (combinedName.IndexOf("PIPE", StringComparison.OrdinalIgnoreCase) >= 0)
                return "PIPE";

            // Check for channel shapes
            if (typeName.StartsWith("C") && typeName.Contains("X") ||
                combinedName.IndexOf("CHANNEL", StringComparison.OrdinalIgnoreCase) >= 0)
                return typeName;

            // Check for angle shapes
            if (typeName.StartsWith("L") && typeName.Contains("X") ||
                combinedName.IndexOf("ANGLE", StringComparison.OrdinalIgnoreCase) >= 0)
                return typeName;

            // Default to the type name for steel shapes, or a generic shape
            return typeName;
        }

        // Find the level closest to a given elevation
        private string FindClosestLevel(BaseModel model, double elevation)
        {
            if (model.ModelLayout?.Levels == null || model.ModelLayout.Levels.Count == 0)
                return null;

            var sortedLevels = model.ModelLayout.Levels
                .OrderBy(l => Math.Abs(l.Elevation - elevation))
                .ToList();

            // Return the ID of the closest level
            return sortedLevels.FirstOrDefault()?.Id;
        }

        // Check if a brace has valid length (not zero or too short)
        private bool IsValidBrace(CG.Point2D startPoint, CG.Point2D endPoint)
        {
            // Calculate distance between points
            double deltaX = endPoint.X - startPoint.X;
            double deltaY = endPoint.Y - startPoint.Y;
            double length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            // Define minimum length (0.1 inches)
            const double minLength = 0.1;

            return length >= minLength;
        }

        private Dictionary<DB.ElementId, string> CreateLevelMapping(BaseModel model)
        {
            Dictionary<DB.ElementId, string> levelMap = new Dictionary<DB.ElementId, string>();

            // Get all levels from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.Level> revitLevels = collector.OfClass(typeof(DB.Level))
                .Cast<DB.Level>()
                .ToList();

            // Map each Revit level to the corresponding level in the model
            foreach (var revitLevel in revitLevels)
            {
                var modelLevel = model.ModelLayout.Levels.FirstOrDefault(l =>
                    l.Name == revitLevel.Name ||
                    Math.Abs(l.Elevation - (revitLevel.Elevation * 12.0)) < 0.1);

                if (modelLevel != null)
                {
                    levelMap[revitLevel.Id] = modelLevel.Id;
                }
            }

            return levelMap;
        }

        private Dictionary<DB.ElementId, string> CreateFramePropertiesMapping(BaseModel model)
        {
            Dictionary<DB.ElementId, string> propsMap = new Dictionary<DB.ElementId, string>();

            // Get all family symbols - focus on structural framing for braces
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.FamilySymbol> famSymbols = collector.OfClass(typeof(DB.FamilySymbol))
                .OfCategory(DB.BuiltInCategory.OST_StructuralFraming)
                .Cast<DB.FamilySymbol>()
                .ToList();

            // Debug available symbols
            Debug.WriteLine($"Found {famSymbols.Count} structural framing family symbols for braces");

            // Also get all frame properties for debugging
            Debug.WriteLine($"Model has {model.Properties.FrameProperties.Count} frame properties");
            foreach (var fp in model.Properties.FrameProperties)
            {
                Debug.WriteLine($"Frame property: {fp.Name}, ID: {fp.Id}, Shape: {fp.Shape}");
            }

            // Map each family symbol to the corresponding frame property in the model
            foreach (var symbol in famSymbols)
            {
                // Capitalize any 'x' between numbers in the name (for consistent matching)
                string capitalizedName = CapitalizeXBetweenNumbers(symbol.Name);

                // Try direct name match first
                var frameProperty = model.Properties.FrameProperties.FirstOrDefault(fp =>
                    string.Equals(fp.Name, capitalizedName, StringComparison.OrdinalIgnoreCase));

                if (frameProperty != null)
                {
                    propsMap[symbol.Id] = frameProperty.Id;
                    Debug.WriteLine($"Mapped brace {symbol.Name} to {frameProperty.Name} ({frameProperty.Id})");
                }
                else
                {
                    // Try partial name match
                    frameProperty = model.Properties.FrameProperties.FirstOrDefault(fp =>
                        fp.Name.IndexOf(capitalizedName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        capitalizedName.IndexOf(fp.Name, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (frameProperty != null)
                    {
                        propsMap[symbol.Id] = frameProperty.Id;
                        Debug.WriteLine($"Mapped brace {symbol.Name} to {frameProperty.Name} ({frameProperty.Id}) by partial match");
                    }
                }
            }

            return propsMap;
        }
    }
}
