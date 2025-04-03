// MaterialExporter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Properties;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Export
{
    public class MaterialExporter : IRAMExporter
    {
        private IModel _model;

        public MaterialExporter(IModel model)
        {
            _model = model;
        }

        public void Export(BaseModel model)
        {
            // Get material dictionaries from RAM
            ISteelMaterials steelMaterials = _model.GetSteelMaterials();
            IConcreteMaterials concreteMaterials = _model.GetConcreteMaterials();

            // Group materials by type
            var steelMatList = model.Properties.Materials.Where(m => m.Type?.ToLower().Contains("steel") == true).ToList();
            var concreteMatList = model.Properties.Materials.Where(m => m.Type?.ToLower().Contains("concrete") == true).ToList();

            // Export steel materials
            foreach (var material in steelMatList)
            {
                try
                {
                    // Check if steel already exists with this name
                    bool exists = false;
                    for (int i = 0; i < steelMaterials.GetCount(); i++)
                    {
                        ISteelMaterial existingMaterial = steelMaterials.GetAt(i);
                        if (existingMaterial.strLabel == material.Name)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        // Create new steel material
                        ISteelMaterial steelMaterial = steelMaterials.Add(material.Name);

                        // Set material properties if available
                        if (material.DesignData != null)
                        {
                            // Set yield strength (Fy)
                            if (material.DesignData.TryGetValue("fy", out object fyValue) && fyValue is double fy)
                            {
                                steelMaterial.dFy = fy;
                            }

                            // Set ultimate strength (Fu)
                            if (material.DesignData.TryGetValue("fu", out object fuValue) && fuValue is double fu)
                            {
                                steelMaterial.dFu = fu;
                            }

                            // Set elastic modulus (E)
                            if (material.DesignData.TryGetValue("elasticModulus", out object eValue) && eValue is double e)
                            {
                                steelMaterial.dE = e;
                            }

                            // Set Poisson's ratio
                            if (material.DesignData.TryGetValue("poissonsRatio", out object prValue) && prValue is double poissonRatio)
                            {
                                steelMaterial.dPoisson = poissonRatio;
                            }

                            // Set weight density
                            if (material.DesignData.TryGetValue("weightDensity", out object wdValue) && wdValue is double weightDensity)
                            {
                                steelMaterial.dUnitWt = weightDensity;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting steel material {material.Name}: {ex.Message}");
                }
            }

            // Export concrete materials
            foreach (var material in concreteMatList)
            {
                try
                {
                    // Check if concrete already exists with this name
                    bool exists = false;
                    for (int i = 0; i < concreteMaterials.GetCount(); i++)
                    {
                        IConcreteMaterial existingMaterial = concreteMaterials.GetAt(i);
                        if (existingMaterial.strLabel == material.Name)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        // Create new concrete material
                        IConcreteMaterial concreteMaterial = concreteMaterials.Add(material.Name);

                        // Set material properties if available
                        if (material.DesignData != null)
                        {
                            // Set compressive strength (f'c)
                            if (material.DesignData.TryGetValue("fc", out object fcValue) && fcValue is double fc)
                            {
                                concreteMaterial.dFcPrime = fc;
                            }

                            // Set elastic modulus (E)
                            if (material.DesignData.TryGetValue("elasticModulus", out object eValue) && eValue is double e)
                            {
                                concreteMaterial.dE = e;
                            }

                            // Set Poisson's ratio
                            if (material.DesignData.TryGetValue("poissonsRatio", out object prValue) && prValue is double poissonRatio)
                            {
                                concreteMaterial.dPoisson = poissonRatio;
                            }

                            // Set weight density
                            if (material.DesignData.TryGetValue("weightDensity", out object wdValue) && wdValue is double weightDensity)
                            {
                                concreteMaterial.dUnitWt = weightDensity;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting concrete material {material.Name}: {ex.Message}");
                }
            }
        }
    }
}