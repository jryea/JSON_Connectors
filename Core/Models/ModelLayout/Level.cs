using Core.Models.Elements;

namespace Core.Models.ModelLayout
{
    public class Level
    {
        public string Name { get; set; }
        public string FloorLayoutId { get; set; }
        public double Elevation { get; set; }

        public Level(string name, string floorLayoutId, double elevationOrHeight)
        {
            Name = name;
            FloorLayoutId = floorLayoutId;
            Elevation = elevationOrHeight;
        }
    }
}