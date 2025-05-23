using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit.ViewModels;

namespace Revit.Import
{
    // Single source of truth for import parameters
    public class ImportContext
    {
        public Document RevitDoc { get; set; }
        public UIApplication UIApp { get; set; }
        public string FilePath { get; set; }
        public Dictionary<string, bool> ElementFilters { get; set; }
        public Dictionary<string, bool> MaterialFilters { get; set; }
        public ImportTransformationParameters TransformationParams { get; set; }

        public ImportContext(Document doc, UIApplication uiApp)
        {
            RevitDoc = doc;
            UIApp = uiApp;
            ElementFilters = new Dictionary<string, bool>();
            MaterialFilters = new Dictionary<string, bool>();
        }

        public bool ShouldImportElement(string elementType)
        {
            return ElementFilters.ContainsKey(elementType) && ElementFilters[elementType];
        }

        public bool ShouldImportMaterial(string materialType)
        {
            return MaterialFilters.ContainsKey(materialType) && MaterialFilters[materialType];
        }
    }
}