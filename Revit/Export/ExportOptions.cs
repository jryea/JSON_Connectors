using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

// Consistent aliases matching existing codebase pattern
using CL = Core.Models.ModelLayout;

namespace Revit.Export
{
    /// <summary>
    /// Export format enumeration
    /// </summary>
    public enum ExportFormat
    {
        ETABS,
        RAM,
        Grasshopper
    }

    /// <summary>
    /// Comprehensive options for the unified export process
    /// </summary>
    public class ExportOptions
    {
        /// <summary>
        /// Output file path (with appropriate extension for the target format)
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// Target export format
        /// </summary>
        public ExportFormat Format { get; set; }

        /// <summary>
        /// Revit level element IDs to include in export (null = all levels)
        /// </summary>
        public List<ElementId> SelectedLevels { get; set; }

        /// <summary>
        /// Base level for transformation (elevations will be adjusted relative to this level)
        /// </summary>
        public Autodesk.Revit.DB.Level BaseLevel { get; set; }

        /// <summary>
        /// Rotation angle in degrees (positive = counterclockwise)
        /// </summary>
        public double RotationAngle { get; set; } = 0.0;

        /// <summary>
        /// Custom floor types (for RAM/Grasshopper exports)
        /// </summary>
        public List<CL.FloorType> CustomFloorTypes { get; set; }

        /// <summary>
        /// Custom levels with floor type assignments (for RAM/Grasshopper exports)
        /// </summary>
        public List<CL.Level> CustomLevels { get; set; }

        /// <summary>
        /// Mapping of floor type IDs to view element IDs (for Grasshopper CAD export)
        /// </summary>
        public Dictionary<string, ElementId> FloorTypeToViewMap { get; set; }

        /// <summary>
        /// Whether to save debug JSON files (pre-transform and post-transform)
        /// </summary>
        public bool SaveDebugFiles { get; set; } = true;

        /// <summary>
        /// Element filters for controlling which element types to export
        /// </summary>
        public Dictionary<string, bool> ElementFilters { get; set; }

        /// <summary>
        /// Material filters for controlling which material types to export
        /// </summary>
        public Dictionary<string, bool> MaterialFilters { get; set; }

        /// <summary>
        /// Determines if any transformations will be applied to the model
        /// </summary>
        public bool HasTransformations()
        {
            return BaseLevel != null ||
                   Math.Abs(RotationAngle) > 0.001 ||
                   (CustomFloorTypes != null && CustomFloorTypes.Count > 0) ||
                   (CustomLevels != null && CustomLevels.Count > 0);
        }

        /// <summary>
        /// Gets a description of the transformations that will be applied
        /// </summary>
        public string GetTransformationDescription()
        {
            var descriptions = new List<string>();

            if (BaseLevel != null)
                descriptions.Add($"Base level: {BaseLevel.Name}");

            if (Math.Abs(RotationAngle) > 0.001)
                descriptions.Add($"Rotation: {RotationAngle:F1}°");

            if (CustomFloorTypes != null && CustomFloorTypes.Count > 0)
                descriptions.Add($"Custom floor types: {CustomFloorTypes.Count}");

            if (CustomLevels != null && CustomLevels.Count > 0)
                descriptions.Add($"Custom levels: {CustomLevels.Count}");

            return descriptions.Count > 0 ? string.Join(", ", descriptions) : "None";
        }

        /// <summary>
        /// Creates export options with default settings
        /// </summary>
        public static ExportOptions CreateDefault(string outputPath, ExportFormat format)
        {
            return new ExportOptions
            {
                OutputPath = outputPath,
                Format = format,
                ElementFilters = GetDefaultElementFilters(),
                MaterialFilters = GetDefaultMaterialFilters()
            };
        }

        private static Dictionary<string, bool> GetDefaultElementFilters()
        {
            return new Dictionary<string, bool>
            {
                { "Grids", true },
                { "Beams", true },
                { "Braces", true },
                { "Columns", true },
                { "Floors", true },
                { "Walls", true },
                { "Footings", true }
            };
        }

        private static Dictionary<string, bool> GetDefaultMaterialFilters()
        {
            return new Dictionary<string, bool>
            {
                { "Steel", true },
                { "Concrete", true }
            };
        }
    }

    /// <summary>
    /// Result of the export operation
    /// </summary>
    public class ExportResult
    {
        /// <summary>
        /// Whether the export was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Path to the final output file
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// Path to the pre-transform debug JSON file (if SaveDebugFiles was enabled)
        /// </summary>
        public string PreTransformJsonPath { get; set; }

        /// <summary>
        /// Path to the post-transform debug JSON file (if SaveDebugFiles was enabled and transformations were applied)
        /// </summary>
        public string PostTransformJsonPath { get; set; }

        /// <summary>
        /// Total number of elements exported
        /// </summary>
        public int ElementCount { get; set; }

        /// <summary>
        /// Error message if the export failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Additional information about the export process
        /// </summary>
        public Dictionary<string, object> AdditionalInfo { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets a summary of the export result
        /// </summary>
        public string GetSummary()
        {
            if (!Success)
                return $"Export failed: {ErrorMessage}";

            var summary = $"Successfully exported {ElementCount} elements to {OutputPath}";

            if (!string.IsNullOrEmpty(PreTransformJsonPath))
                summary += $"\nPre-transform JSON: {PreTransformJsonPath}";

            if (!string.IsNullOrEmpty(PostTransformJsonPath))
                summary += $"\nPost-transform JSON: {PostTransformJsonPath}";

            return summary;
        }
    }
}