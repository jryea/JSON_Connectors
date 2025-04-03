// LoadExporter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Models.Loads;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Export
{
    public class RAMToLoads : IRAMExporter
    {
        private IModel _model;

        public RAMToLoads(IModel model)
        {
            _model = model;
        }

        public void Export(BaseModel model)
        {
            // Export load combinations
            ExportLoadCombinations(model);

            // Export load cases
            ExportLoadCases(model);
        }

        private void ExportLoadCombinations(BaseModel model)
        {
            // Get load combinations from RAM
            ILoadCombinations loadCombos = _model.GetLoadCombinations();

            // Map load definitions by ID
            var loadDefById = model.Loads.LoadDefinitions.ToDictionary(ld => ld.Id, ld => ld);

            // Process each load combination
            foreach (var loadCombo in model.Loads.LoadCombinations)
            {
                try
                {
                    // Generate a name for the combination
                    string comboName = $"Combo{loadCombo.Id.Split('-').Last()}";

                    // Create new load combination
                    ILoadCombination combination = loadCombos.Add(comboName);

                    // Add load cases to combination
                    if (loadCombo.LoadDefinitionIds != null)
                    {
                        foreach (var loadDefId in loadCombo.LoadDefinitionIds)
                        {
                            // Find load definition
                            if (loadDefById.TryGetValue(loadDefId, out LoadDefinition loadDef))
                            {
                                // Add load case with default factor of 1.0
                                combination.AddLoadCase(loadDef.Name, 1.0);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting load combination {loadCombo.Id}: {ex.Message}");
                }
            }
        }

        private void ExportLoadCases(BaseModel model)
        {
            // Get load cases from RAM
            ILoadCases loadCases = _model.GetLoadCases();

            // Process each load definition
            foreach (var loadDef in model.Loads.LoadDefinitions)
            {
                try
                {
                    // Skip if the load case already exists
                    bool exists = false;
                    for (int i = 0; i < loadCases.GetCount(); i++)
                    {
                        ILoadCase existingCase = loadCases.GetAt(i);
                        if (existingCase.strLabel == loadDef.Name)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        // Determine load type
                        ELoadCaseType loadType = DetermineLoadType(loadDef.Type);

                        // Create new load case
                        ILoadCase loadCase = loadCases.Add(loadDef.Name, loadType);

                        // Set self-weight multiplier if appropriate
                        if (loadType == ELoadCaseType.DeadLCa || loadType == ELoadCaseType.SuperDeadLCa)
                        {
                            loadCase.dSelfWeightMultiplier = loadDef.SelfWeight;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting load case {loadDef.Name}: {ex.Message}");
                }
            }
        }

        private ELoadCaseType DetermineLoadType(string loadType)
        {
            if (string.IsNullOrEmpty(loadType))
                return ELoadCaseType.DeadLCa;

            switch (loadType.ToLower())
            {
                case "dead":
                case "permanent":
                    return ELoadCaseType.DeadLCa;

                case "superimposed dead":
                case "sdl":
                    return ELoadCaseType.SuperDeadLCa;

                case "live":
                case "imposed":
                    return ELoadCaseType.LiveReducibleLCa;

                case "live non-reducible":
                case "nonreducible live":
                    return ELoadCaseType.LiveNonReducibleLCa;

                case "roof live":
                    return ELoadCaseType.RoofLiveLCa;

                case "snow":
                    return ELoadCaseType.SnowLCa;

                case "wind":
                    return ELoadCaseType.WindLCa;

                case "seismic":
                case "earthquake":
                    return ELoadCaseType.EarthquakeLCa;

                default:
                    return ELoadCaseType.OtherLCa;
            }
        }
    }
}