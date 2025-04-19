// FloorTypeImport.cs
using System;
using System.Collections.Generic;
using Core.Models.ModelLayout;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.Import.ModelLayout
{
    public class FloorTypeImport
    {
        private IModel _model;

        public FloorTypeImport(IModel model)
        {
            _model = model;
        }

        public int Import(IEnumerable<FloorType> floorTypes, IEnumerable<Level> levels)
        {
            try
            {
                int count = 0;
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();

                // Use the utility to filter valid floor types
                var validFloorTypes = ModelLayoutFilter.GetValidFloorTypes(floorTypes, levels);

                foreach (var floorType in validFloorTypes)
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
