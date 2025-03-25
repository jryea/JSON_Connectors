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

namespace ETABS.Core.Export
{
    /// <summary>
    /// Main exporter class for converting JSON building structure to ETABS E2K format
    /// </summary>
    public class E2KExporter
    {
        private readonly GridsExport _gridsExport;
        private readonly LevelsExport _levelsExport;
        private readonly ColumnsExport _columnsExport;
        private readonly BeamsExport _beamsExport;
        private readonly MaterialsExport _materialsExport;
        private readonly UnitsExport _unitsExport;
        private readonly LoadsExport _loadsExport;

        public E2KExporter()
        {
            _gridsExport = new GridsExport();
            _levelsExport = new LevelsExport();
            _columnsExport = new ColumnsExport();
            _beamsExport = new BeamsExport();
            _materialsExport = new MaterialsExport();
            _unitsExport = new UnitsExport();
            _loadsExport = new LoadsExport();
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

                // Set units
                string unitsSection = _unitsExport.ConvertToE2K(model.Metadata.Units);
                sb.AppendLine(unitsSection);
                sb.AppendLine();

                // Export materials (needed before structural elements)
                if (model.Properties != null && model.Properties.Materials.Count > 0)
                {
                    string materialsSection = _materialsExport.ConvertToE2K(model.Properties.Materials);
                    sb.AppendLine(materialsSection);
                    sb.AppendLine();
                }

                // Export grids
                if (model.ModelLayout.Grids.Count > 0)
                {
                    string gridSection = _gridsExport.ConvertToE2K(model.ModelLayout.Grids);
                    sb.AppendLine(gridSection);
                    sb.AppendLine();
                }

                // Export levels
                if (model.ModelLayout.Levels.Count > 0)
                {
                    string levelSection = _levelsExport.ConvertToE2K(model.ModelLayout.Levels);
                    sb.AppendLine(levelSection);
                    sb.AppendLine();
                }

                // Export columns
                if (model.Elements.Columns.Count > 0)
                {
                    string columnsSection = _columnsExport.ConvertToE2K(model.Elements.Columns, model.ModelLayout.Levels);
                    sb.AppendLine(columnsSection);
                    sb.AppendLine();
                }

                // Export beams
                if (model.Elements.Beams.Count > 0)
                {
                    string beamsSection = _beamsExport.ConvertToE2K(model.Elements.Beams, model.ModelLayout.Levels);
                    sb.AppendLine(beamsSection);
                    sb.AppendLine();
                }

                if (model.Loads.LoadDefinitions.Count > 0)
                {
                    string loadsSection = _loadsExport.ConvertToE2K(model.Loads);
                    sb.AppendLine(loadsSection);
                    sb.AppendLine();
                }

                // Write the complete E2K file
                File.WriteAllText(filePath, sb.ToString());
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
            sb.AppendLine("$ CONTROLS");
            sb.AppendLine($"\tUNITS \"{metadata.Units.Force}\" \"{metadata.Units.Length}\" \"{metadata.Units.Temperature}\" ");
            sb.AppendLine($"$ Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"$ Schema Version: {metadata.ProjectInfo.SchemaVersion}");
            sb.AppendLine();
            sb.AppendLine("$ PROGRAM CONTROL INFORMATION");
            sb.AppendLine("UNITS KIPS FEET F");  // Default units will be overridden by UnitsExporter
            sb.AppendLine();
        }
    }
}