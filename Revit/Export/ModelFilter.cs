using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Core.Models;

namespace Revit.Export
{
    /// <summary>
    /// Post-processing filter for complete structural model
    /// Clean, predictable filtering after all elements are built
    /// </summary>
    public class ModelFilter
    {
        private readonly ExportContext _context;

        public ModelFilter(ExportContext context)
        {
            _context = context;
        }

        public void FilterModel(BaseModel model)
        {
            Debug.WriteLine("ModelFilter: Starting model filtering");

            if (_context.SelectedLevelIds == null || _context.SelectedLevelIds.Count == 0)
            {
                Debug.WriteLine("ModelFilter: No level filtering required");
                return;
            }

            // Get model level IDs that correspond to selected Revit levels
            var selectedModelLevelIds = new HashSet<string>();
            foreach (var revitLevelId in _context.SelectedLevelIds)
            {
                var modelLevelId = _context.GetModelLevelId(revitLevelId);
                if (!string.IsNullOrEmpty(modelLevelId))
                {
                    selectedModelLevelIds.Add(modelLevelId);
                }
            }

            // Get base level ID to exclude
            string baseLevelModelId = null;
            if (_context.BaseLevelId != null)
            {
                baseLevelModelId = _context.GetModelLevelId(_context.BaseLevelId);
            }

            Debug.WriteLine($"ModelFilter: Selected levels: {selectedModelLevelIds.Count}, Base level: {baseLevelModelId}");

            // Filter model components
            FilterLevels(model, selectedModelLevelIds, baseLevelModelId);
            FilterFloorTypes(model);
            FilterElements(model, selectedModelLevelIds, baseLevelModelId);
            CleanupOrphanedProperties(model);

            Debug.WriteLine("ModelFilter: Filtering complete");
        }

        private void FilterLevels(BaseModel model, HashSet<string> selectedLevelIds, string baseLevelId)
        {
            if (model.ModelLayout?.Levels == null) return;

            int initialCount = model.ModelLayout.Levels.Count;

            model.ModelLayout.Levels = model.ModelLayout.Levels
                .Where(level => selectedLevelIds.Contains(level.Id))
                .ToList();

            Debug.WriteLine($"ModelFilter: Levels {initialCount} -> {model.ModelLayout.Levels.Count}");
        }

        private void FilterElements(BaseModel model, HashSet<string> selectedLevelIds, string baseLevelId)
        {
            if (model.Elements == null) return;

            FilterWalls(model, selectedLevelIds, baseLevelId);
            FilterFloors(model, selectedLevelIds, baseLevelId);
            FilterColumns(model, selectedLevelIds, baseLevelId);
            FilterBeams(model, selectedLevelIds, baseLevelId);
            FilterBraces(model, selectedLevelIds, baseLevelId);
            FilterFootings(model, selectedLevelIds, baseLevelId);
        }

        private void FilterWalls(BaseModel model, HashSet<string> selectedLevelIds, string baseLevelId)
        {
            if (model.Elements.Walls == null) return;

            int initialCount = model.Elements.Walls.Count;

            model.Elements.Walls = model.Elements.Walls
                .Where(wall => ShouldKeepElementByTopLevel(wall.TopLevelId, selectedLevelIds, baseLevelId))
                .ToList();

            Debug.WriteLine($"ModelFilter: Walls {initialCount} -> {model.Elements.Walls.Count}");
        }

        private void FilterFloors(BaseModel model, HashSet<string> selectedLevelIds, string baseLevelId)
        {
            if (model.Elements.Floors == null) return;

            int initialCount = model.Elements.Floors.Count;

            model.Elements.Floors = model.Elements.Floors
                .Where(floor => ShouldKeepElementByLevel(floor.LevelId, selectedLevelIds, baseLevelId))
                .ToList();

            Debug.WriteLine($"ModelFilter: Floors {initialCount} -> {model.Elements.Floors.Count}");
        }

        private void FilterColumns(BaseModel model, HashSet<string> selectedLevelIds, string baseLevelId)
        {
            if (model.Elements.Columns == null) return;

            int initialCount = model.Elements.Columns.Count;

            // Keep columns where top level is in selected levels and not base level
            model.Elements.Columns = model.Elements.Columns
                .Where(column => ShouldKeepElementByTopLevel(column.TopLevelId, selectedLevelIds, baseLevelId))
                .ToList();

            Debug.WriteLine($"ModelFilter: Columns {initialCount} -> {model.Elements.Columns.Count}");
        }

        private void FilterBeams(BaseModel model, HashSet<string> selectedLevelIds, string baseLevelId)
        {
            if (model.Elements.Beams == null) return;

            int initialCount = model.Elements.Beams.Count;

            model.Elements.Beams = model.Elements.Beams
                .Where(beam => ShouldKeepElementByLevel(beam.LevelId, selectedLevelIds, baseLevelId))
                .ToList();

            Debug.WriteLine($"ModelFilter: Beams {initialCount} -> {model.Elements.Beams.Count}");
        }

        private void FilterBraces(BaseModel model, HashSet<string> selectedLevelIds, string baseLevelId)
        {
            if (model.Elements.Braces == null) return;

            int initialCount = model.Elements.Braces.Count;

            model.Elements.Braces = model.Elements.Braces
                .Where(brace => ShouldKeepElementByTopLevel(brace.TopLevelId, selectedLevelIds, baseLevelId))
                .ToList();

            Debug.WriteLine($"ModelFilter: Braces {initialCount} -> {model.Elements.Braces.Count}");
        }

        private void FilterFootings(BaseModel model, HashSet<string> selectedLevelIds, string baseLevelId)
        {
            if (model.Elements.IsolatedFootings == null) return;

            int initialCount = model.Elements.IsolatedFootings.Count;

            model.Elements.IsolatedFootings = model.Elements.IsolatedFootings
                .Where(footing => ShouldKeepElementByLevel(footing.LevelId, selectedLevelIds, baseLevelId))
                .ToList();

            Debug.WriteLine($"ModelFilter: Footings {initialCount} -> {model.Elements.IsolatedFootings.Count}");
        }

        private bool ShouldKeepElementByLevel(string levelId, HashSet<string> selectedLevelIds, string baseLevelId)
        {
            if (string.IsNullOrEmpty(levelId)) return false;

            // Exclude if it's the base level
            if (!string.IsNullOrEmpty(baseLevelId) && levelId == baseLevelId) return false;

            // Include if no level filtering or level is selected
            return selectedLevelIds.Count == 0 || selectedLevelIds.Contains(levelId);
        }

        private bool ShouldKeepElementByTopLevel(string topLevelId, HashSet<string> selectedLevelIds, string baseLevelId)
        {
            return ShouldKeepElementByLevel(topLevelId, selectedLevelIds, baseLevelId);
        }

        private void CleanupOrphanedProperties(BaseModel model)
        {
            // Remove properties that are no longer referenced by any elements
            // This could be implemented to clean up unused materials, frame properties, etc.
            // For now, keep all properties as they might be needed
            Debug.WriteLine("ModelFilter: Property cleanup skipped (keeping all properties)");
        }

        private void FilterFloorTypes(BaseModel model)
        {
            if (model.ModelLayout?.FloorTypes == null || model.ModelLayout?.Levels == null) return;

            int initialCount = model.ModelLayout.FloorTypes.Count;

            // Get FloorType IDs that are referenced by remaining levels
            var referencedFloorTypeIds = new HashSet<string>();
            foreach (var level in model.ModelLayout.Levels)
            {
                if (!string.IsNullOrEmpty(level.FloorTypeId))
                {
                    referencedFloorTypeIds.Add(level.FloorTypeId);
                }
            }

            // Keep only referenced FloorTypes
            model.ModelLayout.FloorTypes = model.ModelLayout.FloorTypes
                .Where(ft => referencedFloorTypeIds.Contains(ft.Id))
                .ToList();

            Debug.WriteLine($"ModelFilter: FloorTypes {initialCount} -> {model.ModelLayout.FloorTypes.Count}");
        }
    }
}