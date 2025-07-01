using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Core.Models;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Models.Metadata;
using ETABS.Import.ModelLayout;
using ETABS.Import.Elements;
using ETABS.Import.Metadata;
using ETABS.Import.Loads;
using ETABS.Import.Properties;
using ETABS.Utilities;

namespace ETABS.Import
{
    /// <summary>
    /// Main converter class for converting JSON building structure to ETABS E2K format
    /// </summary>
    public class ModelToETABS
    {
        private readonly ControlsImport _controlsToETABS;
        private readonly StoriesImport _storiesToETABS;
        private readonly GridsImport _gridsToETABS;
        private readonly DiaphragmsImport _diaphragmsToETABS;
        private readonly MaterialsImport _materialsToETABS;
        private readonly FramePropertiesImport _framePropertiesToETABS;
        private readonly WallPropertiesImport _wallPropertiesToETABS;
        private readonly FloorPropertiesImport _floorPropertiesToETABS;
        private readonly LoadPatternsImport _loadPatternsToETABS;
        private readonly LoadCasesImport _loadCasesToETABS;
        private readonly LoadCombinationsImport _loadCombinationsToETABS;
        private readonly ShellPropsImport _shellPropsToETABS;
        private readonly PointCoordinatesImport _pointCoordinatesToETABS;

        // Add an injector instance
        private readonly E2KInjector _injector = new E2KInjector();

        // Initializes a new instance of the ModelToETABS class
        public ModelToETABS()
        {
            _controlsToETABS = new ControlsImport();
            _storiesToETABS = new StoriesImport();
            _gridsToETABS = new GridsImport();
            _diaphragmsToETABS = new DiaphragmsImport();
            _materialsToETABS = new MaterialsImport();
            _framePropertiesToETABS = new FramePropertiesImport();
            _wallPropertiesToETABS = new WallPropertiesImport();
            _floorPropertiesToETABS = new FloorPropertiesImport();
            _loadPatternsToETABS = new LoadPatternsImport();
            _loadCasesToETABS = new LoadCasesImport();
            _loadCombinationsToETABS = new LoadCombinationsImport();
            _shellPropsToETABS = new ShellPropsImport();
            _pointCoordinatesToETABS = new PointCoordinatesImport();
        }

        // Converts a building structure model to ETABS E2K format
        public string ExportToE2K(BaseModel model)
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                // Add E2K file header
                WriteHeader(sb, model.Metadata);

                // Add Controls section (includes units)
                string controlsSection = _controlsToETABS.ConvertToE2K(model.Metadata.ProjectInfo, model.Metadata.Units);
                sb.AppendLine(controlsSection);
                sb.AppendLine();

                /// Convert stories
                if (model.ModelLayout.Levels.Count > 0)
                {
                    string storySection = _storiesToETABS.ConvertToE2K(model.ModelLayout.Levels);
                    sb.AppendLine(storySection);
                    sb.AppendLine();
                }

                // Get valid story names
                List<string> validStoryNames = _storiesToETABS.GetStoryNames();

                //Convert grids
                if (model.ModelLayout.Grids.Count > 0)
                {
                    string gridSection = _gridsToETABS.ConvertToE2K(model.ModelLayout.Grids);
                    sb.AppendLine(gridSection);
                    sb.AppendLine();
                }

                // Convert diaphragms
                if (model.Properties.Diaphragms.Count > 0)
                {
                    string diaphragmSection = _diaphragmsToETABS.ConvertToE2K(model.Properties.Diaphragms);
                    sb.AppendLine(diaphragmSection);
                    sb.AppendLine();
                }

                // Convert materials
                if (model.Properties != null && model.Properties.Materials.Count > 0)
                {
                    string materialsSection = _materialsToETABS.ConvertToE2K(model.Properties.Materials);
                    sb.AppendLine(materialsSection);
                    sb.AppendLine();
                }

                // Convert frame sections
                if (model.Properties != null && model.Properties.FrameProperties.Count > 0)
                {
                    string framePropertiesSection = _framePropertiesToETABS.ConvertToE2K(
                        model.Properties.FrameProperties,
                        model.Properties.Materials);
                    sb.AppendLine(framePropertiesSection);
                    sb.AppendLine();
                }

                // Convert floor properties (slab properties)
                if (model.Properties != null && model.Properties.FloorProperties.Count > 0)
                {
                    string floorPropertiesSection = _floorPropertiesToETABS.ConvertToE2K(
                        model.Properties.FloorProperties,
                        model.Properties.Materials);
                    sb.AppendLine(floorPropertiesSection);
                    sb.AppendLine();
                }

                // Convert wall properties
                if (model.Properties != null && model.Properties.WallProperties.Count > 0)
                {
                    string wallPropertiesSection = _wallPropertiesToETABS.ConvertToE2K(
                        model.Properties.WallProperties,
                        model.Properties.Materials);
                    sb.AppendLine(wallPropertiesSection);
                    sb.AppendLine();
                }

                // Convert load patterns
                string loadPatternsSection = _loadPatternsToETABS.ConvertToE2K(model.Loads);
                sb.AppendLine(loadPatternsSection);
                sb.AppendLine();

                // Important: First process all point coordinates
                // This populates the _pointCoordinatesToETABS._pointMapping dictionary
                // which becomes the single source of truth for all point IDs
                string pointsSection = _pointCoordinatesToETABS.ConvertToE2K(model.Elements, model.ModelLayout);
                sb.AppendLine(pointsSection);
                sb.AppendLine();

                // Create the line elements converter with the point coordinates instance
                var lineElementsToETABS = new LineElementsImport(_pointCoordinatesToETABS);

                // Convert line elements (both connectivities and assignments)  
                string lineElementsSection = lineElementsToETABS.ConvertToE2K(
                    model.Elements,
                    model.ModelLayout.Levels,
                    model.Properties.FrameProperties);
                sb.AppendLine(lineElementsSection);
                sb.AppendLine();

                // Create the area elements converter with the point coordinates instance
                var areaElementsToETABS = new AreaElementsImport(_pointCoordinatesToETABS);

                // Convert area elements (both connectivities and assignments)
                string areaElementsSection = areaElementsToETABS.ConvertToE2K(
                    model.Elements,
                    model.ModelLayout,
                    model.Properties);

                sb.AppendLine(areaElementsSection);
                sb.AppendLine();

                //Convert shell uniform load sets
                if (model.Loads.SurfaceLoads.Count > 0)
                {
                    string shellPropsSection = _shellPropsToETABS.ConvertToE2K(
                        model.Loads.SurfaceLoads,
                        model.Loads.LoadDefinitions);
                    sb.AppendLine(shellPropsSection);
                    sb.AppendLine();
                }

                // Convert load cases
                string loadCasesSection = _loadCasesToETABS.ConvertToE2K(model.Loads);
                sb.AppendLine(loadCasesSection);
                sb.AppendLine();

                // Convert load combinations
                string loadCombinationsSection = _loadCombinationsToETABS.ConvertToE2K(model.Loads);
                sb.AppendLine(loadCombinationsSection);
                sb.AppendLine();

                // Write the footer
                WriteFooter(sb, model.Metadata.ProjectInfo);

                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error converting to E2K format: {ex.Message}", ex);
            }
        }

        private void WriteHeader(StringBuilder sb, MetadataContainer metadata)
        {
            sb.AppendLine("$ PROGRAM INFORMATION");
            sb.AppendLine("\tPROGRAM  \"ETABS\"  VERSION \"21.2.0\"\n");
        }

        private void WriteFooter(StringBuilder sb, ProjectInfo projectInfo)
        {
            // Project Information section
            sb.AppendLine("$ PROJECT INFORMATION");
            sb.AppendLine($"  PROJECTINFO    COMPANYNAME \"IMEG\"    MODELNAME \"{projectInfo.ProjectName}\"  ");
            sb.AppendLine();

            // Log section
            sb.AppendLine("$ LOG");
            sb.AppendLine("  STARTCOMMENTS  ");
            sb.AppendLine($"ETABS Nonlinear  21.2.0 File saved as {projectInfo.ProjectName}.EDB at {DateTime.Now}");
            sb.AppendLine("  ENDCOMMENTS  ");
            sb.AppendLine();

            // End of model file marker
            sb.AppendLine("  END");
            sb.AppendLine("$ END OF MODEL FILE");
        }
    }
}