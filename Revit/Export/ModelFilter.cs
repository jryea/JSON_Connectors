using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Core.Models;

namespace Revit.Export
{
    /// <summary>
    /// Post-processing filter for complete structural model - cleaned up version
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

            // Filter model components - simplified and consolidated
            FilterLevels(model, selectedModelLevelIds);
            FilterElementsByLevel(model, selectedModelLevelIds, baseLevelModelId);
            FilterFloorTypes(model);

            // Use Core utility for property cleanup
            Core.Utilities.ModelFilter.RemoveUnusedProperties(model);

            Debug.WriteLine("ModelFilter: Filtering complete");
        }

        private void FilterLevels(BaseModel model, HashSet<string> selectedLevelIds)
        {
            if (model.ModelLayout?.Levels == null) return;

            int initialCount = model.ModelLayout.Levels.Count;

            // Use simplified LINQ filtering - cleaner than the original logic
            model.ModelLayout.Levels = model.ModelLayout.Levels
                .Where(level => selectedLevelIds.Contains(level.Id))
                .ToList();

            Debug.WriteLine($"ModelFilter: Levels {initialCount} -> {model.ModelLayout.Levels.Count}");
        }

        private void FilterElementsByLevel(BaseModel model, HashSet<string> selectedLevelIds, string baseLevelId)
        {
            if (model.Elements == null) return;

            // Filter walls by top level - simplified logic
            if (model.Elements.Walls != null)
            {
                int initialCount = model.Elements.Walls.Count;
                model.Elements.Walls = model.Elements.Walls
                    .Where(wall => ShouldKeepElementByTopLevel(wall.TopLevelId, selectedLevelIds, baseLevelId))
                    .ToList();
                Debug.WriteLine($"ModelFilter: Walls {initialCount} -> {model.Elements.Walls.Count}");
            }

            // Filter floors by level - simplified logic  
            if (model.Elements.Floors != null)
            {
                int initialCount = model.Elements.Floors.Count;
                model.Elements.Floors = model.Elements.Floors
                    .Where(floor => ShouldKeepElementByLevel(floor.LevelId, selectedLevelIds, baseLevelId))
                    .ToList();
                Debug.WriteLine($"ModelFilter: Floors {initialCount} -> {model.Elements.Floors.Count}");
            }

            // Filter columns by top level - simplified logic
            if (model.Elements.Columns != null)
            {
                int initialCount = model.Elements.Columns.Count;
                model.Elements.Columns = model.Elements.Columns
                    .Where(column => ShouldKeepElementByTopLevel(column.TopLevelId, selectedLevelIds, baseLevelId))
                    .ToList();
                Debug.WriteLine($"ModelFilter: Columns {initialCount} -> {model.Elements.Columns.Count}");
            }

            // Filter beams by level - simplified logic
            if (model.Elements.Beams != null)
            {
                int initialCount = model.Elements.Beams.Count;
                model.Elements.Beams = model.Elements.Beams
                    .Where(beam => ShouldKeepElementByLevel(beam.LevelId, selectedLevelIds, baseLevelId))
                    .ToList();
                Debug.WriteLine($"ModelFilter: Beams {initialCount} -> {model.Elements.Beams.Count}");
            }

            // Filter braces by top level - simplified logic
            if (model.Elements.Braces != null)
            {
                int initialCount = model.Elements.Braces.Count;
                model.Elements.Braces = model.Elements.Braces
                    .Where(brace => ShouldKeepElementByTopLevel(brace.TopLevelId, selectedLevelIds, baseLevelId))
                    .ToList();
                Debug.WriteLine($"ModelFilter: Braces {initialCount} -> {model.Elements.Braces.Count}");
            }

            // Filter isolated footings by level - simplified logic
            if (model.Elements.IsolatedFootings != null)
            {
                int initialCount = model.Elements.IsolatedFootings.Count;
                model.Elements.IsolatedFootings = model.Elements.IsolatedFootings
                    .Where(footing => ShouldKeepElementByLevel(footing.LevelId, selectedLevelIds, baseLevelId))
                    .ToList();
                Debug.WriteLine($"ModelFilter: Footings {initialCount} -> {model.Elements.IsolatedFootings.Count}");
            }
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

            // Use simplified LINQ filtering instead of Core utility for now
            model.ModelLayout.FloorTypes = model.ModelLayout.FloorTypes
                .Where(ft => referencedFloorTypeIds.Contains(ft.Id))
                .ToList();

            Debug.WriteLine($"ModelFilter: FloorTypes {initialCount} -> {model.ModelLayout.FloorTypes.Count}");
        }
    }
}