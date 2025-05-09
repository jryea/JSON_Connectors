using Core.Models.Properties;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;

public class MaterialExport
{
    public List<Material> Export(string materialPropertiesSection)
    {
        var materials = new Dictionary<string, Material>();

        if (string.IsNullOrWhiteSpace(materialPropertiesSection))
            return new List<Material>();

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

        // Process basic material definitions
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
                    // Convert string type to MaterialType enum
                    MaterialType materialType = GetMaterialTypeFromString(type);
                    var material = new Material(name, materialType);
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
                    // Convert symType string to DirectionalSymmetryType enum
                    material.DirectionalSymmetryType = ParseDirectionalSymmetryType(symType);
                    material.ElasticModulus = e;
                    material.PoissonsRatio = u;
                    material.CoefficientOfThermalExpansion = a;
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
                if (materials.TryGetValue(name, out Material material) && material.Type == MaterialType.Steel)
                {
                    // Ensure SteelProps is initialized
                    if (material.SteelProps == null)
                        material.SteelProps = new SteelProperties();

                    material.SteelProps.Fy = fy;
                    material.SteelProps.Fu = fu;
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
                if (materials.TryGetValue(name, out Material material) && material.Type == MaterialType.Concrete)
                {
                    // Ensure ConcreteProps is initialized
                    if (material.ConcreteProps == null)
                        material.ConcreteProps = new ConcreteProperties();

                    material.ConcreteProps.Fc = fc;
                }
            }
        }

        return new List<Material>(materials.Values);
    }

    private MaterialType GetMaterialTypeFromString(string typeString)
    {
        if (string.Equals(typeString, "Steel", StringComparison.OrdinalIgnoreCase))
            return MaterialType.Steel;
        else
            return MaterialType.Concrete;
    }

    private DirectionalSymmetryType ParseDirectionalSymmetryType(string symType)
    {
        if (string.Equals(symType, "Orthotropic", StringComparison.OrdinalIgnoreCase))
            return DirectionalSymmetryType.Orthotropic;
        else if (string.Equals(symType, "Anisotropic", StringComparison.OrdinalIgnoreCase))
            return DirectionalSymmetryType.Anisotropic;
        else
            return DirectionalSymmetryType.Isotropic;
    }
}