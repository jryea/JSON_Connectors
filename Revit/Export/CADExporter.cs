// New file: Revit/Export/CADExporter.cs
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Revit.Export
{
    public class CADExporter
    {
        private readonly Document _doc;

        public CADExporter(Document doc)
        {
            _doc = doc;
        }

        public void ExportCADPlans(string folderPath)
        {
            try
            {
                // Ensure output directory exists
                Directory.CreateDirectory(folderPath);

                // Get all floor plan views
                var views = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View))
                    .WhereElementIsNotElementType()
                    .Cast<View>()
                    .Where(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate)
                    .ToList();

                // Setup DWG export options
                DWGExportOptions options = new DWGExportOptions();

                // Export each view to DWG
                foreach (View view in views)
                {
                    string sanitizedName = SanitizeFilename(view.Name);
                    string filename = Path.Combine(folderPath, sanitizedName + ".dwg");

                    try
                    {
                        // Create a new list with just this view's ID
                        ICollection<ElementId> viewIds = new List<ElementId> { view.Id };

                        // Export the view
                        _doc.Export(folderPath, sanitizedName + ".dwg", viewIds, options);

                        Debug.WriteLine($"Exported view {view.Name} to {filename}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error exporting view {view.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting CAD plans: {ex.Message}");
            }
        }

        private string SanitizeFilename(string filename)
        {
            // Replace invalid filename characters with underscore
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                filename = filename.Replace(c, '_');
            }

            // Trim to reasonable length if needed
            int maxLength = 64;
            if (filename.Length > maxLength)
            {
                filename = filename.Substring(0, maxLength);
            }

            return filename;
        }
    }
}