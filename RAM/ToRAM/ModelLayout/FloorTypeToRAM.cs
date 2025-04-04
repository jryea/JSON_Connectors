// FloorTypeImporter.cs
using System;
using System.Collections.Generic;
using Core.Models.ModelLayout;
using Core.Utilities;
using RAM.Utilities;
using RAMDATAACCESSLib;

namespace RAM.ToRAM.ModelLayout 
{
    public class FloorTypeToRAM : IModelToRAM<List<FloorType>>
    {
        private IModel _model;

        public FloorTypeToRAM(IModel model)
        {
            _model = model;
        }

        public List<FloorType> Import()
        {
            var floorTypes = new List<FloorType>();

            try
            {
                // Get floor types from RAM
                IFloorTypes ramFloorTypes = _model.GetFloorTypes();

                for (int i = 0; i < ramFloorTypes.GetCount(); i++)
                {
                    IFloorType ramFloorType = ramFloorTypes.GetAt(i);

                    // Create a new floor type
                    var floorType = new FloorType
                    {
                        Id = IdGenerator.Generate(IdGenerator.Layout.FLOOR_TYPE),
                        Name = ramFloorType.strLabel,
                        Description = $"Imported from RAM: {ramFloorType.strLabel}"
                    };

                    floorTypes.Add(floorType);
                }

                // If no floor types were found, create a default one
                if (floorTypes.Count == 0)
                {
                    floorTypes.Add(new FloorType
                    {
                        Id = IdGenerator.Generate(IdGenerator.Layout.FLOOR_TYPE),
                        Name = "Default",
                        Description = "Default floor type"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing floor types: {ex.Message}");

                // Create a default floor type
                floorTypes.Add(new FloorType
                {
                    Id = IdGenerator.Generate(IdGenerator.Layout.FLOOR_TYPE),
                    Name = "Default",
                    Description = "Default floor type"
                });
            }

            return floorTypes;
        }
    }
}