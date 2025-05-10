using System;
using System.Collections.Generic;
using Core.Models.Properties;
using Core.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Utilities
{
    public class MaterialProvider
    {
        private string _concreteMaterialId;
        private string _steelMaterialId;
        private List<Material> _materials = new List<Material>();

        public MaterialProvider()
        {
            InitializeDefaultMaterials();
        }

        private void InitializeDefaultMaterials()
        {
            // Create a single concrete material
            var concreteMaterial = new Material("4000Psi", MaterialType.Concrete);

            // Create a single steel material
            var steelMaterial = new Material("A992Fy50", MaterialType.Steel);

            // Store the IDs
            _concreteMaterialId = concreteMaterial.Id;
            _steelMaterialId = steelMaterial.Id;

            // Add to the materials list
            _materials.Add(concreteMaterial);
            _materials.Add(steelMaterial);

            Console.WriteLine($"Created default concrete material with ID: {_concreteMaterialId}");
            Console.WriteLine($"Created default steel material with ID: {_steelMaterialId}");
        }

        // Get concrete material ID
        public string GetConcreteMaterialId() => _concreteMaterialId;

        // Get steel material ID
        public string GetSteelMaterialId() => _steelMaterialId;

        // Get all materials
        public List<Material> GetAllMaterials() => _materials;

        // Get material ID based on RAM material type
        public string GetMaterialIdByType(EMATERIALTYPES materialType)
        {
            return materialType == EMATERIALTYPES.EConcreteMat ?
                _concreteMaterialId : _steelMaterialId;
        }

        // Get RAM material type for a frame element
        public EMATERIALTYPES GetRAMMaterialType(string framePropId,
                                               IEnumerable<FrameProperties> frameProperties,
                                               bool isJoist = false)
        {
            // If explicitly marked as a joist, use joist material
            if (isJoist)
                return EMATERIALTYPES.ESteelJoistMat;

            // Find frame property to determine material type
            if (!string.IsNullOrEmpty(framePropId) && frameProperties != null)
            {
                foreach (var frameProp in frameProperties)
                {
                    if (frameProp.Id == framePropId)
                    {
                        // Check frame material type
                        return frameProp.Type == FrameProperties.FrameMaterialType.Concrete ?
                            EMATERIALTYPES.EConcreteMat :
                            EMATERIALTYPES.ESteelMat;
                    }
                }
            }

            // Default to steel if no matching property found
            return EMATERIALTYPES.ESteelMat;
        }

        // Get deck properties based on type and gage
        public void GetDeckProperties(string deckType, int deckGage, out double selfWeight)
        {
            if (deckType == "VULCRAFT 1.5VL")
            {
                if (deckGage == 22)
                    selfWeight = 1.6;
                else if (deckGage == 20)
                    selfWeight = 2.0;
                else if (deckGage == 19)
                    selfWeight = 2.3;
                else if (deckGage == 18)
                    selfWeight = 2.6;
                else if (deckGage == 16)
                    selfWeight = 3.3;
                else
                    selfWeight = 2.0; // Default
            }
            else if (deckType == "VULCRAFT 2VL")
            {
                if (deckGage == 22)
                    selfWeight = 1.6;
                else if (deckGage == 20)
                    selfWeight = 1.9;
                else if (deckGage == 19)
                    selfWeight = 2.2;
                else if (deckGage == 18)
                    selfWeight = 2.5;
                else if (deckGage == 16)
                    selfWeight = 3.2;
                else
                    selfWeight = 2.0; // Default
            }
            else if (deckType == "VULCRAFT 3VL")
            {
                if (deckGage == 22)
                    selfWeight = 1.7;
                else if (deckGage == 20)
                    selfWeight = 2.1;
                else if (deckGage == 19)
                    selfWeight = 2.4;
                else if (deckGage == 18)
                    selfWeight = 2.7;
                else if (deckGage == 16)
                    selfWeight = 3.5;
                else
                    selfWeight = 2.0; // Default
            }
            else
            {
                selfWeight = 2.0; // Default value for unknown deck types
            }
        }
    }
}