using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using ETABS.Import.Utilities;

namespace ETABS.Import.Elements
{
    // Coordinates the import of all structural elements from E2K files
    public class ETABSToElements
    {
        private readonly ETABSToBeam _etabsToBeam;
        private readonly ETABSToColumn _etabsToColumn;
        private readonly ETABSToBrace _etabsToBrace;
        private readonly ETABSToFloor _etabsToFloor;
        private readonly ETABSToWall _etabsToWall;

        private readonly PointsCollector _pointsCollector;
        private readonly LineConnectivityParser _lineConnectivityParser;
        private readonly LineAssignmentParser _lineAssignmentParser;
        private readonly AreaParser _areaParser;

        // Initializes a new instance of ElementsImporter
        public ETABSToElements()
        {
            // Initialize utilities
            _pointsCollector = new PointsCollector();
            _lineConnectivityParser = new LineConnectivityParser();
            _lineAssignmentParser = new LineAssignmentParser();
            _areaParser = new AreaParser();

            // Initialize element importers
            _etabsToBeam = new ETABSToBeam(_pointsCollector, _lineConnectivityParser, _lineAssignmentParser);
            _etabsToColumn = new ETABSToColumn(_pointsCollector, _lineConnectivityParser, _lineAssignmentParser);
            _etabsToBrace = new ETABSToBrace(_pointsCollector, _lineConnectivityParser, _lineAssignmentParser);
            _etabsToFloor = new ETABSToFloor(_pointsCollector, _areaParser);
            _etabsToWall = new ETABSToWall(_pointsCollector, _areaParser);
        }

        // Parses E2K sections for element data
      
        public void ParseE2KSections(Dictionary<string, string> e2kSections)
        {
            // Parse points
            if (e2kSections.TryGetValue("POINT COORDINATES", out string pointsSection))
            {
                _pointsCollector.ParsePoints(pointsSection);
            }

            // Parse line connectivities
            if (e2kSections.TryGetValue("LINE CONNECTIVITIES", out string lineConnectivitiesSection))
            {
                _lineConnectivityParser.ParseLineConnectivities(lineConnectivitiesSection);
            }

            // Parse line assignments
            if (e2kSections.TryGetValue("LINE ASSIGNS", out string lineAssignsSection))
            {
                _lineAssignmentParser.ParseLineAssignments(lineAssignsSection);
            }

            // Parse area connectivities
            if (e2kSections.TryGetValue("AREA CONNECTIVITIES", out string areaConnectivitiesSection))
            {
                _areaParser.ParseAreaConnectivities(areaConnectivitiesSection);
            }

            // Parse area assignments
            if (e2kSections.TryGetValue("AREA ASSIGNS", out string areaAssignsSection))
            {
                _areaParser.ParseAreaAssignments(areaAssignsSection);
            }
        }

        // Sets up references for levels and properties

        public void SetupReferences(
            IEnumerable<Level> levels,
            IEnumerable<FrameProperties> frameProperties,
            IEnumerable<FloorProperties> floorProperties,
            IEnumerable<WallProperties> wallProperties,
            IEnumerable<Diaphragm> diaphragms)
        {
            // Set levels in all importers
            _etabsToBeam.SetLevels(levels);
            _etabsToColumn.SetLevels(levels);
            _etabsToBrace.SetLevels(levels);
            _etabsToFloor.SetLevels(levels);
            _etabsToWall.SetLevels(levels);

            // Set frame properties in relevant importers
            _etabsToBeam.SetFrameProperties(frameProperties);
            _etabsToColumn.SetFrameProperties(frameProperties);
            _etabsToBrace.SetFrameProperties(frameProperties);

            // Set floor properties
            _etabsToFloor.SetFloorProperties(floorProperties);

            // Set wall properties
            _etabsToWall.SetWallProperties(wallProperties);

            // Set diaphragms
            _etabsToFloor.SetDiaphragms(diaphragms);

            // Set pier/spandrel definitions (would need to be parsed from E2K)
            _etabsToWall.SetPierSpandrelDefinitions(new Dictionary<string, string>());
        }

        // Imports all elements into an ElementContainer
        
        public ElementContainer ImportElements()
        {
            var container = new ElementContainer();

            // Import beams
            container.Beams = _etabsToBeam.Import();

            // Import columns
            container.Columns = _etabsToColumn.Import();

            // Import braces
            container.Braces = _etabsToBrace.Import();

            // Import floors
            container.Floors = _etabsToFloor.Import();

            // Import walls
            container.Walls = _etabsToWall.Import();

            return container;
        }
    }
}