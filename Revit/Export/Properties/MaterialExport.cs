using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using Core.Models.Properties;

namespace Revit.Export.Properties
{
    public class MaterialExport
    {
        private readonly DB.Document _doc;

        public MaterialExport(DB.Document doc)
        {
            _doc = doc;
        }

        public int Export(List<Material> materials)
        {
            int count = 0;

            // Get all materials from Revit
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            IList<DB.Material> revitMaterials = collector.OfClass(typeof(DB.Material))
                .Cast<DB.Material>()
                .ToList();

            foreach (var revitMaterial in revitMaterials)
            {
                try
                {
                    // Skip invalid materials
                    if (revitMaterial.Name == null || revitMaterial.Name.Trim().Length == 0)
                        continue;

                    // Determine material type
                    string materialType = "Generic";
                    if (revitMaterial.Name.Contains("Concrete") || revitMaterial.MaterialClass.Contains("Concrete"))
                    {
                        materialType = "Concrete";
                    }
                    else if (revitMaterial.Name.Contains("Steel") || revitMaterial.MaterialClass.Contains("Metal"))
                    {
                        materialType = "Steel";
                    }
                    else if (revitMaterial.Name.Contains("Wood") || revitMaterial.MaterialClass.Contains("Wood"))
                    {
                        materialType = "Wood";
                    }
                    else if (revitMaterial.Name.Contains("Masonry") || revitMaterial.MaterialClass.Contains("Masonry"))
                    {
                        materialType = "Masonry";
                    }

                    // Create material
                    Material material = new Material(revitMaterial.Name, materialType);

                    materials.Add(material);
                    count++;
                }
                catch (Exception)
                {
                    // Skip this material and continue with the next one
                }
            }

            return count;
        }
    }
}