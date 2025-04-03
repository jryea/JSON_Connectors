// LoadCombinationImporter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models.Loads;
using Core.Utilities;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Import
{
    public class LoadCombinationImporter : IRAMImporter<List<LoadCombination>>
    {
        private IModel _model;
        private Dictionary<int, string> _loadCaseIdMap;

        public LoadCombinationImporter(IModel model, Dictionary<int, string> loadCaseIdMap)
        {
            _model = model;
            _loadCaseIdMap = loadCaseIdMap;
        }

        public List<LoadCombination> Import()
        {
            var loadCombinations = new List<LoadCombination>();

            try
            {
                // Import load combinations from RAM
                ILoadCombinations loadCombos = _model.GetLoadCombinations();

                for (int i = 0; i < loadCombos.GetCount(); i++)
                {
                    ILoadCombination loadCombo = loadCombos.GetAt(i);

                    try
                    {
                        // Create load combination
                        var loadCombination = new LoadCombination
                        {
                            Id = IdGenerator.Generate(IdGenerator.Loads.LOAD_COMBINATION),
                            LoadDefinitionIds = new List<string>()
                        };

                        // Get load cases in this combination
                        ILoadCombinationCases comboCases = loadCombo.GetLoadCombinationCases();

                        for (int j = 0; j < comboCases.GetCount(); j++)
                        {
                            ILoadCombinationCase comboCase = comboCases.GetAt(j);

                            // Add load definition ID if mapped
                            if (_loadCaseIdMap.ContainsKey(comboCase.lLoadCaseId))
                            {
                                string loadDefId = _loadCaseIdMap[comboCase.lLoadCaseId];
                                if (!loadCombination.LoadDefinitionIds.Contains(loadDefId))
                                {
                                    loadCombination.LoadDefinitionIds.Add(loadDefId);
                                }
                            }
                        }

                        // Only add combination if it has at least one load case
                        if (loadCombination.LoadDefinitionIds.Count > 0)
                        {
                            loadCombinations.Add(loadCombination);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error importing load combination {loadCombo.strLabel}: {ex.Message}");
                    }
                }

                // If no load combinations were found, create defaults
                if (loadCombinations.Count == 0)
                {
                    CreateDefaultLoadCombinations(loadCombinations);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing load combinations: {ex.Message}");
                CreateDefaultLoadCombinations(loadCombinations);
            }

            return loadCombinations;
        }

        private void CreateDefaultLoadCombinations(List<LoadCombination> loadCombinations)
        {
            // Create a default load combination (would need all load definition IDs)
            var loadCombination = new LoadCombination
            {
                Id = IdGenerator.Generate(IdGenerator.Loads.LOAD_COMBINATION),
                LoadDefinitionIds = _loadCaseIdMap.Values.ToList() // Add all load definitions
            };

            loadCombinations.Add(loadCombination);
        }
    }
}