using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Core.Models;
using Core.Models.Properties;
using Revit.Utilities;

namespace Revit.Import.Elements
{
    // Imports isolated footing elements from JSON into Revit
    public class IsolatedFootingImport
    {
        private readonly DB.Document _doc;
        private Dictionary<string, DB.FamilySymbol> _footingTypes;

        public IsolatedFootingImport(DB.Document doc)
        {
            _doc = doc;
            InitializeFootingTypes();
        }

        // Initialize dictionary of available footing family types
        private void InitializeFootingTypes()
        {
            _footingTypes = new Dictionary<string, DB.FamilySymbol>();

            // Get all structural foundation family symbols
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            collector.OfClass(typeof(DB.FamilySymbol));
            collector.OfCategory(DB.BuiltInCategory.OST_StructuralFoundation);

            foreach (DB.FamilySymbol symbol in collector)
            {
                if (!symbol.IsActive)
                {
                    symbol.Activate();
                }

                string key = symbol.Name.ToUpper();
                if (!_footingTypes.ContainsKey(key))
                {
                    _footingTypes[key] = symbol;
                }

                // Also add by family name + symbol name for more specific matching
                string combinedKey = $"{symbol.Family.Name}_{symbol.Name}".ToUpper();
                if (!_footingTypes.ContainsKey(combinedKey))
                {
                    _footingTypes[combinedKey] = symbol;
                }
            }

            Debug.WriteLine($"Loaded {_footingTypes.Count} footing family types");
        }

        // Find appropriate footing type based on footing dimensions
        private DB.FamilySymbol FindFootingType(CE.IsolatedFooting jsonFooting)
        {
            // Default to the first type if we can't find a match
            DB.FamilySymbol defaultType = _footingTypes.Values.FirstOrDefault();

            if (jsonFooting == null)
            {
                return defaultType;
            }

            // Try to find a rectangular footing type
            var rectangularTypes = _footingTypes.Where(kvp =>
                kvp.Key.Contains("RECTANGULAR") ||
                kvp.Key.Contains("RECT") ||
                kvp.Key.Contains("SQUARE")).ToList();

            if (rectangularTypes.Any())
            {
                return rectangularTypes.First().Value;
            }

            return defaultType;
        }

        public int Import(List<CE.IsolatedFooting> footings, Dictionary<string, DB.ElementId> levelIdMap, BaseModel model)
        {
            int count = 0;

            foreach (var jsonFooting in footings)
            {
                try
                {
                    // Skip if required data is missing
                    if (jsonFooting.Point == null || string.IsNullOrEmpty(jsonFooting.LevelId))
                    {
                        Debug.WriteLine($"Skipping footing {jsonFooting.Id} due to missing data.");
                        continue;
                    }

                    // Get the ElementId for the level
                    if (!levelIdMap.TryGetValue(jsonFooting.LevelId, out DB.ElementId levelId))
                    {
                        Debug.WriteLine($"Skipping footing {jsonFooting.Id} due to missing level mapping.");
                        continue;
                    }

                    // Get Level
                    DB.Level level = _doc.GetElement(levelId) as DB.Level;
                    if (level == null)
                    {
                        Debug.WriteLine($"Skipping footing {jsonFooting.Id} due to invalid level.");
                        continue;
                    }

                    // Get appropriate footing type
                    DB.FamilySymbol familySymbol = FindFootingType(jsonFooting);

                    if (familySymbol == null)
                    {
                        Debug.WriteLine($"Skipping footing {jsonFooting.Id} because no suitable family symbol could be found.");
                        continue;
                    }

                    // Make sure the family symbol is active
                    if (!familySymbol.IsActive)
                    {
                        familySymbol.Activate();
                    }

                    // Create insertion point for footing
                    DB.XYZ footingPoint = Helpers.ConvertToRevitCoordinates(jsonFooting.Point);

                    // Create the structural isolated footing
                    DB.FamilyInstance footing = _doc.Create.NewFamilyInstance(
                        footingPoint,
                        familySymbol,
                        level,
                        DB.Structure.StructuralType.Footing);

                    // Set dimensions if parameters exist
                    SetFootingParameters(footing, jsonFooting);

                    count++;
                    Debug.WriteLine($"Created isolated footing {jsonFooting.Id} with type {familySymbol.Name}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating isolated footing {jsonFooting.Id}: {ex.Message}");
                }
            }

            return count;
        }

        // Set footing parameters based on JSON properties
        private void SetFootingParameters(DB.FamilyInstance footing, CE.IsolatedFooting jsonFooting)
        {
            try
            {
                // Try to parse dimension strings to get values
                if (double.TryParse(jsonFooting.Width, out double width))
                {
                    // Width parameter might have different names in different families
                    SetParameterByName(footing, "Width", width / 12.0); // Convert to feet
                    SetParameterByName(footing, "b", width / 12.0);
                }

                if (double.TryParse(jsonFooting.Length, out double length))
                {
                    // Length parameter might have different names in different families
                    SetParameterByName(footing, "Length", length / 12.0); // Convert to feet
                    SetParameterByName(footing, "a", length / 12.0);
                }

                if (double.TryParse(jsonFooting.Thickness, out double thickness))
                {
                    // Thickness parameter might have different names in different families
                    SetParameterByName(footing, "Thickness", thickness / 12.0); // Convert to feet
                    SetParameterByName(footing, "h", thickness / 12.0);
                    SetParameterByName(footing, "Height", thickness / 12.0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting footing parameters: {ex.Message}");
            }
        }

        // Helper method to set a parameter by name with error handling
        private void SetParameterByName(DB.FamilyInstance instance, string paramName, double value)
        {
            try
            {
                DB.Parameter param = instance.LookupParameter(paramName);
                if (param != null && !param.IsReadOnly && param.StorageType == DB.StorageType.Double)
                {
                    param.Set(value);
                    Debug.WriteLine($"Set parameter '{paramName}' to {value}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set parameter {paramName}: {ex.Message}");
            }
        }
    }
}