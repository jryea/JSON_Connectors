using System.Linq;
using System.Diagnostics;
using Core.Models;

namespace Revit.Import
{
    /// <summary>
    /// Filters imported model based on user selections for element types and materials
    /// </summary>
    public class ImportModelFilter
    {
        private readonly ImportContext _context;

        public ImportModelFilter(ImportContext context)
        {
            _context = context;
        }

        public void FilterModel(BaseModel model)
        {
            Debug.WriteLine("ImportModelFilter: Starting model filtering");

            // Filter elements by category
            FilterElementsByCategory(model);

            // Filter elements by material
            FilterElementsByMaterial(model);

            // Clean up orphaned properties and references
            CleanupModel(model);

            Debug.WriteLine("ImportModelFilter: Filtering complete");
        }

        private void FilterElementsByCategory(BaseModel model)
        {
            if (model.Elements == null) return;

            // Filter each element type based on user selection
            if (!_context.ShouldImportElement("Grids"))
            {
                model.ModelLayout.Grids?.Clear();
            }

            if (!_context.ShouldImportElement("Beams"))
            {
                model.Elements.Beams?.Clear();
            }

            if (!_context.ShouldImportElement("Columns"))
            {
                model.Elements.Columns?.Clear();
            }

            if (!_context.ShouldImportElement("Braces"))
            {
                model.Elements.Braces?.Clear();
            }

            if (!_context.ShouldImportElement("Walls"))
            {
                model.Elements.Walls?.Clear();
            }

            if (!_context.ShouldImportElement("Floors"))
            {
                model.Elements.Floors?.Clear();
            }

            if (!_context.ShouldImportElement("Footings"))
            {
                model.Elements.IsolatedFootings?.Clear();
            }
        }

        private void FilterElementsByMaterial(BaseModel model)
        {
            if (model.Properties?.Materials == null) return;

            // Get material IDs to keep
            var materialsToKeep = model.Properties.Materials
                .Where(m => (_context.ShouldImportMaterial("Steel") && m.Type == Core.Models.MaterialType.Steel) ||
                           (_context.ShouldImportMaterial("Concrete") && m.Type == Core.Models.MaterialType.Concrete))
                .Select(m => m.Id)
                .ToHashSet();

            // Remove unwanted materials
            model.Properties.Materials = model.Properties.Materials
                .Where(m => materialsToKeep.Contains(m.Id))
                .ToList();

            // Remove properties that reference unwanted materials
            if (model.Properties.FrameProperties != null)
            {
                model.Properties.FrameProperties = model.Properties.FrameProperties
                    .Where(fp => string.IsNullOrEmpty(fp.MaterialId) || materialsToKeep.Contains(fp.MaterialId))
                    .ToList();
            }

            if (model.Properties.WallProperties != null)
            {
                model.Properties.WallProperties = model.Properties.WallProperties
                    .Where(wp => string.IsNullOrEmpty(wp.MaterialId) || materialsToKeep.Contains(wp.MaterialId))
                    .ToList();
            }

            if (model.Properties.FloorProperties != null)
            {
                model.Properties.FloorProperties = model.Properties.FloorProperties
                    .Where(fp => string.IsNullOrEmpty(fp.MaterialId) || materialsToKeep.Contains(fp.MaterialId))
                    .ToList();
            }

            // Remove elements that reference filtered-out frame properties
            FilterElementsByProperties(model);
        }

        private void FilterElementsByProperties(BaseModel model)
        {
            if (model.Elements == null) return;

            var validFramePropertyIds = model.Properties.FrameProperties?.Select(fp => fp.Id).ToHashSet() ?? new System.Collections.Generic.HashSet<string>();
            var validWallPropertyIds = model.Properties.WallProperties?.Select(wp => wp.Id).ToHashSet() ?? new System.Collections.Generic.HashSet<string>();
            var validFloorPropertyIds = model.Properties.FloorProperties?.Select(fp => fp.Id).ToHashSet() ?? new System.Collections.Generic.HashSet<string>();

            // Filter beams by frame properties
            if (model.Elements.Beams != null)
            {
                model.Elements.Beams = model.Elements.Beams
                    .Where(b => string.IsNullOrEmpty(b.FramePropertiesId) || validFramePropertyIds.Contains(b.FramePropertiesId))
                    .ToList();
            }

            // Filter columns by frame properties
            if (model.Elements.Columns != null)
            {
                model.Elements.Columns = model.Elements.Columns
                    .Where(c => string.IsNullOrEmpty(c.FramePropertiesId) || validFramePropertyIds.Contains(c.FramePropertiesId))
                    .ToList();
            }

            // Filter braces by frame properties
            if (model.Elements.Braces != null)
            {
                model.Elements.Braces = model.Elements.Braces
                    .Where(b => string.IsNullOrEmpty(b.FramePropertiesId) || validFramePropertyIds.Contains(b.FramePropertiesId))
                    .ToList();
            }

            // Filter walls by wall properties
            if (model.Elements.Walls != null)
            {
                model.Elements.Walls = model.Elements.Walls
                    .Where(w => string.IsNullOrEmpty(w.PropertiesId) || validWallPropertyIds.Contains(w.PropertiesId))
                    .ToList();
            }

            // Filter floors by floor properties
            if (model.Elements.Floors != null)
            {
                model.Elements.Floors = model.Elements.Floors
                    .Where(f => string.IsNullOrEmpty(f.FloorPropertiesId) || validFloorPropertyIds.Contains(f.FloorPropertiesId))
                    .ToList();
            }
        }

        private void CleanupModel(BaseModel model)
        {
            // Remove duplicate elements if any were created during filtering
            model.RemoveDuplicates();

            Debug.WriteLine("ImportModelFilter: Model cleanup complete");
        }
    }
}