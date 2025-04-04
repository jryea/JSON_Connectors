// RAMModelConverter.cs
using System;
using System.Collections.Generic;
using Core.Models;
using Core.Models.Elements;
using Core.Models.Properties;
using RAMDATAACCESSLib;

namespace RAM.Utilities
{
    public static class RAMModelConverter
    {
        // Convert material types
        public static EMATERIALTYPES ConvertMaterialType(Material material)
        {
            if (material == null)
                return EMATERIALTYPES.ESteelMat;

            string materialType = material.Type.ToLower();

            if (materialType.Contains("concrete"))
                return EMATERIALTYPES.EConcreteMat;
            else if (materialType.Contains("joist"))
                return EMATERIALTYPES.ESteelJoistMat;
            else if (materialType.Contains("steel"))
                return EMATERIALTYPES.ESteelMat;
            else
                return EMATERIALTYPES.ESteelMat;
        }

        // Convert coordinates to inches (RAM standard unit)
        public static double ConvertToInches(double value, string unitType)
        {
            switch (unitType.ToLower())
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

        // Get deck properties based on deck type and gage
        public static void GetDeckProperties(string deckType, int deckGage, out double selfWeight)
        {
            if (deckType == "VULCRAFT 1.5VL")
            {
                if (deckGage == 22)
                    selfWeight = 1.6;
                else if (deckGage == 20)
                    selfWeight = 2.0;
                else if (deckGage == 19)
                    selfWeight = 2.3;
                else if (deckGage == 18)
                    selfWeight = 2.6;
                else if (deckGage == 16)
                    selfWeight = 3.3;
                else
                    selfWeight = 2.0; // Default
            }
            else if (deckType == "VULCRAFT 2VL")
            {
                if (deckGage == 22)
                    selfWeight = 1.6;
                else if (deckGage == 20)
                    selfWeight = 1.9;
                else if (deckGage == 19)
                    selfWeight = 2.2;
                else if (deckGage == 18)
                    selfWeight = 2.5;
                else if (deckGage == 16)
                    selfWeight = 3.2;
                else
                    selfWeight = 2.0; // Default
            }
            else if (deckType == "VULCRAFT 3VL")
            {
                if (deckGage == 22)
                    selfWeight = 1.7;
                else if (deckGage == 20)
                    selfWeight = 2.1;
                else if (deckGage == 19)
                    selfWeight = 2.4;
                else if (deckGage == 18)
                    selfWeight = 2.7;
                else if (deckGage == 16)
                    selfWeight = 3.5;
                else
                    selfWeight = 2.0; // Default
            }
            else
            {
                selfWeight = 2.0; // Default value for unknown deck types
            }
        }
    }
}