using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Elements;
using Core.Models.Properties;

namespace Core.Utilities
{
    /// <summary>
    /// Provides filtering capabilities for BaseModel objects
    /// </summary>
    public static class ModelFilter
    {
        /// <summary>
        /// Filters elements by predicate
        /// </summary>
        /// <param name="model">The model to filter</param>
        /// <param name="predicate">Function that returns true for elements to keep</param>
        public static void FilterElements<T>(BaseModel model, Func<T, bool> predicate) where T : IIdentifiable
        {
            if (model?.Elements == null || predicate == null) return;

            var elementType = typeof(T);

            if (elementType == typeof(Beam))
                model.Elements.Beams = model.Elements.Beams?.Where(e => predicate((T)(object)e)).ToList();
            else if (elementType == typeof(Column))
                model.Elements.Columns = model.Elements.Columns?.Where(e => predicate((T)(object)e)).ToList();
            else if (elementType == typeof(Wall))
                model.Elements.Walls = model.Elements.Walls?.Where(e => predicate((T)(object)e)).ToList();
            else if (elementType == typeof(Floor))
                model.Elements.Floors = model.Elements.Floors?.Where(e => predicate((T)(object)e)).ToList();
            else if (elementType == typeof(Brace))
                model.Elements.Braces = model.Elements.Braces?.Where(e => predicate((T)(object)e)).ToList();
            else if (elementType == typeof(IsolatedFooting))
                model.Elements.IsolatedFootings = model.Elements.IsolatedFootings?.Where(e => predicate((T)(object)e)).ToList();
            else if (elementType == typeof(ContinuousFooting))
                model.Elements.ContinuousFootings = model.Elements.ContinuousFootings?.Where(e => predicate((T)(object)e)).ToList();
            else if (elementType == typeof(Pile))
                model.Elements.Piles = model.Elements.Piles?.Where(e => predicate((T)(object)e)).ToList();
            else if (elementType == typeof(Pier))
                model.Elements.Piers = model.Elements.Piers?.Where(e => predicate((T)(object)e)).ToList();
            else if (elementType == typeof(DrilledPier))
                model.Elements.DrilledPiers = model.Elements.DrilledPiers?.Where(e => predicate((T)(object)e)).ToList();
            else if (elementType == typeof(Joint))
                model.Elements.Joints = model.Elements.Joints?.Where(e => predicate((T)(object)e)).ToList();
            else if (elementType == typeof(Opening))
                model.Elements.Openings = model.Elements.Openings?.Where(e => predicate((T)(object)e)).ToList();
        }

        /// <summary>
        /// Filters elements by material type (removes elements whose properties reference materials of specified types)
        /// </summary>
        /// <param name="model">The model to filter</param>
        /// <param name="materialTypesToRemove">Set of material types to filter out</param>
        public static void FilterElementsByMaterialType(BaseModel model, HashSet<MaterialType> materialTypesToRemove)
        {
            if (model?.Elements == null || model?.Properties == null || materialTypesToRemove == null || materialTypesToRemove.Count == 0)
                return;

            // Create lookup dictionaries for performance
            var materialTypeById = model.Properties.Materials?.ToDictionary(m => m.Id, m => m.Type) ?? new Dictionary<string, MaterialType>();
            var framePropertyMaterialIds = model.Properties.FrameProperties?.ToDictionary(fp => fp.Id, fp => fp.MaterialId) ?? new Dictionary<string, string>();
            var wallPropertyMaterialIds = model.Properties.WallProperties?.ToDictionary(wp => wp.Id, wp => wp.MaterialId) ?? new Dictionary<string, string>();
            var floorPropertyMaterialIds = model.Properties.FloorProperties?.ToDictionary(fp => fp.Id, fp => fp.MaterialId) ?? new Dictionary<string, string>();

            // Helper function to check if an element should be removed based on material type
            bool ShouldRemoveByMaterialType(string propertyId, Dictionary<string, string> propertyToMaterialMap)
            {
                if (string.IsNullOrEmpty(propertyId) || !propertyToMaterialMap.TryGetValue(propertyId, out string materialId))
                    return false;

                if (string.IsNullOrEmpty(materialId) || !materialTypeById.TryGetValue(materialId, out MaterialType materialType))
                    return false;

                return materialTypesToRemove.Contains(materialType);
            }

            // Filter frame elements (beams, columns, braces)
            if (model.Elements.Beams != null)
            {
                model.Elements.Beams = model.Elements.Beams
                    .Where(b => !ShouldRemoveByMaterialType(b.FramePropertiesId, framePropertyMaterialIds))
                    .ToList();
            }

            if (model.Elements.Columns != null)
            {
                model.Elements.Columns = model.Elements.Columns
                    .Where(c => !ShouldRemoveByMaterialType(c.FramePropertiesId, framePropertyMaterialIds))
                    .ToList();
            }

            if (model.Elements.Braces != null)
            {
                model.Elements.Braces = model.Elements.Braces
                    .Where(b => !ShouldRemoveByMaterialType(b.FramePropertiesId, framePropertyMaterialIds))
                    .ToList();
            }

            // Filter wall elements
            if (model.Elements.Walls != null)
            {
                model.Elements.Walls = model.Elements.Walls
                    .Where(w => !ShouldRemoveByMaterialType(w.PropertiesId, wallPropertyMaterialIds))
                    .ToList();
            }

            // Filter floor elements
            if (model.Elements.Floors != null)
            {
                model.Elements.Floors = model.Elements.Floors
                    .Where(f => !ShouldRemoveByMaterialType(f.FloorPropertiesId, floorPropertyMaterialIds))
                    .ToList();
            }
        }

        /// <summary>
        /// Removes properties that are no longer referenced by any elements (optional cleanup)
        /// </summary>
        /// <param name="model">The model to clean up</param>
        public static void RemoveUnusedProperties(BaseModel model)
        {
            if (model?.Elements == null || model?.Properties == null) return;

            // Collect all property IDs that are still in use
            var usedFramePropertyIds = new HashSet<string>();
            var usedWallPropertyIds = new HashSet<string>();
            var usedFloorPropertyIds = new HashSet<string>();
            var usedMaterialIds = new HashSet<string>();

            // Collect frame property IDs from elements
            model.Elements.Beams?.Where(b => !string.IsNullOrEmpty(b.FramePropertiesId))
                .ToList().ForEach(b => usedFramePropertyIds.Add(b.FramePropertiesId));

            model.Elements.Columns?.Where(c => !string.IsNullOrEmpty(c.FramePropertiesId))
                .ToList().ForEach(c => usedFramePropertyIds.Add(c.FramePropertiesId));

            model.Elements.Braces?.Where(b => !string.IsNullOrEmpty(b.FramePropertiesId))
                .ToList().ForEach(b => usedFramePropertyIds.Add(b.FramePropertiesId));

            // Collect wall property IDs from elements
            model.Elements.Walls?.Where(w => !string.IsNullOrEmpty(w.PropertiesId))
                .ToList().ForEach(w => usedWallPropertyIds.Add(w.PropertiesId));

            // Collect floor property IDs from elements
            model.Elements.Floors?.Where(f => !string.IsNullOrEmpty(f.FloorPropertiesId))
                .ToList().ForEach(f => usedFloorPropertyIds.Add(f.FloorPropertiesId));

            // Remove unused properties
            if (model.Properties.FrameProperties != null)
            {
                model.Properties.FrameProperties = model.Properties.FrameProperties
                    .Where(fp => usedFramePropertyIds.Contains(fp.Id))
                    .ToList();
            }

            if (model.Properties.WallProperties != null)
            {
                model.Properties.WallProperties = model.Properties.WallProperties
                    .Where(wp => usedWallPropertyIds.Contains(wp.Id))
                    .ToList();
            }

            if (model.Properties.FloorProperties != null)
            {
                model.Properties.FloorProperties = model.Properties.FloorProperties
                    .Where(fp => usedFloorPropertyIds.Contains(fp.Id))
                    .ToList();
            }

            // Collect material IDs from remaining properties
            model.Properties.FrameProperties?.Where(fp => !string.IsNullOrEmpty(fp.MaterialId))
                .ToList().ForEach(fp => usedMaterialIds.Add(fp.MaterialId));

            model.Properties.WallProperties?.Where(wp => !string.IsNullOrEmpty(wp.MaterialId))
                .ToList().ForEach(wp => usedMaterialIds.Add(wp.MaterialId));

            model.Properties.FloorProperties?.Where(fp => !string.IsNullOrEmpty(fp.MaterialId))
                .ToList().ForEach(fp => usedMaterialIds.Add(fp.MaterialId));

            // Remove unused materials
            if (model.Properties.Materials != null)
            {
                model.Properties.Materials = model.Properties.Materials
                    .Where(m => usedMaterialIds.Contains(m.Id))
                    .ToList();
            }
        }
    }
}