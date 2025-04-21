using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Core.Models;
using Core.Models.Properties;
using Revit.Utilities;
using Autodesk.Revit.DB;

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

                // Add by family name + symbol name for more specific matching
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

            // Try to match by name
            if (!string.IsNullOrEmpty(jsonFooting.Id))
            {
                string typeName = jsonFooting.Id.ToUpper();
                if (_footingTypes.TryGetValue(typeName, out DB.FamilySymbol typeByName))
                {
                    return typeByName;
                }
            }

            // Try to match by dimensions (e.g., width and length)
            var matches = _footingTypes.Where(kvp =>
                kvp.Key.Contains("RECTANGULAR") || kvp.Key.Contains("SQUARE")).ToList();

            if (matches.Any())
            {
                return matches.First().Value;
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
                    // Validate input data
                    if (jsonFooting.Point == null || string.IsNullOrEmpty(jsonFooting.LevelId))
                    {
                        Debug.WriteLine($"Skipping footing {jsonFooting.Id} due to missing data.");
                        continue;
                    }

                    if (!levelIdMap.TryGetValue(jsonFooting.LevelId, out DB.ElementId levelId))
                    {
                        Debug.WriteLine($"Skipping footing {jsonFooting.Id} due to missing level mapping.");
                        continue;
                    }

                    // Get the level
                    DB.Level level = _doc.GetElement(levelId) as DB.Level;
                    if (level == null)
                    {
                        Debug.WriteLine($"Skipping footing {jsonFooting.Id} due to invalid level.");
                        continue;
                    }

                    // Find the appropriate footing type
                    DB.FamilySymbol familySymbol = FindFootingType(jsonFooting);
                    if (familySymbol == null)
                    {
                        Debug.WriteLine($"Skipping footing {jsonFooting.Id} because no suitable family symbol could be found.");
                        continue;
                    }

                    // Ensure the family symbol is active
                    if (!familySymbol.IsActive)
                    {
                        familySymbol.Activate();
                    }

                    // Create the footing
                    DB.XYZ footingPoint = Helpers.ConvertToRevitCoordinates(jsonFooting.Point);
                    DB.FamilyInstance footing = _doc.Create.NewFamilyInstance(
                        footingPoint,
                        familySymbol,
                        level,
                        DB.Structure.StructuralType.Footing);

                    // Set parameters
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
                // Set width, length, and thickness
                SetParameterByName(footing, "Width", jsonFooting.Width / 12.0); // Convert to feet
                SetParameterByName(footing, "Length", jsonFooting.Length / 12.0); // Convert to feet
                SetParameterByName(footing, "Thickness", jsonFooting.Thickness / 12.0); // Convert to feet
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

