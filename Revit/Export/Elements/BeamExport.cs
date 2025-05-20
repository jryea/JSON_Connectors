using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using CG = Core.Models.Geometry;
using Core.Models;
using Revit.Utilities;
using System.Diagnostics;

namespace Revit.Export.Elements
{
    public class BeamExport
    {
        private readonly DB.Document _doc;

        public BeamExport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Export(List<CE.Beam> beams, BaseModel model)
        {
            int count = 0;

            // Get all beams from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.FamilyInstance> revitBeams = collector.OfClass(typeof(DB.FamilyInstance))
                .OfCategory(DB.BuiltInCategory.OST_StructuralFraming)
                .Cast<DB.FamilyInstance>()
                .Where(f => f.StructuralType == DB.Structure.StructuralType.Beam)
                .ToList();

            // Create mappings
            Dictionary<DB.ElementId, string> levelIdMap = CreateLevelMapping(model);
            Dictionary<DB.ElementId, string> framePropertiesMap = CreateFramePropertiesMapping(model);

            foreach (var revitBeam in revitBeams)
            {
                try
                {
                    // Get beam location
                    DB.LocationCurve location = revitBeam.Location as DB.LocationCurve;
                    if (location == null)
                        continue;

                    DB.Curve curve = location.Curve;
                    if (!(curve is DB.Line))
                        continue; // Skip curved beams

                    DB.Line line = curve as DB.Line;

                    // Create beam object
                    CE.Beam beam = new CE.Beam();

                    // Set level
                    DB.ElementId levelId = revitBeam.get_Parameter(DB.BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).AsElementId();
                    if (levelIdMap.ContainsKey(levelId))
                        beam.LevelId = levelIdMap[levelId];

                    // Set beam type
                    DB.ElementId typeId = revitBeam.GetTypeId();
                    if (framePropertiesMap.ContainsKey(typeId))
                        beam.FramePropertiesId = framePropertiesMap[typeId];

                    // Set beam geometry
                    DB.XYZ startPoint = line.GetEndPoint(0);
                    DB.XYZ endPoint = line.GetEndPoint(1);

                    beam.StartPoint = new CG.Point2D(startPoint.X * 12.0, startPoint.Y * 12.0); // Convert to inches
                    beam.EndPoint = new CG.Point2D(endPoint.X * 12.0, endPoint.Y * 12.0);

                    // Determine if beam is part of lateral system
                    beam.IsLateral = IsBeamLateral(revitBeam);

                    // Determine if beam is a joist
                    beam.IsJoist = IsBeamJoist(revitBeam);

                    beams.Add(beam);
                    count++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error exporting beam: {ex.Message}");
                    // Skip this beam and continue with the next one
                }
            }

            return count;
        }
        private bool IsBeamLateral(DB.FamilyInstance beam)
        {
            try
            {
                // Check if both beam ends have moment connections
                bool hasStartMomentConnection = false;
                bool hasEndMomentConnection = false;

                // Check start connection
                DB.Parameter startConnectionParam = beam.get_Parameter(DB.BuiltInParameter.STRUCT_CONNECTION_BEAM_START);
                if (startConnectionParam != null && startConnectionParam.HasValue)
                {
                    string connectionType = startConnectionParam.AsValueString();
                    hasStartMomentConnection = connectionType.Equals("Moment Connection", StringComparison.OrdinalIgnoreCase);
                }

                // Check end connection
                DB.Parameter endConnectionParam = beam.get_Parameter(DB.BuiltInParameter.STRUCT_CONNECTION_BEAM_END);
                if (endConnectionParam != null && endConnectionParam.HasValue)
                {
                    string connectionType = endConnectionParam.AsValueString();
                    hasEndMomentConnection = connectionType.Equals("Moment Connection", StringComparison.OrdinalIgnoreCase);
                }

                // If both ends have moment connections, beam is part of lateral system
                if (hasStartMomentConnection && hasEndMomentConnection)
                {
                    Debug.WriteLine($"Beam identified as lateral: both ends have moment connections");
                    return true;
                }

                // Get beam endpoints
                DB.LocationCurve locationCurve = beam.Location as DB.LocationCurve;
                if (locationCurve == null)
                    return false;

                DB.Curve curve = locationCurve.Curve;
                if (!(curve is DB.Line))
                    return false;

                DB.XYZ beamStart = curve.GetEndPoint(0);
                DB.XYZ beamEnd = curve.GetEndPoint(1);

                // Create XY-only points for comparison (ignore Z)
                DB.XYZ beamStartXY = new DB.XYZ(beamStart.X, beamStart.Y, 0);
                DB.XYZ beamEndXY = new DB.XYZ(beamEnd.X, beamEnd.Y, 0);

                // Get all braces in the document
                DB.FilteredElementCollector braceCollector = new DB.FilteredElementCollector(_doc);
                IList<DB.FamilyInstance> braces = braceCollector.OfClass(typeof(DB.FamilyInstance))
                    .OfCategory(DB.BuiltInCategory.OST_StructuralFraming)
                    .Cast<DB.FamilyInstance>()
                    .Where(f => f.StructuralType == DB.Structure.StructuralType.Brace)
                    .ToList();

                const double TOL_XY = 2.0;  // 2 feet tolerance for XY endpoint proximity

                // Track whether we found a brace close to each beam endpoint
                bool startEndpointHasNearbyBrace = false;
                bool endEndpointHasNearbyBrace = false;

                // For each brace, check if its endpoints are close to beam endpoints in XY
                foreach (var brace in braces)
                {
                    DB.LocationCurve braceCurve = brace.Location as DB.LocationCurve;
                    if (braceCurve == null)
                        continue;

                    DB.Curve bCurve = braceCurve.Curve;
                    if (!(bCurve is DB.Line))
                        continue;

                    DB.XYZ braceStart = bCurve.GetEndPoint(0);
                    DB.XYZ braceEnd = bCurve.GetEndPoint(1);

                    // Create XY-only points for brace endpoints
                    DB.XYZ braceStartXY = new DB.XYZ(braceStart.X, braceStart.Y, 0);
                    DB.XYZ braceEndXY = new DB.XYZ(braceEnd.X, braceEnd.Y, 0);

                    // Check if beam start is close to either brace endpoint
                    if (!startEndpointHasNearbyBrace)
                    {
                        if (beamStartXY.DistanceTo(braceStartXY) <= TOL_XY ||
                            beamStartXY.DistanceTo(braceEndXY) <= TOL_XY)
                        {
                            startEndpointHasNearbyBrace = true;
                        }
                    }

                    // Check if beam end is close to either brace endpoint
                    if (!endEndpointHasNearbyBrace)
                    {
                        if (beamEndXY.DistanceTo(braceStartXY) <= TOL_XY ||
                            beamEndXY.DistanceTo(braceEndXY) <= TOL_XY)
                        {
                            endEndpointHasNearbyBrace = true;
                        }
                    }

                    // If both beam endpoints have a nearby brace, beam is part of lateral system
                    if (startEndpointHasNearbyBrace && endEndpointHasNearbyBrace)
                    {
                        Debug.WriteLine($"Beam identified as lateral: both endpoints close to brace endpoints");
                        return true;
                    }
                }

                // As a fallback, check naming conventions if still not identified
                string familyName = beam.Symbol.FamilyName.ToUpper();
                string typeName = beam.Symbol.Name.ToUpper();

                bool isLateralByName = familyName.Contains("MOMENT") ||
                                     familyName.Contains("LATERAL") ||
                                     typeName.Contains("MOMENT") ||
                                     typeName.Contains("LATERAL");

                if (isLateralByName)
                {
                    Debug.WriteLine($"Beam identified as lateral by name: {familyName} / {typeName}");
                }

                return isLateralByName;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking if beam is lateral: {ex.Message}");
                // Default to false if any error occurs
            }

            return false;
        }

        private bool IsBeamJoist(DB.FamilyInstance beam)
        {
            // Try to determine if beam is a joist
            try
            {
                // Check based on family and type name
                string familyName = beam.Symbol.FamilyName.ToUpper();
                string typeName = beam.Symbol.Name.ToUpper();

                return familyName.Contains("JOIST") || typeName.Contains("JOIST");
            }
            catch
            {
                // Default to false if any error occurs
            }

            return false;
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