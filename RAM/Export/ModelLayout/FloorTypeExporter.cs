// FloorTypeExporter.cs
using System;
using System.Collections.Generic;
using Core.Models;
using Core.Models.ModelLayout;
using RAM.Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Export
{
    public class FloorTypeExporter : IRAMExporter
    {
        private IModel _model;

        public FloorTypeExporter(IModel model)
        {
            _model = model;
        }

        public void Export(BaseModel model)
        {
            IFloorTypes floorTypes = _model.GetFloorTypes();

            foreach (var floorType in model.ModelLayout.FloorTypes)
            {
                try
                {
                    floorTypes.Add(floorType.Name);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting floor type {floorType.Name}: {ex.Message}");
                }
            }
        }
    }
}