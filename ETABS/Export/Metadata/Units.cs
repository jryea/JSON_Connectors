using System.Text;
using Core.Models.Metadata;

namespace ETABS.Export.Metadata
{
    /// <summary>
    /// Converts Core Units objects to ETABS E2K format text
    /// </summary>
    public class UnitsExporter
    {
        /// <summary>
        /// Converts a Units object to E2K format text
        /// </summary>
        /// <param name="units">Units object</param>
        /// <returns>E2K format text for units</returns>
        public string ConvertToE2K(Units units)
        {
            StringBuilder sb = new StringBuilder();

            // Convert your model units to ETABS units
            string lengthUnit = ConvertToETABSLengthUnit(units.Length);
            string forceUnit = ConvertToETABSForceUnit(units.Force);
            string tempUnit = ConvertToETABSTempUnit(units.Temperature);

            sb.AppendLine("$ UNITS");
            sb.AppendLine($"UNITS {lengthUnit} {forceUnit} {tempUnit}");

            return sb.ToString();
        }

        private string ConvertToETABSLengthUnit(string modelLengthUnit)
        {
            // Convert your model length unit to ETABS format
            switch (modelLengthUnit.ToLower())
            {
                case "inches":
                    return "INCH";
                case "feet":
                    return "FT";
                case "millimeters":
                    return "MM";
                case "centimeters":
                    return "CM";
                case "meters":
                    return "M";
                default:
                    return "FT"; // Default to feet
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