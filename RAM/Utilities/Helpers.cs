// Helpers.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Geometry;
using Core.Models.Elements;
using Core.Models.ModelLayout;
using Core.Models.Properties;
using RAMDATAACCESSLib;

namespace RAM.Utilities
{
    public static class Helpers
    {
        // Convert material types
        public static EMATERIALTYPES ConvertMaterialType(Material material)
        {
            if (material == null)
                return EMATERIALTYPES.ESteelMat;

            string materialType = material.Type?.ToLower() ?? "";

            if (materialType.Contains("concrete"))
                return EMATERIALTYPES.EConcreteMat;
            else if (materialType.Contains("joist"))
                return EMATERIALTYPES.ESteelJoistMat;
            else if (materialType.Contains("steel"))
                return EMATERIALTYPES.ESteelMat;
            else
                return EMATERIALTYPES.ESteelMat;
        }

        // Get material type as enum from integer
        public static EMATERIALTYPES GetMaterialType(int materialTypeId)
        {
            switch (materialTypeId)
            {
                case 0:
                    return EMATERIALTYPES.ESteelMat;
                case 1:
                    return EMATERIALTYPES.EConcreteMat;
                case 2:
                    return EMATERIALTYPES.ESteelJoistMat;
                default:
                    return EMATERIALTYPES.ESteelMat;
            }
        }

        // Convert coordinates to inches (RAM standard unit)
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

        // Helper to get floor types from RAM model by names
        public static List<IFloorType> GetFloorTypes(IModel model, List<string> floorTypeNames)
        {
            List<IFloorType> floorTypes = new List<IFloorType>();
            try
            {
                IFloorTypes ramFloorTypes = model.GetFloorTypes();

                // If no specific floor types are requested, return all floor types
                if (floorTypeNames == null || floorTypeNames.Count == 0)
                {
                    for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                    {
                        floorTypes.Add(ramFloorTypes.GetAt(i));
                    }
                    return floorTypes;
                }

                // Otherwise, find the specific floor types requested
                for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                {
                    IFloorType floorType = ramFloorTypes.GetAt(i);
                    if (floorTypeNames.Contains(floorType.strLabel))
                    {
                        floorTypes.Add(floorType);
                    }
                }

                // If no matching floor types found, add at least the first available type
                if (floorTypes.Count == 0 && ramFloorTypes.GetCount() > 0)
                {
                    floorTypes.Add(ramFloorTypes.GetAt(0));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting floor types: {ex.Message}");
            }

            return floorTypes;
        }

        // Convert lines to a nested list structure for processing by floor type
        public static List<List<Line>> ConvertLinesToNestedList(dynamic lineStructure)
        {
            List<List<Line>> nestedList = new List<List<Line>>();

            try
            {
                // Handle different input types that might be passed
                if (lineStructure is IEnumerable<Line> linesList)
                {
                    // If we just have a single flat list, create a single inner list
                    nestedList.Add(linesList.ToList());
                }
                else
                {
                    // Try to get the number of paths in the structure
                    int pathCount = GetPathCount(lineStructure);

                    for (int i = 0; i < pathCount; i++)
                    {
                        var path = GetPath(lineStructure, i);
                        var branch = GetBranch(lineStructure, path);

                        var lineList = new List<Line>();
                        foreach (var lineItem in branch)
                        {
                            if (TryGetLine(lineItem, out Line line))
                            {
                                lineList.Add(line);
                            }
                        }

                        nestedList.Add(lineList);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting lines to nested list: {ex.Message}");
                // Return at least one empty list to allow processing to continue
                if (nestedList.Count == 0)
                {
                    nestedList.Add(new List<Line>());
                }
            }

            return nestedList;
        }

        // Helper methods for dynamic structure handling (simulating the Grasshopper tree structure)
        private static int GetPathCount(dynamic structure)
        {
            try
            {
                return (int)structure.PathCount;
            }
            catch
            {
                return 1; // Default to 1 if can't determine
            }
        }

        private static dynamic GetPath(dynamic structure, int index)
        {
            try
            {
                return structure.get_Path(index);
            }
            catch
            {
                return null;
            }
        }

        private static dynamic GetBranch(dynamic structure, dynamic path)
        {
            try
            {
                return structure.get_Branch(path);
            }
            catch
            {
                return new List<object>();
            }
        }

        private static bool TryGetLine(dynamic item, out Line line)
        {
            line = null;
            try
            {
                if (item != null)
                {
                    line = item as Line;
                    if (line == null && item.Value is Line valueLine)
                    {
                        line = valueLine;
                    }

                    return line != null;
                }
            }
            catch
            {
                // Ignore conversion errors
            }

            return false;
        }
    }
}