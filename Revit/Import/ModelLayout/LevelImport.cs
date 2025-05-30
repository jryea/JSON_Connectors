using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;
using CL = Core.Models.ModelLayout;
using Revit.Utilities;

namespace Revit.Import.ModelLayout
{
    // Imports level elements from JSON into Revit
    public class LevelImport
    {
        private readonly DB.Document _doc;

        public LevelImport(DB.Document doc)
        {
            _doc = doc;
        }

        // Helper method to format level names according to requirements
        private string FormatLevelName(string jsonLevelName)
        {
            if (string.IsNullOrEmpty(jsonLevelName))
                return "Level";

            // If the name is only a number, add "Level " prefix
            if (int.TryParse(jsonLevelName, out _))
                return $"Level {jsonLevelName}";

            // If the name contains "story", replace "story" with "level"
            if (jsonLevelName.ToLower().Contains("story"))
                return jsonLevelName.ToLower().Replace("story", "Level");

            // Otherwise, use the name as is
            return jsonLevelName;
        }

        private string GetUniqueLevelName(string baseName, DB.FilteredElementCollector existingLevels)
        {
            var existingNames = existingLevels.Cast<DB.Level>().Select(l => l.Name).ToHashSet();

            string testName = baseName;
            int copyCount = 1;

            while (existingNames.Contains(testName))
            {
                testName = copyCount == 1 ? $"{baseName} Copy" : $"{baseName} Copy {copyCount}";
                copyCount++;
            }

            return testName;
        }

        // Find the "S - Standard (document)" view template
        private DB.ViewFamilyType FindStandardViewTemplate()
        {
            try
            {
                DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
                var viewTemplates = collector.OfClass(typeof(DB.View))
                    .Cast<DB.View>()
                    .Where(v => v.IsTemplate && v.ViewType == DB.ViewType.EngineeringPlan)
                    .ToList();

                // Look for the specific template name
                var standardTemplate = viewTemplates.FirstOrDefault(vt =>
                    vt.Name.Equals("S - Standard (document)", StringComparison.OrdinalIgnoreCase));

                if (standardTemplate != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Found 'S - Standard (document)' view template: {standardTemplate.Name}");
                    return null; // We'll use the template differently
                }

                // If not found, look for any template with "Standard" or "S -" in the name
                standardTemplate = viewTemplates.FirstOrDefault(vt =>
                    vt.Name.Contains("Standard") || vt.Name.StartsWith("S -"));

                if (standardTemplate != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Found alternative standard template: {standardTemplate.Name}");
                    return null; // We'll use the template differently
                }

                System.Diagnostics.Debug.WriteLine("No suitable view template found");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding view template: {ex.Message}");
                return null;
            }
        }

        // Get the engineering plan view family type
        private DB.ViewFamilyType GetEngineeringPlanViewType()
        {
            try
            {
                DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
                var viewFamilyTypes = collector.OfClass(typeof(DB.ViewFamilyType))
                    .Cast<DB.ViewFamilyType>()
                    .Where(vft => vft.ViewFamily == DB.ViewFamily.StructuralPlan)
                    .ToList();

                // Return the first engineering plan view type found
                var engineeringPlanType = viewFamilyTypes.FirstOrDefault();

                if (engineeringPlanType != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Found engineering plan view type: {engineeringPlanType.Name}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No engineering plan view type found");
                }

                return engineeringPlanType;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting engineering plan view type: {ex.Message}");
                return null;
            }
        }

        // Find the view template by name
        private DB.View FindViewTemplate(string templateName)
        {
            try
            {
                DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
                var viewTemplate = collector.OfClass(typeof(DB.View))
                    .Cast<DB.View>()
                    .FirstOrDefault(v => v.IsTemplate &&
                        v.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase));

                if (viewTemplate != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Found view template: {viewTemplate.Name}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"View template '{templateName}' not found");
                }

                return viewTemplate;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding view template '{templateName}': {ex.Message}");
                return null;
            }
        }

        // Create an engineering plan view for a level
        private void CreateEngineeringPlanView(DB.Level level)
        {
            try
            {
                // Get the engineering plan view type
                DB.ViewFamilyType viewType = GetEngineeringPlanViewType();
                if (viewType == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot create engineering plan view for level {level.Name} - no view type found");
                    return;
                }

                // Create the view plan
                DB.ViewPlan engineeringPlan = DB.ViewPlan.Create(_doc, viewType.Id, level.Id);

                if (engineeringPlan == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to create engineering plan view for level {level.Name}");
                    return;
                }

                // Set the view name
                string viewName = $"{level.Name} - Structural Plan";

                // Make sure the name is unique
                var existingViewNames = new DB.FilteredElementCollector(_doc)
                    .OfClass(typeof(DB.ViewPlan))
                    .Cast<DB.ViewPlan>()
                    .Select(v => v.Name)
                    .ToHashSet();

                string uniqueViewName = viewName;
                int counter = 1;
                while (existingViewNames.Contains(uniqueViewName))
                {
                    uniqueViewName = $"{viewName} ({counter})";
                    counter++;
                }

                engineeringPlan.Name = uniqueViewName;

                // Try to apply the "S - Standard (document)" view template
                DB.View viewTemplate = FindViewTemplate("S - Standard (document)");
                if (viewTemplate != null)
                {
                    try
                    {
                        engineeringPlan.ViewTemplateId = viewTemplate.Id;
                        System.Diagnostics.Debug.WriteLine($"Applied 'S - Standard (document)' template to view {uniqueViewName}");
                    }
                    catch (Exception templateEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error applying view template: {templateEx.Message}");
                    }
                }
                else
                {
                    // Try alternative template names
                    string[] alternativeTemplates = {
                        "S - Standard",
                        "Standard",
                        "Structural Plan",
                        "Engineering Plan"
                    };

                    foreach (string altTemplate in alternativeTemplates)
                    {
                        viewTemplate = FindViewTemplate(altTemplate);
                        if (viewTemplate != null)
                        {
                            try
                            {
                                engineeringPlan.ViewTemplateId = viewTemplate.Id;
                                System.Diagnostics.Debug.WriteLine($"Applied alternative template '{altTemplate}' to view {uniqueViewName}");
                                break;
                            }
                            catch (Exception templateEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error applying alternative template '{altTemplate}': {templateEx.Message}");
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Successfully created engineering plan view: {uniqueViewName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating engineering plan view for level {level.Name}: {ex.Message}");
            }
        }

        // Imports levels from the JSON model into Revit
        public int Import(List<CL.Level> levels, Dictionary<string, DB.ElementId> levelMapping)
        {
            int count = 0;

            // Get all existing Revit levels
            DB.FilteredElementCollector collector = new DB.FilteredElementCollector(_doc);
            collector.OfClass(typeof(DB.Level));

            for (int i = 0; i < levels.Count; i++)
            {
                var jsonLevel = levels[i];
                try
                {
                    // Format the level name according to requirements
                    string levelName = FormatLevelName(jsonLevel.Name);

                    // Get unique name to handle conflicts
                    string uniqueName = GetUniqueLevelName(levelName, collector);

                    // Convert elevation from inches to feet for Revit
                    double elevation = jsonLevel.Elevation / 12.0;

                    // Create a new level in Revit
                    DB.Level revitLevel = DB.Level.Create(_doc, elevation);
                    revitLevel.Name = uniqueName;

                    // Add the new level to the mapping
                    levelMapping[jsonLevel.Id] = revitLevel.Id;

                    // Create an engineering plan view for this level
                    CreateEngineeringPlanView(revitLevel);

                    count++;
                    System.Diagnostics.Debug.WriteLine($"Created level '{uniqueName}' at elevation {elevation:F2}' with engineering plan view");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating level {jsonLevel.Name}: {ex.Message}");
                    // Skip this level and continue with the next one
                }
            }
            return count;
        }
    }
}