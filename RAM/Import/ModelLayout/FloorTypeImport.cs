// FloorTypeImporter.cs
using System;
using System.Collections.Generic;
using Core.Models.ModelLayout;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.ModelLayout
{
    // Imports floor types to RAM from the Core model
    public class FloorTypeImport
    {
        private IModel _model;

        public FloorTypeImport(IModel model)
        {
            _model = model;
        }

        // Imports floor types to RAM
        public int Import(IEnumerable<FloorType> floorTypes)
        {
            try
            {
                int count = 0;
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();

                foreach (var floorType in floorTypes)
                {
                    if (!string.IsNullOrEmpty(floorType.Name))
                    {
                        ramFloorTypes.Add(floorType.Name);
                        count++;
                    }
                }

                return count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing floor types: {ex.Message}");
                throw;
            }
        }
    }
}