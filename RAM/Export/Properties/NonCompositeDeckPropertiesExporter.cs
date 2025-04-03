// NonCompositeDeckPropertiesExporter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Properties;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Export
{
    public class NonCompositeDeckPropertiesExporter : IRAMExporter
    {
        private IModel _model;

        public NonCompositeDeckPropertiesExporter(IModel model)
        {
            _model = model;
        }

        public void Export(BaseModel model)
        {
            // Get non-composite deck properties from RAM model
            INonCompDeckProps nonCompDeckProps = _model.GetNonCompDeckProps();

            // Filter for non-composite deck properties
            var nonCompositeDeckProps = model.Properties.FloorProperties
                .Where(fp => fp.Type?.ToLower() == "noncomposite")
                .ToList();

            // Export each non-composite deck property
            foreach (var floorProp in nonCompositeDeckProps)
            {
                try
                {
                    // Create the non-composite deck property in RAM
                    INonCompDeckProp nonCompDeckProp = nonCompDeckProps.Add(floorProp.Name);

                    // Set basic properties
                    double effectiveThickness = floorProp.Thickness;
                    double elasticModulus = 29000000.0; // Default elastic modulus for steel
                    double poissonsRatio = 0.3; // Default Poisson's ratio for steel
                    double selfWeight = 0.0; // Default self-weight

                    // Override defaults with specified values if available
                    if (floorProp.DeckProperties != null)
                    {
                        if (floorProp.DeckProperties.TryGetValue("effectiveThickness", out object etValue) && etValue is double et)
                        {
                            effectiveThickness = et;
                        }

                        if (floorProp.DeckProperties.TryGetValue("elasticModulus", out object emValue) && emValue is double em)
                        {
                            elasticModulus = em;
                        }

                        if (floorProp.DeckProperties.TryGetValue("poissonsRatio", out object prValue) && prValue is double pr)
                        {
                            poissonsRatio = pr;
                        }

                        if (floorProp.DeckProperties.TryGetValue("selfWeight", out object swValue) && swValue is double sw)
                        {
                            selfWeight = sw;
                        }
                    }

                    // Set properties on the non-composite deck
                    nonCompDeckProp.dThickness = effectiveThickness;
                    nonCompDeckProp.dElasticMod = elasticModulus;
                    nonCompDeckProp.dPoissonsRatio = poissonsRatio;
                    nonCompDeckProp.dSelfWeight = selfWeight;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting non-composite deck property {floorProp.Name}: {ex.Message}");
                }
            }
        }
    }
}