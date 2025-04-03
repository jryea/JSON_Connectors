using System.Collections.Generic;
using System.Text;
using Core.Models.Properties;

namespace ETABS.Export.Properties
{
    /// <summary>
    /// Converts Core Diaphragm objects to ETABS E2K format text
    /// </summary>
    public class DiaphragmsToETABS
    {
        /// <summary>
        /// Converts a collection of Diaphragm objects to E2K format text
        /// </summary>
        /// <param name="diaphragms">Collection of Diaphragm objects</param>
        /// <returns>E2K format text for diaphragms</returns>
        public string ConvertToE2K(IEnumerable<Diaphragm> diaphragms)
        {
            StringBuilder sb = new StringBuilder();

            // E2K Diaphragm Section Header
            sb.AppendLine("$ DIAPHRAGM NAMES");

            // Default diaphragm if no diaphragms are defined
            if (diaphragms == null || !HasItems(diaphragms))
            {
                sb.AppendLine("  DIAPHRAGM \"D1\"    TYPE RIGID");
                return sb.ToString();
            }

            // Process each diaphragm and add to output
            foreach (var diaphragm in diaphragms)
            {
                string diaphragmType = GetDiaphragmType(diaphragm.Type);
                sb.AppendLine($"  DIAPHRAGM \"{diaphragm.Name}\"    TYPE {diaphragmType}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Determines if an enumerable collection has any items
        /// </summary>
        /// <typeparam name="T">Type of items in the collection</typeparam>
        /// <param name="collection">The collection to check</param>
        /// <returns>True if the collection has at least one item, otherwise false</returns>
        private bool HasItems<T>(IEnumerable<T> collection)
        {
            if (collection == null)
                return false;

            using (var enumerator = collection.GetEnumerator())
            {
                return enumerator.MoveNext();
            }
        }

        /// <summary>
        /// Converts the model diaphragm type to ETABS diaphragm type
        /// </summary>
        /// <param name="modelType">Diaphragm type from the model</param>
        /// <returns>ETABS diaphragm type</returns>
        private string GetDiaphragmType(string modelType)
        {
            if (string.IsNullOrEmpty(modelType))
                return "RIGID";

            switch (modelType.ToLower())
            {
                case "rigid":
                    return "RIGID";
                case "semi-rigid":
                case "semirigid":
                    return "SEMIRIGID";
                case "flexible":
                    return "FLEXIBLE";
                default:
                    return "RIGID"; // Default to rigid diaphragm
            }
        }
    }
}