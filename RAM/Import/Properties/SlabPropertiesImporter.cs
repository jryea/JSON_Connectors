// SlabPropertiesImporter.cs
using System;
using System.Collections.Generic;
using Core.Models.Properties;
using Core.Utilities;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Import
{
    public class SlabPropertiesImporter : IRAMImporter<List<FloorProperties>>
    {
        private IModel _model;
        private Dictionary<int, string> _materialIdMap;

        public SlabPropertiesImporter(IModel model, Dictionary<int, string> materialIdMap)
        {
            _model = model;
            _materialIdMap = materialIdMap;
        }

        public List<FloorProperties> Import()
        {
            var floorProperties = new List<FloorProperties>();

            try
            {
                // Get concrete slab properties from RAM
                IConcSlabProps concSlabProps = _model.GetConcreteSlabProps();

                for (int i = 0; i < concSlabProps.GetCount(); i++)
                {
                    IConcSlabProp concSlabProp = concSlabProps.GetAt(i);

                    // Get material ID
                    string materialId = null;
                    if (_materialIdMap.ContainsKey(concSlabProp.lConcMaterialId))
                    {
                        materialId = _materialIdMap[concSlabProp.lConcMaterialId];
                    }

                    // Create floor property
                    var floorProp = new FloorProperties
                    {
                        Id = IdGenerator.Generate(IdGenerator.Properties.FLOOR_PROPERTIES),
                        Name = concSlabProp.strLabel,
                        Type = "Slab",
                        Thickness = concSlabProp.dThickness,
                        MaterialId = materialId
                    };

                    // Add slab-specific properties
                    floorProp.SlabProperties["elasticModulus"] = concSlabProp.dElasticMod;
                    floorProp.SlabProperties["poissonsRatio"] = concSlabProp.dPoissonsRatio;
                    floorProp.SlabProperties["selfWeight"] = concSlabProp.dSelfWeight;
                    floorProp.SlabProperties["modelingType"] = "ShellThin";
                    floorProp.SlabProperties["isRibbed"] = false;
                    floorProp.SlabProperties["isWaffle"] = false;
                    floorProp.SlabProperties["isTwoWay"] = true;

                    floorProperties.Add(floorProp);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing slab properties: {ex.Message}");
            }

            return floorProperties;
        }
    }
}