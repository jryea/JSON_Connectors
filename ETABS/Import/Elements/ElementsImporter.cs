using System.Collections.Generic;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using ETABS.Import.Utilities;

namespace ETABS.Import.Elements
{
    /// <summary>
    /// Coordinates the import of all structural elements from E2K files
    /// </summary>
    public class ElementsImporter
    {
        private readonly BeamImport _beamImport;
        private readonly ColumnImport _columnImport;
        private readonly BraceImport _braceImport;
        private readonly FloorImport _floorImport;
        private readonly WallImport _wallImport;

        private readonly PointsCollector _pointsCollector;
        private readonly LineConnectivityParser _lineConnectivityParser;
        private readonly LineAssignmentParser _lineAssignmentParser;
        private readonly AreaParser _areaParser;

        /// <summary>
        /// Initializes a new instance of ElementsImporter
        /// </summary>
        public ElementsImporter()
        {
            // Initialize utilities
            _pointsCollector = new PointsCollector();
            _lineConnectivityParser = new LineConnectivityParser();
            _lineAssignmentParser = new LineAssignmentParser();
            _areaParser = new AreaParser();

            // Initialize element importers
            _beamImport = new BeamImport(_pointsCollector, _lineConnectivityParser, _lineAssignmentParser);
            _columnImport = new ColumnImport(_pointsCollector, _lineConnectivityParser, _lineAssignmentParser);
            _braceImport = new BraceImport(_pointsCollector, _lineConnectivityParser, _lineAssignmentParser);
            _floorImport = new FloorImport(_pointsCollector, _areaParser);
            _wallImport = new WallImport(_pointsCollector, _areaParser);
        }

        /// Parses E2K sections for element data
      
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

        /// <summary>
        /// Sets up references for levels and properties
        /// </summary>
        /// <param name="levels">Collection of levels</param>
        /// <param name="frameProperties">Collection of frame properties</param>
        /// <param name="floorProperties">Collection of floor properties</param>
        /// <param name="wallProperties">Collection of wall properties</param>
        /// <param name="diaphragms">Collection of diaphragms</param>
        public void SetupReferences(
            IEnumerable<Level> levels,
            IEnumerable<FrameProperties> frameProperties,
            IEnumerable<FloorProperties> floorProperties,
            IEnumerable<WallProperties> wallProperties,
            IEnumerable<Diaphragm> diaphragms)
        {
            // Set levels in all importers
            _beamImport.SetLevels(levels);
            _columnImport.SetLevels(levels);
            _braceImport.SetLevels(levels);
            _floorImport.SetLevels(levels);
            _wallImport.SetLevels(levels);

            // Set frame properties in relevant importers
            _beamImport.SetFrameProperties(frameProperties);
            _columnImport.SetFrameProperties(frameProperties);
            _braceImport.SetFrameProperties(frameProperties);

            // Set floor properties
            _floorImport.SetFloorProperties(floorProperties);

            // Set wall properties
            _wallImport.SetWallProperties(wallProperties);

            // Set diaphragms
            _floorImport.SetDiaphragms(diaphragms);

            // Set pier/spandrel definitions (would need to be parsed from E2K)
            _wallImport.SetPierSpandrelDefinitions(new Dictionary<string, string>());
        }

        /// <summary>
        /// Imports all elements into an ElementContainer
        /// </summary>
        /// <returns>ElementContainer with all imported elements</returns>
        public ElementContainer ImportElements()
        {
            var container = new ElementContainer();

            // Import beams
            container.Beams = _beamImport.Import();

            // Import columns
            container.Columns = _columnImport.Import();

            // Import braces
            container.Braces = _braceImport.Import();

            // Import floors
            container.Floors = _floorImport.Import();

            // Import walls
            container.Walls = _wallImport.Import();

            // Note: Other element types (joints, footings, piers, etc.) would require additional importers

            return container;
        }
    }
}