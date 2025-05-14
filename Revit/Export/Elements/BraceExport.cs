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

                    // Set brace type
                    DB.ElementId typeId = revitBrace.GetTypeId();
                    if (framePropertiesMap.ContainsKey(typeId))
                        brace.FramePropertiesId = framePropertiesMap[typeId];

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

            // Get all family symbols
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.FamilySymbol> famSymbols = collector.OfClass(typeof(DB.FamilySymbol))
                .Cast<DB.FamilySymbol>()
                .ToList();

            // Map each family symbol to the corresponding frame property in the model
            foreach (var symbol in famSymbols)
            {
                var frameProperty = model.Properties.FrameProperties.FirstOrDefault(fp =>
                    fp.Name == symbol.Name);

                if (frameProperty != null)
                {
                    propsMap[symbol.Id] = frameProperty.Id;
                }
            }

            return propsMap;
        }
    }
}