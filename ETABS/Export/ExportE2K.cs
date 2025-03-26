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
using System.Reflection;
using System.Xml.Linq;
using static Core.Utilities.IdGenerator;
using ETABS.Export;

namespace ETABS.Core.Export
{
    /// <summary>
    /// Main exporter class for converting JSON building structure to ETABS E2K format
    /// </summary>
    public class E2KExport
    {
        private readonly ControlsExport _controlsExport;    
        private readonly StoriesExport _storiesExport;
        private readonly GridsExport _gridsExport;
        private readonly DiaphragmsExport _diaphragmsExport;
        private readonly WallPropertiesExport _wallPropertiesExport;
        private readonly MaterialsExport _materialsExport;
        private readonly LoadPatternsExport _loadsExport;
        private readonly PointCoordinatesExport _pointCoordinatesExport;

        // Add an injector instance
        private readonly E2KInjector _injector = new E2KInjector();

        public E2KExport()
        {
            _controlsExport = new ControlsExport();
            _storiesExport = new StoriesExport();
            _gridsExport = new GridsExport();
            _diaphragmsExport = new DiaphragmsExport();
            _wallPropertiesExport = new WallPropertiesExport();
            _materialsExport = new MaterialsExport();
            _loadsExport = new LoadPatternsExport();
            _pointCoordinatesExport = new PointCoordinatesExport();
        }

        /// <summary>
        /// Exports a building structure model to ETABS E2K format
        /// </summary>
        /// <param name="model">Building structure model</param>
        /// <param name="filePath">Path to save the E2K file</param>
        public void ExportToE2K(BaseModel model, string filePath)
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

                // Export wall properties
                if (model.Properties != null && model.Properties.WallProperties.Count > 0)
                {
                    string wallPropertiesSection = _wallPropertiesExport.ConvertToE2K(model.Properties.WallProperties);
                    sb.AppendLine(wallPropertiesSection);
                    sb.AppendLine();
                }

                if (model.Loads.LoadDefinitions.Count > 0)
                {
                    string loadsSection = _loadsExport.ConvertToE2K(model.Loads);
                    sb.AppendLine(loadsSection);
                    sb.AppendLine();
                }

                // Export point coordinates (needed before structural elements)
                string pointsSection = _pointCoordinatesExport.ConvertToE2K(model.Elements, model.ModelLayout);
                sb.AppendLine(pointsSection);

                // Create the area connectivities exporter with the point mapping
                var areaConnectivitiesExport = new AreaConnectivitiesExport(_pointCoordinatesExport.PointMapping);

                // Export area connectivities (needed before area assignments)
                string areaSection = areaConnectivitiesExport.ConvertToE2K(model.Elements);
                sb.AppendLine(areaSection);
                sb.AppendLine();

                WriteFooter(sb, model.Metadata.ProjectInfo);

                // Generate the base E2K content
                string baseE2kContent = sb.ToString();

                // Inject custom sections
                string finalE2kContent = _injector.InjectCustomSections(baseE2kContent);

                // Write the complete E2K file
                File.WriteAllText(filePath, finalE2kContent);
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

        private void WriteFooter(StringBuilder sb, ProjectInfo projectInfo)
        {
            // Project Information section
            sb.AppendLine("$ PROJECT INFORMATION");
            sb.AppendLine($"  PROJECTINFO  COMPANYNAME \"IMEG\"  MODELNAME \"{projectInfo.ProjectName}.e2k\"");
            sb.AppendLine();

            // Log section
            sb.AppendLine("$ LOG");
            sb.AppendLine("  STARTCOMMENTS  ");
            sb.AppendLine("  END  ");
            sb.AppendLine();

            // End of model file marker
            sb.AppendLine("  END OF MODEL FILE");
        }

        public void ParseRawE2KContent(string rawE2KContent)
        {
            _injector.ParseE2KContent(rawE2KContent);
        }

        // Add a method to add custom E2K sections
        public void AddCustomSection(string sectionName, string content)
        {
            _injector.AddCustomSection(sectionName, content);
        }
    }
}