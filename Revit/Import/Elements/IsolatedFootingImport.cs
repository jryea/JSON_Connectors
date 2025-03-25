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
    /// Imports isolated footing elements from JSON into Revit
    /// </summary>
    public class IsolatedFootingImport
    {
        private readonly Document _doc;

        public IsolatedFootingImport(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Imports isolated footings from the JSON model into Revit
        /// </summary>
        /// <param name="footings">List of isolated footings to import</param>
        /// <returns>Number of footings imported</returns>
        public int Import(List<C.IsolatedFooting> footings)
        {
            int count = 0;

            // Find a default isolated footing family symbol
            FamilySymbol footingSymbol = FindIsolatedFootingSymbol();

            if (footingSymbol == null)
            {
                Debug.WriteLine("No isolated footing family available, cannot create footings");
                return 0;
            }

            // Ensure symbol is active
            if (!footingSymbol.IsActive)
            {
                footingSymbol.Activate();
            }

            foreach (var jsonFooting in footings)
            {
                try
                {
                    // Convert point coordinates
                    XYZ footingPoint = RevitTypeHelper.ConvertToRevitCoordinates(jsonFooting.Point);

                    // Create the isolated footing
                    FamilyInstance footing = _doc.Create.NewFamilyInstance(
                        footingPoint,
                        footingSymbol,
                        StructuralType.Footing);

                    count++;
                }
                catch (Exception ex)
                {
                    // Log the exception for this footing but continue with the next one
                    Debug.WriteLine($"Error creating isolated footing: {ex.Message}");
                }
            }

            return count;
        }

        /// <summary>
        /// Find an appropriate isolated footing family symbol
        /// </summary>
        private FamilySymbol FindIsolatedFootingSymbol()
        {
            // Try to find a family symbol for isolated footings
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            collector.OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFoundation);

            foreach (FamilySymbol symbol in collector)
            {
                // Look for symbols that might be isolated footings
                Family family = symbol.Family;
                if (family != null && (family.Name.Contains("Isolated") || family.Name.Contains("Footing")))
                {
                    return symbol;
                }
            }

            // If no specific isolated footing found, return the first foundation symbol
            return collector.FirstElement() as FamilySymbol;
        }
    }
}