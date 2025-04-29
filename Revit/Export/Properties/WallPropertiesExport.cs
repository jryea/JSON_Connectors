using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using Core.Models.Properties;
using System.Diagnostics;

namespace Revit.Export.Properties
{
    public class WallPropertiesExport
    {
        private readonly DB.Document _doc;

        public WallPropertiesExport(DB.Document doc)
        {
            _doc = doc;
        }

        // Find material by type in the provided materials list
        private string FindMaterialIdByType(string materialType, List<Material> materials)
        {
            if (materials == null || materials.Count == 0)
                return null;

            // Look for exact material type match first
            var material = materials.FirstOrDefault(m =>
                string.Equals(m.Type, materialType, StringComparison.OrdinalIgnoreCase));

            // If not found, try to find concrete material as fallback for walls
            if (material == null && materialType != "Concrete")
                material = materials.FirstOrDefault(m =>
                    string.Equals(m.Type, "Concrete", StringComparison.OrdinalIgnoreCase));

            // If still not found, try to match by name
            if (material == null)
            {
                material = materials.FirstOrDefault(m =>
                    m.Name != null && m.Name.IndexOf(materialType, StringComparison.OrdinalIgnoreCase) >= 0);

                // Last resort: find any concrete material
                if (material == null && materialType == "Concrete")
                {
                    material = materials.FirstOrDefault(m =>
                        m.Name != null && m.Name.IndexOf("Concrete", StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }

            // Return the ID of the found material
            return material?.Id;
        }

        public int Export(List<WallProperties> wallProperties, List<Material> materials)
        {
            int count = 0;
            HashSet<string> processedWallTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<DB.ElementId> wallTypeIds = new HashSet<DB.ElementId>();

            // First collect all wall instances from the model
            DB.FilteredElementCollector wallCollector = new DB.FilteredElementCollector(_doc);
            IList<DB.Wall> walls = wallCollector.OfCategory(DB.BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .Where(w => !(w is DB.DirectShape)) // Exclude DirectShape walls
                .Cast<DB.Wall>()
                .ToList();

            Debug.WriteLine($"Found {walls.Count} wall instances in the model");

            // Extract the wall type IDs from the wall instances
            foreach (var wall in walls)
            {
                try
                {
                    DB.ElementId typeId = wall.GetTypeId();
                    if (!wallTypeIds.Contains(typeId))
                    {
                        wallTypeIds.Add(typeId);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting wall type ID: {ex.Message}");
                }
            }

            Debug.WriteLine($"Found {wallTypeIds.Count} unique wall types used by walls in the model");

            // Check that we have materials to reference
            if (materials == null || materials.Count == 0)
            {
                Debug.WriteLine("Warning: No materials available for wall properties");
                return 0;
            }

            // Process each used wall type
            foreach (var typeId in wallTypeIds)
            {
                try
                {
                    // Get the wall type element
                    DB.WallType wallType = _doc.GetElement(typeId) as DB.WallType;
                    if (wallType == null)
                    {
                        Debug.WriteLine($"Warning: Could not get wall type for ID {typeId}");
                        continue;
                    }

                    // Skip if we've already processed a wall type with this name
                    if (processedWallTypeNames.Contains(wallType.Name))
                    {
                        Debug.WriteLine($"Skipping duplicate wall type name: {wallType.Name}");
                        continue;
                    }

                    processedWallTypeNames.Add(wallType.Name);
                    Debug.WriteLine($"Processing wall type: {wallType.Name}");

                    // Get thickness in inches
                    double thickness = wallType.Width * 12.0; // Convert feet to inches

                    // Determine material type (concrete, masonry, etc.)
                    string materialType = DetermineMaterialType(wallType);
                    string materialId = FindMaterialIdByType(materialType, materials);

                    if (string.IsNullOrEmpty(materialId))
                    {
                        Debug.WriteLine($"Warning: No matching material found for wall type '{wallType.Name}' - skipping");
                        continue;
                    }

                    // Create wall property
                    WallProperties wallProperty = new WallProperties(
                        wallType.Name,
                        materialId,
                        thickness
                    );

                    // Add function property
                    wallProperty.Properties["Function"] = wallType.Function.ToString();
                    wallProperty.Properties["ModelingType"] = "ShellThin";

                    wallProperties.Add(wallProperty);
                    count++;

                    Debug.WriteLine($"Exported wall type: {wallType.Name}, Material: {materialType}, MaterialID: {materialId}, Thickness: {thickness}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error exporting wall type: {ex.Message}");
                }
            }

            Debug.WriteLine($"Successfully exported {count} wall properties");
            return count;
        }

        // Determine material type based on wall type name and family
        private string DetermineMaterialType(DB.WallType wallType)
        {
            string typeName = wallType.Name.ToUpper();
            string familyName = wallType.FamilyName.ToUpper();

            if (typeName.Contains("CONCRETE") || typeName.Contains("CONC") ||
                familyName.Contains("CONCRETE") || familyName.Contains("CONC"))
                return "Concrete";

            if (typeName.Contains("MASONRY") || typeName.Contains("CMU") || typeName.Contains("BRICK") ||
                familyName.Contains("MASONRY") || familyName.Contains("CMU") || familyName.Contains("BRICK"))
                return "Masonry";

            if (typeName.Contains("STEEL") || typeName.Contains("METAL") ||
                familyName.Contains("STEEL") || familyName.Contains("METAL"))
                return "Steel";

            // Default to concrete for structural walls
            return "Concrete";
        }
    }
}