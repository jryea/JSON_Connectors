using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Core.Models;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using Core.Models.Metadata;
using ETABS.Export.ModelLayout;
using ETABS.Export.Elements;
using ETABS.Export.Metadata;
using ETABS.Export.Loads;
using ETABS.Export.Properties;

namespace ETABS.Utilities
{
    // Main exporter class for converting JSON building structure to ETABS E2K format
    public class ModelToE2K
    {
        private readonly ControlsExport _controlsExport;
        private readonly StoriesExport _storiesExport;
        private readonly GridsExport _gridsExport;
        private readonly DiaphragmsExport _diaphragmsExport;
        private readonly MaterialsExport _materialsExport;
        private readonly FramePropertiesExport _framePropertiesExport;
        private readonly WallPropertiesExport _wallPropertiesExport;
        private readonly LoadPatternsExport _loadPatternsExport;
        private readonly LoadCasesExport _loadCasesExport;
        private readonly LoadCombinationsExport _loadCombinationsExport;
        private readonly ShellPropsExport _shellPropsExport;
        private readonly PointCoordinatesExport _pointCoordinatesExport;

        // Add an injector instance
        private readonly E2KInjector _injector = new E2KInjector();

        public ModelToE2K()
        {
            _controlsExport = new ControlsExport();
            _storiesExport = new StoriesExport();
            _gridsExport = new GridsExport();
            _diaphragmsExport = new DiaphragmsExport();
            _materialsExport = new MaterialsExport();
            _framePropertiesExport = new FramePropertiesExport();
            _wallPropertiesExport = new WallPropertiesExport();
            _loadPatternsExport = new LoadPatternsExport();
            _loadCasesExport = new LoadCasesExport();
            _loadCombinationsExport = new LoadCombinationsExport();
            _shellPropsExport = new ShellPropsExport();
            _pointCoordinatesExport = new PointCoordinatesExport();
        }

        // Exports a building structure model to ETABS E2K format
        public string ExportToE2K(BaseModel model)
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                // Add E2K file header
                WriteHeader(sb, model.Metadata);

                // Add Controls section (includes units)
                string controlsSection = _controlsExport.ConvertToE2K(model.Metadata.ProjectInfo, model.Metadata.Units);
                sb.AppendLine(controlsSection);
                sb.AppendLine();

                // Export stories
                if (model.ModelLayout.Levels.Count > 0)
                {
                    string storySection = _storiesExport.ConvertToE2K(model.ModelLayout.Levels);
                    sb.AppendLine(storySection);
                    sb.AppendLine();
                }

                // Export grids
                if (model.ModelLayout.Grids.Count > 0)
                {
                    string gridSection = _gridsExport.ConvertToE2K(model.ModelLayout.Grids);
                    sb.AppendLine(gridSection);
                    sb.AppendLine();
                }

                // Export diaphragms
                if (model.Properties.Diaphragms.Count > 0)
                {
                    string diaphragmSection = _diaphragmsExport.ConvertToE2K(model.Properties.Diaphragms);
                    sb.AppendLine(diaphragmSection);
                    sb.AppendLine();
                }

                // Export materials
                if (model.Properties != null && model.Properties.Materials.Count > 0)
                {
                    string materialsSection = _materialsExport.ConvertToE2K(model.Properties.Materials);
                    sb.AppendLine(materialsSection);
                    sb.AppendLine();
                }

                // Export frame sections
                if (model.Properties != null && model.Properties.FrameProperties.Count > 0)
                {
                    string framePropertiesSection = _framePropertiesExport.ConvertToE2K(
                        model.Properties.FrameProperties,
                        model.Properties.Materials);
                    sb.AppendLine(framePropertiesSection);
                    sb.AppendLine();
                }

                // Export wall properties
                if (model.Properties != null && model.Properties.WallProperties.Count > 0)
                {
                    string wallPropertiesSection = _wallPropertiesExport.ConvertToE2K
                        (model.Properties.WallProperties,
                        model.Properties.Materials);
                    sb.AppendLine(wallPropertiesSection);
                    sb.AppendLine();
                }

                // Export load patterns
                string loadPatternsSection = _loadPatternsExport.ConvertToE2K(model.Loads);
                sb.AppendLine(loadPatternsSection);
                sb.AppendLine();

                // Export point coordinates (needed before structural elements)
                string pointsSection = _pointCoordinatesExport.ConvertToE2K(model.Elements, model.ModelLayout);
                sb.AppendLine(pointsSection);
                sb.AppendLine();

                // Create the consolidated line elements exporter with the point mapping
                var lineElementsExport = new LineElementsExport(_pointCoordinatesExport.PointMapping);

                // Export line elements (both connectivities and assignments)  
                string lineElementsSection = lineElementsExport.ConvertToE2K(
                    model.Elements,
                    model.ModelLayout.Levels,
                    model.Properties.FrameProperties);
                sb.AppendLine(lineElementsSection);
                sb.AppendLine();

                // Create the consolidated area elements exporter with the point mapping
                var areaElementsExport = new AreaElementsExport(_pointCoordinatesExport.PointMapping);

                // Export area elements (both connectivities and assignments)
                string areaElementsSection = areaElementsExport.ConvertToE2K(
                    model.Elements,
                    model.ModelLayout.Levels,
                    model.Properties.WallProperties,
                    model.Properties.FloorProperties);
                sb.AppendLine(areaElementsSection);
                sb.AppendLine();

                // Export shell uniform load sets
                if (model.Loads.SurfaceLoads.Count > 0)
                {
                    string shellPropsSection = _shellPropsExport.ConvertToE2K(
                        model.Loads.SurfaceLoads,
                        model.Loads.LoadDefinitions);
                    sb.AppendLine(shellPropsSection);
                    sb.AppendLine();

                    // Export area loads
                    string areaLoadsSection = _shellPropsExport.FormatAreaLoads(model.Loads.SurfaceLoads);
                    sb.AppendLine(areaLoadsSection);
                    sb.AppendLine();
                }

                // Export load cases
                //string loadCasesSection = _loadCasesExport.ConvertToE2K(model.Loads);
                //sb.AppendLine(loadCasesSection);
                //sb.AppendLine();

                // Export load combinations
                string loadCombinationsSection = _loadCombinationsExport.ConvertToE2K(model.Loads);
                sb.AppendLine(loadCombinationsSection);
                sb.AppendLine();

                // Add standard analysis options section
                WriteAnalysisOptions(sb);

                // Write the footer
                WriteFooter(sb, model.Metadata.ProjectInfo);

                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error exporting to E2K format: {ex.Message}", ex);
            }
        }

        private void WriteHeader(StringBuilder sb, MetadataContainer metadata)
        {
            sb.AppendLine("$ PROGRAM INFORMATION");
            sb.AppendLine("\tPROGRAM  \"ETABS\"  VERSION \"21.2.0\"\n");
        }

        private void WriteAnalysisOptions(StringBuilder sb)
        {
            // Add standard analysis options section based on E2K Mission Bay file
            sb.AppendLine("$ ANALYSIS OPTIONS");
            sb.AppendLine("  ACTIVEDOF \"UX UY UZ RX RY RZ\"  ");
            sb.AppendLine("  MODELHINGESINLINKS \"Yes\"  HINGEDAMPINGOPTION \"KT\"  ");
            sb.AppendLine("  PDELTA  METHOD \"NONE\"  ");
            sb.AppendLine("  AUTOMESHOPTIONS  MESHTYPE  \"GENERAL\"  FLOORMESHMAXSIZE  144 WALLMESHMAXSIZE  60 ");
            sb.AppendLine();

            // Add mass source section
            sb.AppendLine("$ MASS SOURCE");
            sb.AppendLine("  MASSSOURCE  \"MsSrc1\"    INCLUDEELEMENTS \"No\"    INCLUDEADDEDMASS \"No\"    INCLUDELOADS \"Yes\"    INCLUDEMOVE \"No\"    INCLUDELATERALMASS \"Yes\"    INCLUDEVERTICALMASS \"No\"    LUMPATSTORIES \"Yes\"    ISDEFAULT \"Yes\"  ");
            sb.AppendLine("  MASSSOURCELOAD  \"MsSrc1\"  \"SW\"  1 ");
            sb.AppendLine("  MASSSOURCELOAD  \"MsSrc1\"  \"SDL\"  1 ");
            sb.AppendLine();
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