using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;

namespace ETABS.ToETABS.Elements.AreaAssignment
{
    // Converts wall assignment information to ETABS E2K format
    public class WallAssignmentToETABS : IAssignmentToETABS
    {
        private List<Wall> _walls;
        private IEnumerable<Level> _levels;
        private IEnumerable<WallProperties> _wallProperties;
        private readonly HashSet<string> _validStoryNames;

        // Constructor to initialize with valid story names
        public WallAssignmentToETABS(IEnumerable<string> validStoryNames)
        {
            _validStoryNames = new HashSet<string>(validStoryNames);
        }

        // Sets the data needed for converting wall assignments
        public void SetData(
            List<Wall> walls,
            IEnumerable<Level> levels,
            IEnumerable<WallProperties> wallProperties)
        {
            _walls = walls;
            _levels = levels;
            _wallProperties = wallProperties;
        }

        // Converts wall assignments to E2K format
        public string ExportAssignments(Dictionary<string, string> idMapping)
        {
            StringBuilder sb = new StringBuilder();

            if (_walls == null || _walls.Count == 0 || idMapping == null || idMapping.Count == 0)
                return sb.ToString();

            foreach (var wall in _walls)
            {
                // Check if we have a mapping for this wall ID
                if (!idMapping.TryGetValue(wall.Id, out string areaId))
                    continue;

                // Find only the top level (ETABS wall assignments only need top level)
                var topLevel = _levels.FirstOrDefault(l => l.Id == wall.TopLevelId);
                if (topLevel == null || !_validStoryNames.Any(validName => validName.Contains(topLevel.Name)))
                    continue;

                // Use the valid story name that contains the level name
                string topStory = _validStoryNames.First(validName => validName.Contains(topLevel.Name));

                // Find wall properties
                string propertyName = "Default";
                if (!string.IsNullOrEmpty(wall.PropertiesId))
                {
                    var properties = _wallProperties.FirstOrDefault(p => p.Id == wall.PropertiesId);
                    if (properties != null)
                        propertyName = properties.Name;
                }

                // Format wall assignment using the valid story name
                sb.AppendLine(FormatWallAssignment(areaId, topStory, propertyName));
            }

            return sb.ToString();
        }

        // Formats a wall assignment line for E2K format
        private string FormatWallAssignment(string areaId, string topStory, string propertyName)
        {
            // Replace all "\"" symbols in the name with " inch"
            string formattedName = propertyName.Replace("\"", " inch");

            // Format: AREAASSIGN "W1" "Story4" SECTION "Wall1" AUTOMESH "YES"
            return $"  AREAASSIGN \"{areaId}\" \"{topStory}\" SECTION \"{formattedName}\" AUTOMESH \"YES\"";
        }
    }
}
