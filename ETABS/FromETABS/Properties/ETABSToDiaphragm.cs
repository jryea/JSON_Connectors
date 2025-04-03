using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Models.Properties;
using Core.Utilities;

namespace ETABS.Import.Properties
{
    // Imports diaphragm definitions from ETABS E2K file
  
    public class ETABSToDiaphragm
    {
        // Imports diaphragms from E2K DIAPHRAGM NAMES section
        public List<Diaphragm> Import(string diaphragmNamesSection)
        {
            var diaphragms = new List<Diaphragm>();

            if (string.IsNullOrWhiteSpace(diaphragmNamesSection))
                return diaphragms;

            // Regular expression to match diaphragm definition
            // Format: DIAPHRAGM "D1" TYPE RIGID
            var diaphragmPattern = new Regex(@"^\s*DIAPHRAGM\s+""([^""]+)""\s+TYPE\s+(\w+)",
                RegexOptions.Multiline);

            var matches = diaphragmPattern.Matches(diaphragmNamesSection);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 3)
                {
                    string name = match.Groups[1].Value;
                    string type = match.Groups[2].Value;

                    // Create diaphragm object
                    var diaphragm = new Diaphragm
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.DIAPHRAGM),
                        Name = name,
                        Type = ConvertDiaphragmType(type)
                    };

                    // Add additional properties
                    diaphragm.Properties["etabsType"] = type;

                    diaphragms.Add(diaphragm);
                }
            }
            return diaphragms;
        }

        // Converts ETABS diaphragm type to model diaphragm type
        private string ConvertDiaphragmType(string etabsType)
        {
            switch (etabsType.ToUpper())
            {
                case "RIGID":
                    return "Rigid";
                case "SEMIRIGID":
                    return "Semi-Rigid";
                case "FLEXIBLE":
                    return "Flexible";
                default:
                    return "Rigid"; // Default to rigid diaphragm
            }
        }
    }
}