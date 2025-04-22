using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CE = Core.Models.Elements;
using Core.Models;
using Revit.Utilities;

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
            InitializeFootingTypes();
        }

        // Initialize dictionary of available footing family types
        private void InitializeFootingTypes()
        {
            _footingTypes = new Dictionary<string, DB.FamilySymbol>();

            // Get all structural foundation family symbols
            var collector = new DB.FilteredElementCollector(_doc).OfClass(typeof(DB.Family));

            // Find the IMEG_Footing-Rectangular family
            _imegRectangularFootingFamily = collector
                .Cast<DB.Family>()
                .FirstOrDefault(f => f.Name.Equals("IMEG_Footing-Rectangular", StringComparison.OrdinalIgnoreCase));

            if (_imegRectangularFootingFamily != null)
            {
                LoadIMEGFootingSymbols();
            }
            else
            {
                LoadGenericFootingSymbols();
            }
        }

        // Load all symbols for the IMEG_Footing-Rectangular family
        private void LoadIMEGFootingSymbols()
        {
            _imegFootingSymbols = _imegRectangularFootingFamily
                .GetFamilySymbolIds()
                .Select(id => _doc.GetElement(id) as DB.FamilySymbol)
                .Where(symbol => symbol != null)
                .ToList();

            foreach (var symbol in _imegFootingSymbols)
            {
                ActivateSymbol(symbol);
                _footingTypes[symbol.Name.ToUpper()] = symbol;
            }
        }

        // Load all generic footing symbols
        private void LoadGenericFootingSymbols()
        {
            var collector = new DB.FilteredElementCollector(_doc)
                .OfClass(typeof(DB.FamilySymbol))
                .OfCategory(DB.BuiltInCategory.OST_StructuralFoundation);

            foreach (DB.FamilySymbol symbol in collector)
            {
                ActivateSymbol(symbol);
                string key = symbol.Name.ToUpper();
                if (!_footingTypes.ContainsKey(key))
                {
                    _footingTypes[key] = symbol;
                }
            }
        }

        // Activate a family symbol if it is not already active
        private void ActivateSymbol(DB.FamilySymbol symbol)
        {
            if (!symbol.IsActive)
            {
                symbol.Activate();
            }
        }

        // Format dimensions to Revit format: "2'-0" x 2'-0" x 1'-0""
        private string FormatFootingDimensions(double length, double width, double thickness)
        {
            return $"{FormatDimension(length)}  x  {FormatDimension(width)}  x  {FormatDimension(thickness)}";
        }

        // Format a single dimension to Revit format (e.g., 2.0 -> "2'-0"", 1.5 -> "1'-6"")
        private string FormatDimension(double dimension)
        {
            int feet = (int)Math.Floor(dimension);
            double inches = Math.Round((dimension - feet) * 12);

            if (inches == 12)
            {
                feet += 1;
                inches = 0;
            }

            return inches == 0 ? $"{feet}'-0\"" : $"{feet}'-{inches}\"";
        }

        // Find or create appropriate footing type based on dimensions
        private DB.FamilySymbol GetFootingType(CE.IsolatedFooting jsonFooting)
        {
            string preferredTypeName = FormatFootingDimensions(jsonFooting.Length, jsonFooting.Width, jsonFooting.Thickness);

            // If IMEG family is available, try to find or duplicate a matching type
            if (_imegRectangularFootingFamily != null && _imegFootingSymbols.Count > 0)
            {
                var exactMatch = _imegFootingSymbols
                    .FirstOrDefault(s => s.Name.Equals(preferredTypeName, StringComparison.OrdinalIgnoreCase));

                if (exactMatch != null)
                {
                    return exactMatch;
                }

                var matchingThicknessType = _imegFootingSymbols
                    .FirstOrDefault(s => s.Name.EndsWith(FormatDimension(jsonFooting.Thickness)));

                if (matchingThicknessType != null)
                {
                    return DuplicateFootingType(matchingThicknessType, preferredTypeName, jsonFooting);
                }
            }

            // Fall back to any available type
            return _footingTypes.Values.FirstOrDefault();
        }

        // Duplicate a footing type and set its parameters
        private DB.FamilySymbol DuplicateFootingType(DB.FamilySymbol baseType, string newName, CE.IsolatedFooting jsonFooting)
        {
            try
            {
                var newType = baseType.Duplicate(newName) as DB.FamilySymbol;
                if (newType != null)
                {
                    newType.Name = newName;
                    SetFootingTypeParameters(newType, jsonFooting);
                    _imegFootingSymbols.Add(newType);
                    _footingTypes[newType.Name.ToUpper()] = newType;
                    return newType;
                }
            }
            catch
            {
                // Ignore duplication errors and fall back to default
            }

            return null;
        }

        // Set parameters for a footing type
        private void SetFootingTypeParameters(DB.FamilySymbol type, CE.IsolatedFooting jsonFooting)
        {
            type.get_Parameter(DB.BuiltInParameter.STRUCTURAL_FOUNDATION_LENGTH)?.Set(jsonFooting.Length);
            type.get_Parameter(DB.BuiltInParameter.STRUCTURAL_FOUNDATION_WIDTH)?.Set(jsonFooting.Width);

            var thicknessParam = type.LookupParameter("Thickness");
            if (thicknessParam != null && !thicknessParam.IsReadOnly)
            {
                thicknessParam.Set(jsonFooting.Thickness);
            }
        }

        public int Import(List<CE.IsolatedFooting> footings, Dictionary<string, DB.ElementId> levelIdMap, BaseModel model)
        {
            if (footings == null || footings.Count == 0)
            {
                return 0;
            }

            int count = 0;

            foreach (var jsonFooting in footings)
            {
                if (jsonFooting.Point == null || string.IsNullOrEmpty(jsonFooting.LevelId))
                {
                    continue;
                }

                if (!levelIdMap.TryGetValue(jsonFooting.LevelId, out DB.ElementId levelId))
                {
                    continue;
                }

                var level = _doc.GetElement(levelId) as DB.Level;
                if (level == null)
                {
                    continue;
                }

                var familySymbol = GetFootingType(jsonFooting);
                if (familySymbol == null)
                {
                    continue;
                }

                ActivateSymbol(familySymbol);

                var footingPoint = Helpers.ConvertToRevitCoordinates(jsonFooting.Point);
                var footing = _doc.Create.NewFamilyInstance(
                    footingPoint,
                    familySymbol,
                    level,
                    DB.Structure.StructuralType.Footing);

                if (footing != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
