using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using C = Core.Models.Elements;
using Revit.Utils;

namespace Revit.Import.Elements
{
    /// <summary>
    /// Imports beam elements from JSON into Revit
    /// </summary>
    public class BeamImport
    {
        private readonly Document _doc;

        public BeamImport(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Imports beams from the JSON model into Revit
        /// </summary>
        /// <param name="beams">List of beams to import</param>
        /// <param name="levelIdMap">Dictionary of level ID mappings</param>
        /// <param name="framePropertyIdMap">Dictionary of frame property ID mappings</param>
        /// <returns>Number of beams imported</returns>
        public int Import(List<C.Beam> beams, Dictionary<string, ElementId> levelIdMap, Dictionary<string, ElementId> framePropertyIdMap)
        {
            int count = 0;

            foreach (var jsonBeam in beams)
            {
                try
                {
                    // Get the level for this beam
                    ElementId levelId = RevitTypeHelper.GetElementId(levelIdMap, jsonBeam.LevelId, "Level");

                    // Get family type for this beam (from frame properties)
                    ElementId familyTypeId = ElementId.InvalidElementId;
                    if (!string.IsNullOrEmpty(jsonBeam.FramePropertiesId) && framePropertyIdMap.ContainsKey(jsonBeam.FramePropertiesId))
                    {
                        familyTypeId = framePropertyIdMap[jsonBeam.FramePropertiesId];
                    }

                    // Create curve for beam
                    XYZ startPoint = RevitTypeHelper.ConvertToRevitCoordinates(jsonBeam.StartPoint);
                    XYZ endPoint = RevitTypeHelper.ConvertToRevitCoordinates(jsonBeam.EndPoint);
                    Line beamLine = Line.CreateBound(startPoint, endPoint);

                    // Create the structural beam
                    FamilyInstance beam = _doc.Create.NewFamilyInstance(
                        beamLine,
                        familySymbol,
                        level,
                        StructuralType.Beam);

                    // Set beam properties
                    if (jsonBeam.IsLateral)
                    {
                        // Set parameter for lateral system if available
                        Parameter lateralParam = beam.LookupParameter("IsLateral");
                        if (lateralParam != null && lateralParam.StorageType == StorageType.Integer)
                        {
                            lateralParam.Set(1);
                        }
                    }

                    count++;
                }
                catch (Exception ex)
                {
                    // Log the exception for this beam but continue with the next one
                    Debug.WriteLine($"Error creating beam: {ex.Message}");
                }
            }

            return count;
        }
    }
}