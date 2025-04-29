using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace ETABS.ToETABS.Elements.AreaAssignment
{
    // Converts wall assignment information to ETABS E2K format
    public class WallAssignmentToETABS : IAssignmentToETABS
    {
        private List<Wall> _walls;
        private IEnumerable<Level> _levels;
        private IEnumerable<WallProperties> _wallProperties;

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

                // Find base and top levels
                var baseLevel = _levels.FirstOrDefault(l => l.Id == wall.BaseLevelId);
                var topLevel = _levels.FirstOrDefault(l => l.Id == wall.TopLevelId);

                if (baseLevel == null || topLevel == null)
                    continue;

                // Format story names
                string baseStory = GetStoryName(baseLevel);
                string topStory = GetStoryName(topLevel);

                // Find wall properties
                string propertyName = "Default";
                if (!string.IsNullOrEmpty(wall.PropertiesId))
                {
                    var properties = _wallProperties.FirstOrDefault(p => p.Id == wall.PropertiesId);
                    if (properties != null)
                        propertyName = properties.Name;
                }

                // Format wall assignment
                sb.AppendLine(FormatWallAssignment(areaId, baseStory, topStory, propertyName));
            }

            return sb.ToString();
        }

        // Gets a formatted story name from a level
        private string GetStoryName(Level level)
        {
            return level.Name.ToLower() == "base" ? "Base" : $"Story{level.Name}";
        }

        // Formats a wall assignment line for E2K format
        private string FormatWallAssignment(string areaId, string baseStory, string topStory, string propertyName)
        {
            // Format: AREAASSIGN "W1" "Story1" "Story2" SECTION "Wall1" AUTOMESH "YES"
            return $"  AREAASSIGN \"{areaId}\" \"{baseStory}\" \"{topStory}\" SECTION \"{propertyName}\" AUTOMESH \"YES\"";
        }
    }
}