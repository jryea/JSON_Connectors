// MaterialImporter.cs
using System;
using System.Collections.Generic;
using Core.Models.Properties;
using Core.Utilities;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Import
{
    public class MaterialToRAM : IRAMImporter<List<Material>>
    {
        private IModel _model;

        public MaterialToRAM(IModel model)
        {
            _model = model;
        }

        public List<Material> Import()
        {
            var materials = new List<Material>();

            try
            {
                // Import steel materials
                ImportSteelMaterials(materials);

                // Import concrete materials
                ImportConcreteMaterials(materials);

                // If no materials were found, create default steel and concrete materials
                if (materials.Count == 0)
                {
                    materials.Add(new Material
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL),
                        Name = "A992 Steel",
                        Type = "Steel",
                        DesignData = new Dictionary<string, object>
                        {
                            { "fy", 50000.0 },
                            { "fu", 65000.0 },
                            { "elasticModulus", 29000000.0 },
                            { "poissonsRatio", 0.3 },
                            { "weightDensity", 490.0 }
                        }
                    });

                    materials.Add(new Material
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL),
                        Name = "4000 psi Concrete",
                        Type = "Concrete",
                        DesignData = new Dictionary<string, object>
                        {
                            { "fc", 4000.0 },
                            { "elasticModulus", 3600000.0 },
                            { "poissonsRatio", 0.2 },
                            { "weightDensity", 150.0 }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing materials: {ex.Message}");

                // Create default materials
                materials.Add(new Material
                {
                    Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL),
                    Name = "A992 Steel",
                    Type = "Steel",
                    DesignData = new Dictionary<string, object>
                    {
                        { "fy", 50000.0 },
                        { "fu", 65000.0 },
                        { "elasticModulus", 29000000.0 },
                        { "poissonsRatio", 0.3 },
                        { "weightDensity", 490.0 }
                    }
                });

                materials.Add(new Material
                {
                    Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL),
                    Name = "4000 psi Concrete",
                    Type = "Concrete",
                    DesignData = new Dictionary<string, object>
                    {
                        { "fc", 4000.0 },
                        { "elasticModulus", 3600000.0 },
                        { "poissonsRatio", 0.2 },
                        { "weightDensity", 150.0 }
                    }
                });
            }

            return materials;
        }

        private void ImportSteelMaterials(List<Material> materials)
        {
            // Get steel materials from RAM
            ISteelMaterials steelMaterials = _model.GetSteelMaterials();

            for (int i = 0; i < steelMaterials.GetCount(); i++)
            {
                ISteelMaterial steelMaterial = steelMaterials.GetAt(i);

                // Create design data dictionary
                var designData = new Dictionary<string, object>
                {
                    { "fy", steelMaterial.dFy },
                    { "fu", steelMaterial.dFu },
                    { "elasticModulus", steelMaterial.dE },
                    { "poissonsRatio", steelMaterial.dPoisson },
                    { "weightDensity", steelMaterial.dUnitWt * 1728.0 } // Convert lb/in³ to pcf
                };

                // Create material
                var material = new Material
                {
                    Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL),
                    Name = steelMaterial.strLabel,
                    Type = "Steel",
                    DesignData = designData
                };

                materials.Add(material);
            }
        }
        }

        private void ImportConcreteMaterials(List<Material> materials)
        {
            // Get concrete materials from RAM
            IConcreteMaterials concreteMaterials = _model.GetConcreteMaterials();

            for (int i = 0; i < concreteMaterials.GetCount(); i++)
            {
                IConcreteMaterial concreteMaterial = concreteMaterials.GetAt(i);

                // Create design data dictionary
                var designData = new Dictionary<string, object>
                {
                    { "fc", concreteMaterial.dFcPrime },
                    { "elasticModulus", concreteMaterial.dE },
                    { "poissonsRatio", concreteMaterial.dPoisson },
                    { "weightDensity", concreteMaterial.dUnitWt * 1728.0 } // Convert lb/in³ to pcf
                };

                // Create material
                var material = new Material
                {
                    Id = IdGenerator.Generate(IdGenerator.Properties.MATERIAL),
                    Name = concreteMaterial.strLabel,
                    Type = "Concrete",
                    DesignData = designData
                };

                materials.Add(material);
            }