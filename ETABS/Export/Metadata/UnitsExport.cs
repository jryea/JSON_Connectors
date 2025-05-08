using System;
using System.Text.RegularExpressions;
using Core.Models.Metadata;

namespace ETABS.Export.Metadata
{
    // Imports unit definitions from ETABS E2K file
    public class UnitsExport
    {
        // Imports units from E2K CONTROLS section
        public Units Import(string controlsSection)
        {
            var units = new Units
            {
                Length = "inches",     // Default units
                Force = "pounds",      // Default units
                Temperature = "fahrenheit" // Default units
            };

            if (string.IsNullOrWhiteSpace(controlsSection))
                return units;

            // Extract units definition
            // Format: UNITS "LB" "IN" "F"
            var unitsPattern = new Regex(@"UNITS\s+""([^""]+)""\s+""([^""]+)""\s+""([^""]+)""",
                RegexOptions.Singleline);

            var unitsMatch = unitsPattern.Match(controlsSection);
            if (unitsMatch.Success && unitsMatch.Groups.Count >= 4)
            {
                string forceUnit = unitsMatch.Groups[1].Value;
                string lengthUnit = unitsMatch.Groups[2].Value;
                string tempUnit = unitsMatch.Groups[3].Value;

                // Convert ETABS units to model units
                units.Force = ConvertForceUnit(forceUnit);
                units.Length = ConvertLengthUnit(lengthUnit);
                units.Temperature = ConvertTemperatureUnit(tempUnit);
            }

            return units;
        }

        // Converts ETABS force unit to model force unit
       
        private string ConvertForceUnit(string etabsUnit)
        {
            switch (etabsUnit.ToUpper())
            {
                case "LB":
                    return "pounds";
                case "KIP":
                    return "kips";
                case "N":
                    return "newton";
                case "KN":
                    return "kilonewton";
                case "TON":
                    return "tons";
                case "KG":
                case "KGF":
                    return "kilogram";
                default:
                    return "pounds"; // Default to pounds if not recognized
            }
        }

        // Converts ETABS length unit to model length unit
      
        private string ConvertLengthUnit(string etabsUnit)
        {
            switch (etabsUnit.ToUpper())
            {
                case "IN":
                    return "inches";
                case "FT":
                    return "feet";
                case "MM":
                    return "millimeters";
                case "CM":
                    return "centimeters";
                case "M":
                    return "meters";
                default:
                    return "inches"; // Default to inches if not recognized
            }
        }

        // Converts ETABS temperature unit to model temperature unit
       
        private string ConvertTemperatureUnit(string etabsUnit)
        {
            switch (etabsUnit.ToUpper())
            {
                case "F":
                    return "fahrenheit";
                case "C":
                    return "celsius";
                case "K":
                    return "kelvin";
                default:
                    return "fahrenheit"; // Default to fahrenheit if not recognized
            }
        }

        // Gets force unit scale factor for converting values
        
        public static double GetForceUnitScale(string fromUnit, string toUnit)
        {
            // Normalize units
            fromUnit = NormalizeForceUnit(fromUnit);
            toUnit = NormalizeForceUnit(toUnit);

            // If units are the same, scale is 1.0
            if (fromUnit == toUnit)
                return 1.0;

            // Convert to kips as base unit
            double fromToKip;
            switch (fromUnit)
            {
                case "pounds":
                    fromToKip = 0.001; // 1 pound = 0.001 kip
                    break;
                case "kips":
                    fromToKip = 1.0;
                    break;
                case "newton":
                    fromToKip = 0.0002248089; // 1 N = 0.0002248089 kip
                    break;
                case "kilonewton":
                    fromToKip = 0.2248089; // 1 kN = 0.2248089 kip
                    break;
                default:
                    fromToKip = 1.0; // Default
                    break;
            }

            // Convert from kips to target unit
            double kipToTarget;
            switch (toUnit)
            {
                case "pounds":
                    kipToTarget = 1000.0; // 1 kip = 1000 pounds
                    break;
                case "kips":
                    kipToTarget = 1.0;
                    break;
                case "newton":
                    kipToTarget = 4448.222; // 1 kip = 4448.222 N
                    break;
                case "kilonewton":
                    kipToTarget = 4.448222; // 1 kip = 4.448222 kN
                    break;
                default:
                    kipToTarget = 1.0; // Default
                    break;
            }

            return fromToKip * kipToTarget;
        }

        // Gets length unit scale factor for converting values
   
        public static double GetLengthUnitScale(string fromUnit, string toUnit)
        {
            // Normalize units
            fromUnit = NormalizeLengthUnit(fromUnit);
            toUnit = NormalizeLengthUnit(toUnit);

            // If units are the same, scale is 1.0
            if (fromUnit == toUnit)
                return 1.0;

            // Convert to inches as base unit
            double fromToInch;
            switch (fromUnit)
            {
                case "inches":
                    fromToInch = 1.0;
                    break;
                case "feet":
                    fromToInch = 12.0; // 1 foot = 12 inches
                    break;
                case "millimeters":
                    fromToInch = 0.03937008; // 1 mm = 0.03937008 inches
                    break;
                case "centimeters":
                    fromToInch = 0.3937008; // 1 cm = 0.3937008 inches
                    break;
                case "meters":
                    fromToInch = 39.37008; // 1 m = 39.37008 inches
                    break;
                default:
                    fromToInch = 1.0; // Default
                    break;
            }

            // Convert from inches to target unit
            double inchToTarget;
            switch (toUnit)
            {
                case "inches":
                    inchToTarget = 1.0;
                    break;
                case "feet":
                    inchToTarget = 1.0 / 12.0; // 1 inch = 1/12 foot
                    break;
                case "millimeters":
                    inchToTarget = 25.4; // 1 inch = 25.4 mm
                    break;
                case "centimeters":
                    inchToTarget = 2.54; // 1 inch = 2.54 cm
                    break;
                case "meters":
                    inchToTarget = 0.0254; // 1 inch = 0.0254 m
                    break;
                default:
                    inchToTarget = 1.0; // Default
                    break;
            }

            return fromToInch * inchToTarget;
        }

        // Normalizes force unit name for consistent comparison
        private static string NormalizeForceUnit(string unit)
        {
            if (string.IsNullOrEmpty(unit))
                return "pounds";

            unit = unit.ToLower();

            if (unit == "lb" || unit == "lbs" || unit == "pound")
                return "pounds";
            if (unit == "kip" || unit == "k")
                return "kips";
            if (unit == "n")
                return "newton";
            if (unit == "kn")
                return "kilonewton";

            return unit;
        }

        // Normalizes length unit name for consistent comparison
        private static string NormalizeLengthUnit(string unit)
        {
            if (string.IsNullOrEmpty(unit))
                return "inches";

            unit = unit.ToLower();

            if (unit == "in" || unit == "inch")
                return "inches";
            if (unit == "ft" || unit == "foot")
                return "feet";
            if (unit == "mm")
                return "millimeters";
            if (unit == "cm")
                return "centimeters";
            if (unit == "m")
                return "meters";

            return unit;
        }
    }
}