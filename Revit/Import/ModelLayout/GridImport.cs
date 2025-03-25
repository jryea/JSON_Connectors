using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.DB;
using C = Core.Models.ModelLayout;
using Revit.Utils;

namespace Revit.Import.ModelLayout
{
    /// <summary>
    /// Imports grid elements from JSON into Revit
    /// </summary>
    public class GridImport
    {
        private readonly Document _doc;

        public GridImport(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Imports grids from the JSON model into Revit
        /// </summary>
        /// <param name="grids">List of grids to import</param>
        /// <param name="gridIdMap">Dictionary to store mappings of JSON IDs to Revit ElementIds</param>
        /// <returns>Number of grids imported</returns>
        public int Import(List<C.Grid> grids, Dictionary<string, ElementId> gridIdMap)
        {
            int count = 0;

            foreach (var jsonGrid in grids)
            {
                try
                {
                    // Convert JSON grid points to Revit XYZ
                    XYZ startPoint = RevitTypeHelper.ConvertToRevitCoordinates(jsonGrid.StartPoint);
                    XYZ endPoint = RevitTypeHelper.ConvertToRevitCoordinates(jsonGrid.EndPoint);

                    // Create line for grid
                    Line gridLine = Line.CreateBound(startPoint, endPoint);

                    // Create grid in Revit
                    Autodesk.Revit.DB.Grid revitGrid = Autodesk.Revit.DB.Grid.Create(_doc, gridLine);

                    // Set grid name
                    revitGrid.Name = jsonGrid.Name;

                    // Apply bubble visibility if specified in JSON
                    if (jsonGrid.StartPoint.IsBubble)
                    {
                        revitGrid.ShowBubbleInView(DatumEnds.End0, _doc.ActiveView);
                    }
                    else
                    {
                        revitGrid.HideBubbleInView(DatumEnds.End0, _doc.ActiveView);
                    }

                    if (jsonGrid.EndPoint.IsBubble)
                    {
                        revitGrid.ShowBubbleInView(DatumEnds.End1, _doc.ActiveView);
                    }
                    else
                    {
                        revitGrid.HideBubbleInView(DatumEnds.End1, _doc.ActiveView);
                    }

                    // Store the mapping from JSON ID to Revit ElementId
                    gridIdMap[jsonGrid.Id] = revitGrid.Id;

                    count++;
                }
                catch (Exception ex)
                {
                    // Log the exception for this grid but continue with the next one
                    Debug.WriteLine($"Error creating grid {jsonGrid.Name}: {ex.Message}");
                }
            }

            return count;
        }
    }
}