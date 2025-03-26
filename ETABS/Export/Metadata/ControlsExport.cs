using System.Text;
using Core.Models.Metadata;

namespace ETABS.Export.Metadata
{
    /// <summary>
    /// Converts Core Metadata and Units objects to ETABS E2K format text for the CONTROLS section
    /// </summary>
    public class ControlsExport
    {
        /// <summary>
        /// Converts Metadata and Units objects to E2K format text for CONTROLS section
        /// </summary>
        /// <param name="projectInfo">Project information object</param>
        /// <param name="units">Units object</param>
        /// <returns>E2K format text for CONTROLS section</returns>
        public string ConvertToE2K(ProjectInfo projectInfo, Units units)
        {
            StringBuilder sb = new StringBuilder();

            // Convert your model units to ETABS units
            string lengthUnit = ConvertToETABSLengthUnit(units.Length);
            string forceUnit = ConvertToETABSForceUnit(units.Force);
            string tempUnit = ConvertToETABSTempUnit(units.Temperature);

            sb.AppendLine("$ CONTROLS");
            sb.AppendLine($"\tUNITS  \"{forceUnit}\"  \"{lengthUnit}\"  \"{tempUnit}\"");
            sb.AppendLine($"\tTITLE1  \"IMEG\"");
            sb.AppendLine($"\tTITLE2  \"{projectInfo.ProjectName}.e2k\"");
            sb.AppendLine("\tPREFERENCE  MERGETOL 0.1");
            sb.AppendLine("\tRLLF  METHOD \"ASCE7-10\"  USEDEFAULTMIN \"YES\"");

            return sb.ToString();
        }

        private string ConvertToETABSLengthUnit(string modelLengthUnit)
        {
            // Convert your model length unit to ETABS format
            switch (modelLengthUnit.ToLower())
            {
                case "inches":
                    return "IN";
                case "feet":
                    return "FT";
                case "millimeters":
                    return "MM";
                case "centimeters":
                    return "CM";
                case "meters":
                    return "M";
                default:
                    return "IN"; // Default to inches
            }
        }

        private string ConvertToETABSForceUnit(string modelForceUnit)
        {
            // Convert your model force unit to ETABS format
            switch (modelForceUnit.ToLower())
            {
                case "pounds":
                    return "LB";
                case "kips":
                    return "KIP";
                case "newton":
                    return "N";
                case "kilonewton":
                    return "KN";
                default:
                    return "KIP"; // Default to kips
            }
        }

        private string ConvertToETABSTempUnit(string modelTempUnit)
        {
            // Convert your model temperature unit to ETABS format
            switch (modelTempUnit.ToLower())
            {
                case "fahrenheit":
                    return "F";
                case "celsius":
                    return "C";
                default:
                    return "F"; // Default to fahrenheit
            }
        }
    }
}