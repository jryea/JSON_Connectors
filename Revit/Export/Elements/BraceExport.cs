using System;
using System.Collections.Generic;
using System.Linq;
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

                    // Set levels
                    DB.ElementId baseLevelId = revitBrace.get_Parameter(DB.BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).AsElementId();
                    DB.ElementId topLevelId = revitBrace.get_Parameter(DB.BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).AsElementId();

                    if (levelIdMap.ContainsKey(baseLevelId))
                        brace.BaseLevelId = levelIdMap[baseLevelId];

                    if (levelIdMap.ContainsKey(topLevelId))
                        brace.TopLevelId = levelIdMap[topLevelId];
                    else
                        brace.TopLevelId = brace.BaseLevelId; // Default to base level if top level not found

                    // Set brace type
                    DB.ElementId typeId = revitBrace.GetTypeId();
                    if (framePropertiesMap.ContainsKey(typeId))
                        brace.FramePropertiesId = framePropertiesMap[typeId];

                    // Set brace geometry
                    DB.XYZ startPoint = line.GetEndPoint(0);
                    DB.XYZ endPoint = line.GetEndPoint(1);

                    brace.StartPoint = new CG.Point2D(startPoint.X * 12.0, startPoint.Y * 12.0); // Convert to inches
                    brace.EndPoint = new CG.Point2D(endPoint.X * 12.0, endPoint.Y * 12.0);

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
                catch (Exception)
                {
                    // Skip this brace and continue with the next one
                }
            }

            return count;
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