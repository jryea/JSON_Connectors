using System.Collections.Generic;
using System.Text;
using Core.Models.Properties;

namespace ETABS.Import.Properties
{
    // Converts Core Diaphragm objects to ETABS E2K format text
    public class DiaphragmsImport
    {
        // Converts a collection of Diaphragm objects to E2K format text
        
        public string ConvertToE2K(IEnumerable<Diaphragm> diaphragms)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("$ DIAPHRAGM NAMES");

            if (diaphragms == null || !HasItems(diaphragms))
            {
                sb.AppendLine("  DIAPHRAGM \"D1\"    TYPE RIGID");
                return sb.ToString();
            }

            foreach (var diaphragm in diaphragms)
            {
                // Convert enum to string representation for ETABS
                string diaphragmTypeStr = diaphragm.Type == DiaphragmType.Rigid ? "RIGID" : "SEMIRIGID";
                sb.AppendLine($"  DIAPHRAGM \"{diaphragm.Name}\"    TYPE {diaphragmTypeStr}");
            }

            return sb.ToString();
        }
        

        // Determines if an enumerable collection has any items
        private bool HasItems<T>(IEnumerable<T> collection)
        {
            if (collection == null)
                return false;

            using (var enumerator = collection.GetEnumerator())
            {
                return enumerator.MoveNext();
            }
        }

        // Converts the model diaphragm type to ETABS diaphragm type
        private string GetDiaphragmType(DiaphragmType modelType)
        {
            switch (modelType)
            {
                case DiaphragmType.Rigid:
                    return "RIGID";
                case DiaphragmType.SemiRigid:
                    return "SEMIRIGID";
                default:
                    return "RIGID"; // Default to rigid diaphragm
            }
        }
    }
}