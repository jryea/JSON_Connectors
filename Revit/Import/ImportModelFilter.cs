using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using Core.Models;
using Core.Utilities;

namespace Revit.Import
{
    /// <summary>
    /// Filters imported model based on user selections for element types and materials using Core utilities
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

            // Filter elements by material using Core utilities
            FilterElementsByMaterial(model);

            // Clean up model - preserve properties for potential reuse (no property cleanup)
            CleanupModel(model);

            Debug.WriteLine("ImportModelFilter: Filtering complete");
        }

        private void FilterElementsByCategory(BaseModel model)
        {
            if (model.Elements == null) return;

            // Filter grids
            if (!_context.ShouldImportElement("Grids") && model.ModelLayout?.Grids != null)
            {
                model.ModelLayout.Grids.Clear();
                Debug.WriteLine("ImportModelFilter: Grids filtered out");
            }

            // Filter beams using Core utility
            if (!_context.ShouldImportElement("Beams") && model.Elements.Beams != null)
            {
                int initialCount = model.Elements.Beams.Count;
                model.Elements.Beams.Clear();
                Debug.WriteLine($"ImportModelFilter: Beams filtered out {initialCount} -> 0");
            }

            // Filter columns using Core utility
            if (!_context.ShouldImportElement("Columns") && model.Elements.Columns != null)
            {
                int initialCount = model.Elements.Columns.Count;
                model.Elements.Columns.Clear();
                Debug.WriteLine($"ImportModelFilter: Columns filtered out {initialCount} -> 0");
            }

            // Filter braces using Core utility
            if (!_context.ShouldImportElement("Braces") && model.Elements.Braces != null)
            {
                int initialCount = model.Elements.Braces.Count;
                model.Elements.Braces.Clear();
                Debug.WriteLine($"ImportModelFilter: Braces filtered out {initialCount} -> 0");
            }

            // Filter walls using Core utility
            if (!_context.ShouldImportElement("Walls") && model.Elements.Walls != null)
            {
                int initialCount = model.Elements.Walls.Count;
                model.Elements.Walls.Clear();
                Debug.WriteLine($"ImportModelFilter: Walls filtered out {initialCount} -> 0");
            }

            // Filter floors using Core utility
            if (!_context.ShouldImportElement("Floors") && model.Elements.Floors != null)
            {
                int initialCount = model.Elements.Floors.Count;
                model.Elements.Floors.Clear();
                Debug.WriteLine($"ImportModelFilter: Floors filtered out {initialCount} -> 0");
            }

            // Filter footings using Core utility
            if (!_context.ShouldImportElement("Footings") && model.Elements.IsolatedFootings != null)
            {
                int initialCount = model.Elements.IsolatedFootings.Count;
                model.Elements.IsolatedFootings.Clear();
                Debug.WriteLine($"ImportModelFilter: Footings filtered out {initialCount} -> 0");
            }
        }

        private void FilterElementsByMaterial(BaseModel model)
        {
            if (model.Properties?.Materials == null) return;

            // Determine which material types to remove based on user selections
            var materialTypesToRemove = new HashSet<MaterialType>();

            if (!_context.ShouldImportMaterial("Steel"))
            {
                materialTypesToRemove.Add(MaterialType.Steel);
            }

            if (!_context.ShouldImportMaterial("Concrete"))
            {
                materialTypesToRemove.Add(MaterialType.Concrete);
            }

            // If no materials are being filtered out, skip this step
            if (materialTypesToRemove.Count == 0)
            {
                Debug.WriteLine("ImportModelFilter: No material filtering required");
                return;
            }

            Debug.WriteLine($"ImportModelFilter: Filtering out material types: {string.Join(", ", materialTypesToRemove)}");

            // Use Core utility to filter elements by material type
            // This will remove elements that use the unwanted material types
            Core.Utilities.ModelFilter.FilterElementsByMaterialType(model, materialTypesToRemove);
        }

        /// <summary>
        /// Alternative manual approach for material filtering if Core utility doesn't meet specific needs
        /// This method demonstrates how the old logic could be simplified using standard LINQ
        /// </summary>
        private void FilterMaterialsAndProperties(BaseModel model, HashSet<MaterialType> materialTypesToRemove)
        {
            // Get material IDs to remove
            var materialIdsToRemove = new HashSet<string>();
            if (model.Properties.Materials != null)
            {
                foreach (var material in model.Properties.Materials)
                {
                    if (materialTypesToRemove.Contains(material.Type))
                    {
                        materialIdsToRemove.Add(material.Id);
                    }
                }
            }

            // Filter materials using simplified LINQ
            if (model.Properties.Materials != null)
            {
                int initialCount = model.Properties.Materials.Count;
                model.Properties.Materials = model.Properties.Materials
                    .Where(m => !materialIdsToRemove.Contains(m.Id))
                    .ToList();
                Debug.WriteLine($"ImportModelFilter: Materials {initialCount} -> {model.Properties.Materials.Count}");
            }

            // Filter frame properties using simplified LINQ
            if (model.Properties.FrameProperties != null)
            {
                int initialCount = model.Properties.FrameProperties.Count;
                model.Properties.FrameProperties = model.Properties.FrameProperties
                    .Where(fp => string.IsNullOrEmpty(fp.MaterialId) || !materialIdsToRemove.Contains(fp.MaterialId))
                    .ToList();
                Debug.WriteLine($"ImportModelFilter: FrameProperties {initialCount} -> {model.Properties.FrameProperties.Count}");
            }

            // Filter wall properties using simplified LINQ
            if (model.Properties.WallProperties != null)
            {
                int initialCount = model.Properties.WallProperties.Count;
                model.Properties.WallProperties = model.Properties.WallProperties
                    .Where(wp => string.IsNullOrEmpty(wp.MaterialId) || !materialIdsToRemove.Contains(wp.MaterialId))
                    .ToList();
                Debug.WriteLine($"ImportModelFilter: WallProperties {initialCount} -> {model.Properties.WallProperties.Count}");
            }

            // Filter floor properties using simplified LINQ
            if (model.Properties.FloorProperties != null)
            {
                int initialCount = model.Properties.FloorProperties.Count;
                model.Properties.FloorProperties = model.Properties.FloorProperties
                    .Where(fp => string.IsNullOrEmpty(fp.MaterialId) || !materialIdsToRemove.Contains(fp.MaterialId))
                    .ToList();
                Debug.WriteLine($"ImportModelFilter: FloorProperties {initialCount} -> {model.Properties.FloorProperties.Count}");
            }

            // Get valid property IDs after filtering
            var validFramePropertyIds = model.Properties.FrameProperties?.Select(fp => fp.Id).ToHashSet() ?? new HashSet<string>();
            var validWallPropertyIds = model.Properties.WallProperties?.Select(wp => wp.Id).ToHashSet() ?? new HashSet<string>();
            var validFloorPropertyIds = model.Properties.FloorProperties?.Select(fp => fp.Id).ToHashSet() ?? new HashSet<string>();

            // Filter elements by valid properties using simplified logic
            FilterElementsByValidProperties(model, validFramePropertyIds, validWallPropertyIds, validFloorPropertyIds);
        }

        private void FilterElementsByValidProperties(BaseModel model,
            HashSet<string> validFramePropertyIds,
            HashSet<string> validWallPropertyIds,
            HashSet<string> validFloorPropertyIds)
        {
            // Filter beams by frame properties using simplified LINQ
            if (model.Elements.Beams != null)
            {
                int initialCount = model.Elements.Beams.Count;
                model.Elements.Beams = model.Elements.Beams
                    .Where(b => string.IsNullOrEmpty(b.FramePropertiesId) || validFramePropertyIds.Contains(b.FramePropertiesId))
                    .ToList();
                Debug.WriteLine($"ImportModelFilter: Beams by properties {initialCount} -> {model.Elements.Beams.Count}");
            }

            // Filter columns by frame properties using simplified LINQ
            if (model.Elements.Columns != null)
            {
                int initialCount = model.Elements.Columns.Count;
                model.Elements.Columns = model.Elements.Columns
                    .Where(c => string.IsNullOrEmpty(c.FramePropertiesId) || validFramePropertyIds.Contains(c.FramePropertiesId))
                    .ToList();
                Debug.WriteLine($"ImportModelFilter: Columns by properties {initialCount} -> {model.Elements.Columns.Count}");
            }

            // Filter braces by frame properties using simplified LINQ
            if (model.Elements.Braces != null)
            {
                int initialCount = model.Elements.Braces.Count;
                model.Elements.Braces = model.Elements.Braces
                    .Where(b => string.IsNullOrEmpty(b.FramePropertiesId) || validFramePropertyIds.Contains(b.FramePropertiesId))
                    .ToList();
                Debug.WriteLine($"ImportModelFilter: Braces by properties {initialCount} -> {model.Elements.Braces.Count}");
            }

            // Filter walls by wall properties using simplified LINQ
            if (model.Elements.Walls != null)
            {
                int initialCount = model.Elements.Walls.Count;
                model.Elements.Walls = model.Elements.Walls
                    .Where(w => string.IsNullOrEmpty(w.PropertiesId) || validWallPropertyIds.Contains(w.PropertiesId))
                    .ToList();
                Debug.WriteLine($"ImportModelFilter: Walls by properties {initialCount} -> {model.Elements.Walls.Count}");
            }

            // Filter floors by floor properties using simplified LINQ
            if (model.Elements.Floors != null)
            {
                int initialCount = model.Elements.Floors.Count;
                model.Elements.Floors = model.Elements.Floors
                    .Where(f => string.IsNullOrEmpty(f.FloorPropertiesId) || validFloorPropertyIds.Contains(f.FloorPropertiesId))
                    .ToList();
                Debug.WriteLine($"ImportModelFilter: Floors by properties {initialCount} -> {model.Elements.Floors.Count}");
            }
        }

        private void CleanupModel(BaseModel model)
        {
            // Remove duplicate elements if any were created during filtering
            model.RemoveDuplicates();

            // Clean up unused properties for Import processing
            Core.Utilities.ModelFilter.RemoveUnusedProperties(model);

            Debug.WriteLine("ImportModelFilter: Model cleanup complete");
        }
    }
}