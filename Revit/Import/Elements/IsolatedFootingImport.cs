using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Core.Models;
using Revit.Utilities;
using System.Diagnostics;

namespace Revit.Import.Elements
{
    public class IsolatedFootingImport
    {
        private readonly DB.Document _doc;
        private Dictionary<string, DB.FamilySymbol> _footingTypes;
        private DB.Family _imegRectangularFootingFamily;
        private List<DB.FamilySymbol> _imegFootingSymbols;

        public IsolatedFootingImport(DB.Document doc)
        {
            _doc = doc;
            _footingTypes = new Dictionary<string, DB.FamilySymbol>();
            _imegFootingSymbols = new List<DB.FamilySymbol>();
            InitializeFootingTypes();
        }

        // Initialize dictionary of available footing family types
        private void InitializeFootingTypes()
        {
            _footingTypes = new Dictionary<string, DB.FamilySymbol>();
            _imegFootingSymbols = new List<DB.FamilySymbol>();

            // Get all families in the document
            var collector = new DB.FilteredElementCollector(_doc).OfClass(typeof(DB.Family));

            // First try to find the specific IMEG footing family
            _imegRectangularFootingFamily = collector
                .Cast<DB.Family>()
                .FirstOrDefault(f => f.Name.Equals("IMEG_Footing-Rectangular", StringComparison.OrdinalIgnoreCase));

            if (_imegRectangularFootingFamily != null)
            {
                Debug.WriteLine("Found IMEG_Footing-Rectangular family");
                LoadIMEGFootingSymbols();
            }
            else
            {
                // Try looking for any other rectangular footing families
                Debug.WriteLine("IMEG_Footing-Rectangular family not found, looking for alternatives");
                _imegRectangularFootingFamily = collector
                    .Cast<DB.Family>()
                    .FirstOrDefault(f => f.Name.ToUpper().Contains("FOOTING") &&
                                        f.Name.ToUpper().Contains("RECT"));

                if (_imegRectangularFootingFamily != null)
                {
                    Debug.WriteLine($"Found alternative rectangular footing family: {_imegRectangularFootingFamily.Name}");
                    LoadIMEGFootingSymbols();
                }
                else
                {
                    Debug.WriteLine("No rectangular footing family found, loading generic footing symbols");
                    LoadGenericFootingSymbols();
                }
            }

            Debug.WriteLine($"Loaded {_footingTypes.Count} footing types");
            foreach (var type in _footingTypes)
            {
                Debug.WriteLine($"  - {type.Key}: {type.Value.Name}");
            }
        }

        // Load all symbols for the IMEG_Footing-Rectangular family or alternative
        private void LoadIMEGFootingSymbols()
        {
            _imegFootingSymbols = new List<DB.FamilySymbol>();
            Debug.WriteLine("Loading symbols for rectangular footing family");

            try
            {
                // Get all symbols in the family
                var symbolIds = _imegRectangularFootingFamily.GetFamilySymbolIds();
                foreach (var id in symbolIds)
                {
                    var symbol = _doc.GetElement(id) as DB.FamilySymbol;
                    if (symbol != null)
                    {
                        ActivateSymbol(symbol);
                        _imegFootingSymbols.Add(symbol);
                        _footingTypes[symbol.Name.ToUpper()] = symbol;
                        Debug.WriteLine($"  Added footing type: {symbol.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading rectangular footing symbols: {ex.Message}");
            }

            Debug.WriteLine($"Loaded {_imegFootingSymbols.Count} rectangular footing symbols");
        }

        // Load all generic structural foundation symbols
        private void LoadGenericFootingSymbols()
        {
            Debug.WriteLine("Loading generic footing symbols");
            try
            {
                var collector = new DB.FilteredElementCollector(_doc)
                    .OfClass(typeof(DB.FamilySymbol))
                    .OfCategory(DB.BuiltInCategory.OST_StructuralFoundation);

                foreach (DB.FamilySymbol symbol in collector)
                {
                    // Filter to only include isolated/spread footings
                    bool isIsolatedFooting = IsIsolatedFootingType(symbol);
                    if (isIsolatedFooting)
                    {
                        ActivateSymbol(symbol);
                        string key = symbol.Name.ToUpper();
                        if (!_footingTypes.ContainsKey(key))
                        {
                            _footingTypes[key] = symbol;
                            Debug.WriteLine($"  Added generic footing type: {symbol.Name}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading generic footing symbols: {ex.Message}");
            }
        }

        // Determine if a family symbol is an isolated/spread footing type
        private bool IsIsolatedFootingType(DB.FamilySymbol symbol)
        {
            try
            {
                string familyName = symbol.Family.Name.ToUpper();
                string symbolName = symbol.Name.ToUpper();

                return (familyName.Contains("FOOTING") || symbolName.Contains("FOOTING")) &&
                      (familyName.Contains("ISOLATED") || familyName.Contains("SPREAD") ||
                       symbolName.Contains("ISOLATED") || symbolName.Contains("SPREAD") ||
                       !familyName.Contains("WALL") && !familyName.Contains("CONTINUOUS") &&
                       !symbolName.Contains("WALL") && !symbolName.Contains("CONTINUOUS"));
            }
            catch
            {
                return false;
            }
        }

        // Activate a family symbol if it is not already active
        private void ActivateSymbol(DB.FamilySymbol symbol)
        {
            try
            {
                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    Debug.WriteLine($"  Activated symbol: {symbol.Name}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"  Error activating symbol {symbol.Name}: {ex.Message}");
            }
        }

        // Format dimensions to Revit format: "2'-0" x 2'-0" x 1'-0""
        private string FormatFootingDimensions(double lengthFeet, double widthFeet, double thicknessFeet)
        {
            return $"{FormatDimension(lengthFeet)} x {FormatDimension(widthFeet)} x {FormatDimension(thicknessFeet)}";
        }

        // Format a single dimension to Revit format (e.g., 2.0 -> "2'-0"", 1.5 -> "1'-6"")
        private string FormatDimension(double dimensionInFeet)
        {
            int feet = (int)Math.Floor(dimensionInFeet);
            double inchesDecimal = (dimensionInFeet - feet) * 12;
            int inches = (int)Math.Round(inchesDecimal);

            if (inches == 12)
            {
                feet += 1;
                inches = 0;
            }

            return $"{feet}'-{inches}\"";
        }

        // Find or create appropriate footing type based on dimensions
        private DB.FamilySymbol GetFootingType(CE.IsolatedFooting jsonFooting)
        {
            // Convert dimensions from inches to feet for formatted display
            double lengthFeet = jsonFooting.Length / 12.0;
            double widthFeet = jsonFooting.Width / 12.0;
            double thicknessFeet = jsonFooting.Thickness / 12.0;

            // Format the dimensions exactly as they appear in Revit
            string preferredTypeName = FormatFootingDimensions(lengthFeet, widthFeet, thicknessFeet);
            Debug.WriteLine($"Looking for footing type with name: {preferredTypeName}");

            // If we have IMEG or similar rectangular footings, try to find or duplicate a matching type
            if (_imegRectangularFootingFamily != null && _imegFootingSymbols.Count > 0)
            {
                // First try exact match by name
                var exactMatch = _imegFootingSymbols
                    .FirstOrDefault(s => s.Name.Equals(preferredTypeName, StringComparison.OrdinalIgnoreCase));

                if (exactMatch != null)
                {
                    Debug.WriteLine($"Found exact matching footing type: {exactMatch.Name}");
                    return exactMatch;
                }

                // Try to find a type with the same thickness, which we can duplicate
                var matchingThicknessType = _imegFootingSymbols
                    .FirstOrDefault(s => s.Name.EndsWith(FormatDimension(thicknessFeet)));

                if (matchingThicknessType != null)
                {
                    Debug.WriteLine($"Found footing with matching thickness: {matchingThicknessType.Name}");
                    var duplicated = DuplicateFootingType(matchingThicknessType, preferredTypeName, jsonFooting);
                    if (duplicated != null)
                    {
                        Debug.WriteLine($"Successfully duplicated footing type as: {duplicated.Name}");
                        return duplicated;
                    }
                }
                else
                {
                    // If no matching thickness, just take the first one and duplicate it
                    var baseType = _imegFootingSymbols.FirstOrDefault();
                    if (baseType != null)
                    {
                        Debug.WriteLine($"Using base type for duplication: {baseType.Name}");
                        var duplicated = DuplicateFootingType(baseType, preferredTypeName, jsonFooting);
                        if (duplicated != null)
                        {
                            Debug.WriteLine($"Successfully duplicated footing type as: {duplicated.Name}");
                            return duplicated;
                        }
                    }
                }
            }

            // Fallback: try to find a type in our collection that might work
            foreach (var type in _footingTypes.Values)
            {
                // Try to get length/width parameters to see if they match
                try
                {
                    var lenParam = type.get_Parameter(DB.BuiltInParameter.STRUCTURAL_FOUNDATION_LENGTH);
                    var widthParam = type.get_Parameter(DB.BuiltInParameter.STRUCTURAL_FOUNDATION_WIDTH);

                    if (lenParam != null && widthParam != null &&
                        Math.Abs(lenParam.AsDouble() - lengthFeet) < 0.01 &&
                        Math.Abs(widthParam.AsDouble() - widthFeet) < 0.01)
                    {
                        Debug.WriteLine($"Found footing with matching dimensions: {type.Name}");
                        return type;
                    }
                }
                catch
                {
                    // Skip if we can't read parameters
                    continue;
                }
            }

            // Last resort - just use the first available type
            var defaultType = _footingTypes.Values.FirstOrDefault();
            Debug.WriteLine($"No matching footing type found, using default: {defaultType?.Name ?? "NULL"}");
            return defaultType;
        }

        // Duplicate a footing type and set its parameters
        private DB.FamilySymbol DuplicateFootingType(DB.FamilySymbol baseType, string newName, CE.IsolatedFooting jsonFooting)
        {
            try
            {
                // Check if a type with this name already exists
                foreach (var symbol in _imegFootingSymbols)
                {
                    if (symbol.Name.Equals(newName, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"Footing type {newName} already exists, using existing type");
                        return symbol;
                    }
                }

                Debug.WriteLine($"Duplicating footing type {baseType.Name} as {newName}");
                var newType = baseType.Duplicate(newName) as DB.FamilySymbol;
                if (newType != null)
                {
                    // Convert dimensions from inches to feet for Revit
                    double lengthFeet = jsonFooting.Length / 12.0;
                    double widthFeet = jsonFooting.Width / 12.0;
                    double thicknessFeet = jsonFooting.Thickness / 12.0;

                    // Set parameters - make sure to handle errors for each parameter separately
                    Debug.WriteLine($"Setting footing dimensions: L={lengthFeet}', W={widthFeet}', T={thicknessFeet}'");

                    try
                    {
                        var lengthParam = newType.get_Parameter(DB.BuiltInParameter.STRUCTURAL_FOUNDATION_LENGTH);
                        if (lengthParam != null && !lengthParam.IsReadOnly)
                        {
                            lengthParam.Set(lengthFeet);
                            Debug.WriteLine("  Set length parameter successfully");
                        }
                        else
                        {
                            Debug.WriteLine("  Length parameter not found or read-only");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"  Error setting length: {ex.Message}");
                    }

                    try
                    {
                        var widthParam = newType.get_Parameter(DB.BuiltInParameter.STRUCTURAL_FOUNDATION_WIDTH);
                        if (widthParam != null && !widthParam.IsReadOnly)
                        {
                            widthParam.Set(widthFeet);
                            Debug.WriteLine("  Set width parameter successfully");
                        }
                        else
                        {
                            Debug.WriteLine("  Width parameter not found or read-only");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"  Error setting width: {ex.Message}");
                    }

                    try
                    {
                        var thicknessParam = newType.LookupParameter("Thickness");
                        if (thicknessParam != null && !thicknessParam.IsReadOnly)
                        {
                            thicknessParam.Set(thicknessFeet);
                            Debug.WriteLine("  Set thickness parameter successfully");
                        }
                        else
                        {
                            Debug.WriteLine("  Thickness parameter not found or read-only");

                            // Try alternative thickness parameter names
                            string[] altThicknessParams = { "Height", "h", "THICKNESS", "Depth", "d" };
                            foreach (var paramName in altThicknessParams)
                            {
                                var altParam = newType.LookupParameter(paramName);
                                if (altParam != null && !altParam.IsReadOnly)
                                {
                                    altParam.Set(thicknessFeet);
                                    Debug.WriteLine($"  Set alternative thickness parameter '{paramName}' successfully");
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"  Error setting thickness: {ex.Message}");
                    }

                    // Add to cached collections
                    _imegFootingSymbols.Add(newType);
                    _footingTypes[newType.Name.ToUpper()] = newType;

                    return newType;
                }
                else
                {
                    Debug.WriteLine("Duplication failed - null result returned");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error duplicating footing type: {ex.Message}");
            }

            return null;
        }

        // Main import method - imports isolated footings from the JSON model into Revit
        public int Import(List<CE.IsolatedFooting> footings, Dictionary<string, DB.ElementId> levelIdMap, BaseModel model)
        {
            if (footings == null || footings.Count == 0)
            {
                Debug.WriteLine("No footings to import");
                return 0;
            }

            int count = 0;
            Debug.WriteLine($"Starting import of {footings.Count} isolated footings");

            foreach (var jsonFooting in footings)
            {
                try
                {
                    Debug.WriteLine($"\nProcessing footing {jsonFooting.Id}");

                    // Verify required data exists
                    if (jsonFooting.Point == null)
                    {
                        Debug.WriteLine("  Skipping footing - missing point data");
                        continue;
                    }

                    if (string.IsNullOrEmpty(jsonFooting.LevelId))
                    {
                        Debug.WriteLine("  Skipping footing - missing level ID");
                        continue;
                    }

                    // Verify dimensions are valid
                    if (jsonFooting.Length <= 0 || jsonFooting.Width <= 0 || jsonFooting.Thickness <= 0)
                    {
                        Debug.WriteLine($"  Skipping footing - invalid dimensions: L={jsonFooting.Length}, W={jsonFooting.Width}, T={jsonFooting.Thickness}");

                        // Set default dimensions if needed
                        if (jsonFooting.Length <= 0) jsonFooting.Length = 36.0; // 3'
                        if (jsonFooting.Width <= 0) jsonFooting.Width = 36.0;   // 3'
                        if (jsonFooting.Thickness <= 0) jsonFooting.Thickness = 12.0; // 1'

                        Debug.WriteLine($"  Using default dimensions: L={jsonFooting.Length}, W={jsonFooting.Width}, T={jsonFooting.Thickness}");
                    }

                    Debug.WriteLine($"  Footing dimensions: {jsonFooting.Length}\" x {jsonFooting.Width}\" x {jsonFooting.Thickness}\"");

                    // Get the level for this footing
                    if (!levelIdMap.TryGetValue(jsonFooting.LevelId, out DB.ElementId levelId))
                    {
                        Debug.WriteLine("  Skipping footing - level ID not found in mapping");
                        continue;
                    }

                    var level = _doc.GetElement(levelId) as DB.Level;
                    if (level == null)
                    {
                        Debug.WriteLine("  Skipping footing - invalid level");
                        continue;
                    }

                    Debug.WriteLine($"  Footing level: {level.Name} (ID: {level.Id})");

                    // Find the appropriate footing family type for these dimensions
                    var familySymbol = GetFootingType(jsonFooting);
                    if (familySymbol == null)
                    {
                        Debug.WriteLine("  Skipping footing - no suitable family symbol found");
                        continue;
                    }

                    Debug.WriteLine($"  Using footing type: {familySymbol.Name}");

                    // Ensure symbol is activated
                    ActivateSymbol(familySymbol);

                    // Create the footing instance
                    var footingPoint = Helpers.ConvertToRevitCoordinates(jsonFooting.Point);
                    Debug.WriteLine($"  Creating footing at point: X={footingPoint.X}, Y={footingPoint.Y}, Z={footingPoint.Z}");

                    // Check for existing footings at this location
                    bool footingExists = CheckForExistingFooting(footingPoint);
                    if (footingExists)
                    {
                        Debug.WriteLine("  Skipping footing - already exists at this location");
                        continue;
                    }

                    var footing = _doc.Create.NewFamilyInstance(
                        footingPoint,
                        familySymbol,
                        level,
                        DB.Structure.StructuralType.Footing);

                    if (footing != null)
                    {
                        // Try to set instance parameters (some family types allow this)
                        TrySetFootingInstanceParameters(footing, jsonFooting);

                        count++;
                        Debug.WriteLine($"  Successfully created footing instance (ID: {footing.Id})");
                    }
                    else
                    {
                        Debug.WriteLine("  Failed to create footing instance");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"  Error creating footing {jsonFooting.Id}: {ex.Message}");
                }
            }

            Debug.WriteLine($"Imported {count} footings successfully");
            return count;
        }

        // Check if a footing already exists at this location
        private bool CheckForExistingFooting(DB.XYZ location)
        {
            try
            {
                var collector = new DB.FilteredElementCollector(_doc)
                    .OfCategory(DB.BuiltInCategory.OST_StructuralFoundation)
                    .OfClass(typeof(DB.FamilyInstance))
                    .WhereElementIsNotElementType();

                const double tolerance = 0.1; // 0.1 feet (about 1.2 inches)

                foreach (DB.FamilyInstance instance in collector)
                {
                    var locPoint = instance.Location as DB.LocationPoint;
                    if (locPoint != null)
                    {
                        var existingPoint = locPoint.Point;
                        if (existingPoint.DistanceTo(location) < tolerance)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"  Error checking for existing footings: {ex.Message}");
            }

            return false;
        }

        // Try to set instance parameters for the footing if the family supports it
        private void TrySetFootingInstanceParameters(DB.FamilyInstance footingInstance, CE.IsolatedFooting jsonFooting)
        {
            try
            {
                // Some footing families allow setting dimensions at the instance level
                double lengthFeet = jsonFooting.Length / 12.0;
                double widthFeet = jsonFooting.Width / 12.0;
                double thicknessFeet = jsonFooting.Thickness / 12.0;

                // Try setting length and width
                var lengthParam = footingInstance.get_Parameter(DB.BuiltInParameter.STRUCTURAL_FOUNDATION_LENGTH);
                if (lengthParam != null && !lengthParam.IsReadOnly)
                {
                    lengthParam.Set(lengthFeet);
                    Debug.WriteLine("  Set instance length parameter");
                }

                var widthParam = footingInstance.get_Parameter(DB.BuiltInParameter.STRUCTURAL_FOUNDATION_WIDTH);
                if (widthParam != null && !widthParam.IsReadOnly)
                {
                    widthParam.Set(widthFeet);
                    Debug.WriteLine("  Set instance width parameter");
                }

                // Try thickness (using common parameter names)
                string[] thicknessParamNames = { "Thickness", "Height", "Depth", "h", "d" };
                foreach (var paramName in thicknessParamNames)
                {
                    var thicknessParam = footingInstance.LookupParameter(paramName);
                    if (thicknessParam != null && !thicknessParam.IsReadOnly)
                    {
                        thicknessParam.Set(thicknessFeet);
                        Debug.WriteLine($"  Set instance thickness parameter '{paramName}'");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"  Error setting instance parameters: {ex.Message}");
            }
        }
    }
}