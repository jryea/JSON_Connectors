using Core.Models.Elements;
using Core.Models.Properties;
using Core.Models.ModelLayout;


namespace Core.Models.ModelLayout
{

    public class Grid
    {
        public string Name { get; set; }
        public GridPoint StartPoint { get; set; }
        public GridPoint EndPoint { get; set; }

        public Grid(string name, GridPoint startPoint, GridPoint endPoint)
        {
            Name = name;
            StartPoint = startPoint;
            EndPoint = endPoint;
        }
    }
}
