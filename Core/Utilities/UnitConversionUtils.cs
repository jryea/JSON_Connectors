using System;

namespace Core.Utilities
{
    public static class UnitConversionUtils
    {
        // Convert to inches from various units
        public static double ConvertToInches(double value, string unitType)
        {
            switch (unitType?.ToLower() ?? "inches")
            {
                case "inches":
                    return value;
                case "feet":
                    return value * 12;
                case "millimeters":
                    return value * 0.0393701;
                case "centimeters":
                    return value * 0.393701;
                case "meters":
                    return value * 39.3701;
                default:
                    return value; // Assume inches if unknown
            }
        }

        // Convert from inches to specified unit
        public static double ConvertFromInches(double inches, string unitType)
        {
            switch (unitType?.ToLower() ?? "inches")
            {
                case "feet":
                    return inches / 12.0;
                case "millimeters":
                    return inches * 25.4;
                case "centimeters":
                    return inches * 2.54;
                case "meters":
                    return inches * 0.0254;
                case "inches":
                default:
                    return inches;
            }
        }

        // Round coordinates to prevent floating point comparison issues
        public static double RoundCoordinate(double value, int decimals = 6)
        {
            return Math.Round(value, decimals);
        }
    }
}