using Core.Models.Elements;
using Core.Models.ModelLayout;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ETABS.Import.Elements.AreaAssignment
{
    // Converts opening assignment information to ETABS E2K format
    public class OpeningAssignmentImport : IAssignmentImport
    {
        private List<Opening> _openings;
        private IEnumerable<Level> _levels;
        private IEnumerable<Floor> _floors;
        private readonly HashSet<string> _validStoryNames;

        // Constructor to initialize with valid story names
        public OpeningAssignmentImport(IEnumerable<string> validStoryNames)
        {
            _validStoryNames = new HashSet<string>(validStoryNames);
        }

        // Sets the data needed for converting opening assignments
        public void SetData(
            List<Opening> openings,
            IEnumerable<Level> levels,
            IEnumerable<Floor> floors)
        {
            _openings = openings;
            _levels = levels;
            _floors = floors;
        }

        // Converts opening assignments to E2K format
        public string ExportAssignments(Dictionary<string, string> idMapping)
        {
            StringBuilder sb = new StringBuilder();

            if (_openings == null || _openings.Count == 0 || idMapping == null || idMapping.Count == 0)
                return sb.ToString();

            foreach (var opening in _openings)
            {
                // Check if we have a mapping for this opening ID
                if (!idMapping.TryGetValue(opening.Id, out string areaId))
                    continue;

                // Find the story by looking up the floor's level
                string story = FindStoryFromFloorId(opening.FloorId);
                if (string.IsNullOrEmpty(story))
                    continue;

                // Format opening assignment
                sb.AppendLine(FormatOpeningAssignment(areaId, story));
            }

            return sb.ToString();
        }

        // Finds the story name from a floor ID
        private string FindStoryFromFloorId(string floorId)
        {
            if (string.IsNullOrEmpty(floorId) || _floors == null || _levels == null)
                return null;

            // Find the floor
            var floor = _floors.FirstOrDefault(f => f.Id == floorId);
            if (floor == null)
                return null;

            // Find the level
            var level = _levels.FirstOrDefault(l => l.Id == floor.LevelId);
            if (level == null)
                return null;

            // Check if we have a valid story name that contains the level name
            var validStory = _validStoryNames.FirstOrDefault(validName => validName.Contains(level.Name));

            return validStory;
        }

        // Formats an opening assignment line for E2K format
        private string FormatOpeningAssignment(string areaId, string story)
        {
            // Format: AREAASSIGN "A1" "Story4" OPENING "Yes"
            return $"  AREAASSIGN \"{areaId}\" \"{story}\" OPENING \"Yes\"";
        }
    }
}