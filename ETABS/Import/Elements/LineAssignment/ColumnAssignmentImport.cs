﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;

namespace ETABS.Import.Elements.LineAssignment
{
    // Converts column assignment information to ETABS E2K format
    public class ColumnAssignmentImport : IAssignmentImport
    {
        private List<Column> _columns;
        private IEnumerable<Level> _levels;
        private IEnumerable<FrameProperties> _frameProperties;

        // Sets the data needed for converting column assignments
        public void SetData(
            List<Column> columns,
            IEnumerable<Level> levels,
            IEnumerable<FrameProperties> frameProperties)
        {
            _columns = columns;
            _levels = levels;
            _frameProperties = frameProperties;
        }

        // Converts column assignments to E2K format
        public string ExportAssignments(Dictionary<string, string> idMapping)
        {
            StringBuilder sb = new StringBuilder();

            // Get all levels sorted by elevation (highest to lowest)
            var sortedLevels = _levels.OrderByDescending(l => l.Elevation).ToList();

            foreach (var column in _columns)
            {
                if (!idMapping.TryGetValue(column.Id, out string e2kId))
                    continue;

                // Find base and top levels
                var baseLevel = _levels.FirstOrDefault(l => l.Id == column.BaseLevelId);
                var topLevel = _levels.FirstOrDefault(l => l.Id == column.TopLevelId);

                if (baseLevel == null || topLevel == null)
                    continue;

                // Find section from frame properties
                string sectionName = "Unknown";
                if (!string.IsNullOrEmpty(column.FramePropertiesId))
                {
                    var properties = _frameProperties.FirstOrDefault(p => p.Id == column.FramePropertiesId);
                    if (properties != null)
                        sectionName = properties.Name;
                }

                // Replace Unicode representation of double quote (\u0022) with "inch" in the section name
                sectionName = sectionName.Replace("\u0022", "inch");

                // Create assignments for each story the column spans through, except the base level
                foreach (var level in sortedLevels)
                {
                    // Skip levels below the base level or above the top level
                    if (level.Elevation < baseLevel.Elevation || level.Elevation > topLevel.Elevation)
                        continue;

                    // Skip the base level itself - columns in ETABS are defined by their top story
                    if (level.Id == baseLevel.Id)
                        continue;

                    // Create an assignment for this level
                    string storyName = level.Name;

                    // Format the column assignment, including orientation if not 0
                    sb.AppendLine(FormatColumnAssign(
                        lineId: e2kId,
                        story: storyName,
                        section: sectionName,
                        column: column,
                        minNumSta: 3,
                        autoMesh: "YES",
                        meshAtIntersections: "YES"));
                }
            }

            return sb.ToString();
        }

        // Formats a column assignment line for E2K format
        private string FormatColumnAssign(
            string lineId,
            string story,
            string section,
            Column column,
            int minNumSta = 3,
            string autoMesh = "YES",
            string meshAtIntersections = "YES")
        {
            StringBuilder sb = new StringBuilder($"  LINEASSIGN \"{lineId}\" \"{story}\" SECTION \"{section}\"");

            // Include ANG parameter only if orientation is not 0
            if (column != null && Math.Abs(column.Orientation) > 0.001)
                sb.Append($" ANG {column.Orientation}");

            // Add modifiers if they deviate from default value of 1.0
            if (column?.FrameModifiers != null)
            {
                // Using Math.Abs to compare floating point values with a small tolerance
                // Format with appropriate decimal places for cleaner output
                if (Math.Abs(column.FrameModifiers.Area - 1.0) > 0.0001)
                    sb.Append($" PROPMODA {column.FrameModifiers.Area:0.####}");
                if (Math.Abs(column.FrameModifiers.A22 - 1.0) > 0.0001)
                    sb.Append($" PROPMODA2 {column.FrameModifiers.A22:0.####}");
                if (Math.Abs(column.FrameModifiers.A33 - 1.0) > 0.0001)
                    sb.Append($" PROPMODA3 {column.FrameModifiers.A33:0.####}");
                if (Math.Abs(column.FrameModifiers.Torsion - 1.0) > 0.0001)
                    sb.Append($" PROPMODT {column.FrameModifiers.Torsion:0.####}");
                if (Math.Abs(column.FrameModifiers.I22 - 1.0) > 0.0001)
                    sb.Append($" PROPMODI22 {column.FrameModifiers.I22:0.####}");
                if (Math.Abs(column.FrameModifiers.I33 - 1.0) > 0.0001)
                    sb.Append($" PROPMODI33 {column.FrameModifiers.I33:0.####}");
                if (Math.Abs(column.FrameModifiers.Mass - 1.0) > 0.0001)
                    sb.Append($" PROPMODM {column.FrameModifiers.Mass:0.####}");
                if (Math.Abs(column.FrameModifiers.Weight - 1.0) > 0.0001)
                    sb.Append($" PROPMODW {column.FrameModifiers.Weight:0.####}");
            }

            sb.Append($" MINNUMSTA {minNumSta} AUTOMESH \"{autoMesh}\" MESHATINTERSECTIONS \"{meshAtIntersections}\"");

            return sb.ToString();
        }
    }
}