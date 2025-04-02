using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Models.Properties;
using Core.Utilities;

namespace ETABS.Import.Properties
{
    /// <summary>
    /// Imports material definitions from ETABS E2K file
    /// </summary>
    public class MaterialsImport
    {
        /// <summary>
        /// Imports materials from E2K MATERIAL PROPERTIES section
        /// </summary>
        /// <param name="materialPropertiesSection">The MATERIAL PROPERTIES section content from E2K file</param>
        /// <returns>List of Material objects</returns>
        public List<Material> Import(string materialPropertiesSection)
        {
            var materials = new Dictionary<string, Material>();

            if (string.IsNullOrWhiteSpace(materialPropertiesSection))
                return new List<Material>();

            // Regular expression to match material definition line
            // Format: MATERIAL "Steel" TYPE "Steel" GRADE "Grade 50" WEIGHTPERVOLUME 0.2835648
            var basicPattern = new Regex(@"^\s*MATERIAL\s+""([^""]+)""\s+TYPE\s+""([^""]+)""\s+GRADE\s+""([^""]+)""",
                RegexOptions.Multiline);

            // Pattern for properties (E, U, A)
            var propsPattern = new Regex(@"^\s*MATERIAL\s+""([^""]+)""\s+SYMTYPE\s+""([^""]+)""\s+E\s+([\d\.E\+\-]+)\s+U\s+([\d\.E\+\-]+)(?:\s+A\s+([\d\.E\+\-]+))?",
                RegexOptions.Multiline);

            // Pattern for steel (FY, FU)
            var steelPattern = new Regex(@"^\s*MATERIAL\s+""([^""]+)""\s+FY\s+([\d\.E\+\-]+)\s+FU\s+([\d\.E\+\-]+)",
                RegexOptions.Multiline);

            // Pattern for concrete (FC)
            var concretePattern = new Regex(@"^\s*MATERIAL\s+""([^""]+)""\s+FC\s+([\d\.E\+\-]+)",
                RegexOptions.Multiline);

            // Process basic material definitions
            var basicMatches = basicPattern.Matches(materialPropertiesSection);
            foreach (Match match in basicMatches)
            {
                if (match.Groups.Count >= 4)
                {
                    string name = match.Groups[1].Value;
                    string type = match.Groups[2].Value;
                    string grade = match.Groups[3].Value;

                    // Create material if it doesn't exist already
                    if (!materials.ContainsKey(name))
                    {
                        var material = new Material(name, type);
                        materials[name] = material;
                    }
                }
            }

            // Process elastic properties
            var propsMatches = propsPattern.Matches(materialPropertiesSection);
            foreach (Match match in propsMatches)
            {
                if (match.Groups.Count >= 5)
                {
                    string name = match.Groups[1].Value;
                    string symType = match.Groups[2].Value;
                    double e = Convert.ToDouble(match.Groups[3].Value);
                    double u = Convert.ToDouble(match.Groups[4].Value);

                    double a = 0;
                    if (match.Groups.Count > 5 && !string.IsNullOrEmpty(match.Groups[5].Value))
                    {
                        a = Convert.ToDouble(match.Groups[5].Value);
                    }

                    // Update material if it exists
                    if (materials.TryGetValue(name, out Material material))
                    {
                        material.DirectionalSymmetryType = symType;
                        material.DesignData["elasticModulus"] = e;
                        material.DesignData["poissonsRatio"] = u;
                        material.DesignData["thermalCoeff"] = a;
                    }
                }
            }

            // Process steel properties
            var steelMatches = steelPattern.Matches(materialPropertiesSection);
            foreach (Match match in steelMatches)
            {
                if (match.Groups.Count >= 4)
                {
                    string name = match.Groups[1].Value;
                    double fy = Convert.ToDouble(match.Groups[2].Value);
                    double fu = Convert.ToDouble(match.Groups[3].Value);

                    // Update material if it exists
                    if (materials.TryGetValue(name, out Material material))
                    {
                        material.DesignData["fy"] = fy;
                        material.DesignData["fu"] = fu;
                    }
                }
            }

            // Process concrete properties
            var concreteMatches = concretePattern.Matches(materialPropertiesSection);
            foreach (Match match in concreteMatches)
            {
                if (match.Groups.Count >= 3)
                {
                    string name = match.Groups[1].Value;
                    double fc = Convert.ToDouble(match.Groups[2].Value);

                    // Update material if it exists
                    if (materials.TryGetValue(name, out Material material))
                    {
                        material.DesignData["fc"] = fc;
                    }
                }
            }

            return new List<Material>(materials.Values);
        }
    }
}