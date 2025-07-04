﻿using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using System.Collections.Generic;
using ETABS.Import;
using System.Linq;
using System.Text;

namespace ETABS.Import.Elements.AreaAssignment
{
    // Converts floor assignment information to ETABS E2K format
    public class FloorAssignmentImport : IAssignmentImport
    {
        private List<Floor> _floors;
        private IEnumerable<Level> _levels;
        private IEnumerable<FloorProperties> _floorProperties;

        // Sets the data needed for converting floor assignments
        public void SetData(
            List<Floor> floors,
            IEnumerable<Level> levels,
            IEnumerable<FloorProperties> floorProperties)
        {
            _floors = floors;
            _levels = levels;
            _floorProperties = floorProperties;
        }

        // Converts floor assignments to E2K format
        public string ExportAssignments(Dictionary<string, string> idMapping)
        {
            StringBuilder sb = new StringBuilder();

            if (_floors == null || _floors.Count == 0 || idMapping == null || idMapping.Count == 0)
                return sb.ToString();

            foreach (var floor in _floors)
            {
                // Check if we have a mapping for this floor ID
                if (!idMapping.TryGetValue(floor.Id, out string areaId))
                    continue;

                // Find the level
                var level = _levels?.FirstOrDefault(l => l.Id == floor.LevelId);

                // Use the valid story name that contains the level name
                string story = level.Name;

                // Find floor properties
                string propertyName = "Default";
                if (!string.IsNullOrEmpty(floor.FloorPropertiesId))
                {
                    var properties = _floorProperties.FirstOrDefault(p => p.Id == floor.FloorPropertiesId);
                    if (properties != null)
                    {
                        propertyName = properties.Name;
                        // Apply the same inch symbol replacement as FloorPropertiesImport
                        // Replace Unicode representation of double quote (\u0022) with "inch" to match property names
                        propertyName = propertyName.Replace("\u0022", " inch");
                    }
                }

                // Find diaphragm assignment
                string diaphragm = "D1"; // Default
                if (!string.IsNullOrEmpty(floor.DiaphragmId))
                {
                    diaphragm = floor.DiaphragmId;
                }

                // Format floor assignment
                sb.AppendLine(FormatFloorAssignment(areaId, story, propertyName, diaphragm));
            }

            return sb.ToString();
        }

        // Formats a floor assignment line for E2K format
        private string FormatFloorAssignment(string areaId, string story, string propertyName, string diaphragm)
        {
            // Format: AREAASSIGN "F1" "Story2" SECTION "8 inch Concrete" DIAPHRAGM "D1" AUTOMESH "YES"
            return $"  AREAASSIGN \"{areaId}\" \"{story}\" SECTION \"{propertyName}\" DIAPHRAGM \"{diaphragm}\" AUTOMESH \"YES\"";
        }
    }
}