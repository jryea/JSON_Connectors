using Core.Models.Elements;
using System;

namespace Core.Models.ModelLayout
{
    public class Level
    {
        public string Name { get; set; }
        public Guid FloorTypeId { get; set; }
        public double Elevation { get; set; }

        public Level(string name, FloorType floorType, double elevationOrHeight)
        {
            Name = name;
            FloorTypeId = floorType.Id;
            Elevation = elevationOrHeight;
        }
    }
}