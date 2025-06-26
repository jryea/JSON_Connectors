using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using ETABS.Utilities;

namespace ETABS.Export.Elements
{
    // Coordinates the import of all structural elements from E2K files
    public class ElementsExport
    {
        private readonly BeamExport _beamExport;
        private readonly ColumnExport _columnExport;
        private readonly BraceExport _braceExport;
        private readonly FloorExport _floorExport;
        private readonly WallExport _wallExport;
        private readonly OpeningExport _openingExport;

        private readonly PointsCollector _pointsCollector;
        private readonly LineConnectivityParser _lineConnectivityParser;
        private readonly LineAssignmentParser _lineAssignmentParser;
        private readonly AreaParser _areaParser;


        // Initializes a new instance of ElementsExport
        public ElementsExport()
        {
            // Initialize utilities
            _pointsCollector = new PointsCollector();
            _lineConnectivityParser = new LineConnectivityParser();
            _lineAssignmentParser = new LineAssignmentParser();
            _areaParser = new AreaParser();

            // Initialize element exporters
            _beamExport = new BeamExport(_pointsCollector, _lineConnectivityParser, _lineAssignmentParser);
            _columnExport = new ColumnExport(_pointsCollector, _lineConnectivityParser, _lineAssignmentParser);
            _braceExport = new BraceExport(_pointsCollector, _lineConnectivityParser, _lineAssignmentParser);
            _floorExport = new FloorExport(_pointsCollector, _areaParser);
            _wallExport = new WallExport(_pointsCollector, _areaParser);
            _openingExport = new OpeningExport(_pointsCollector, _areaParser);
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
            IEnumerable<Floor> floors,
            IEnumerable<Opening> openings,
            IEnumerable<FrameProperties> frameProperties,
            IEnumerable<FloorProperties> floorProperties,
            IEnumerable<WallProperties> wallProperties,
            IEnumerable<Diaphragm> diaphragms)
        {
            // Set levels in all importers
            _beamExport.SetLevels(levels);
            _columnExport.SetLevels(levels);
            _braceExport.SetLevels(levels);
            _floorExport.SetLevels(levels);
            _wallExport.SetLevels(levels);
            _openingExport.SetLevels(levels);

            // Set floors for openings
            _openingExport.SetFloors(floors);

            // Set frame properties in relevant importers
            _beamExport.SetFrameProperties(frameProperties);
            _columnExport.SetFrameProperties(frameProperties);
            _braceExport.SetFrameProperties(frameProperties);

            // Set floor properties
            _floorExport.SetFloorProperties(floorProperties);

            // Set wall properties
            _wallExport.SetWallProperties(wallProperties);

            // Set diaphragms
            _floorExport.SetDiaphragms(diaphragms);

            // Set pier/spandrel definitions (would need to be parsed from E2K)
            _wallExport.SetPierSpandrelDefinitions(new Dictionary<string, string>());
        }

        public ElementContainer ExportElements()
        {
            var container = new ElementContainer();

            // Export beams
            container.Beams = _beamExport.Export();

            // Export columns
            container.Columns = _columnExport.Export();

            // Export braces
            container.Braces = _braceExport.Export();

            // Export floors
            container.Floors = _floorExport.Export();

            // Export walls
            container.Walls = _wallExport.Export();

            // Export openings
            container.Openings = _openingExport.Export();   

            return container;
        }
    }
}