using Core.Models.Elements;
using Core.Models.Loads;
using Core.Models.Model;
using Core.Models.Properties;
using Core.Models.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    /// <summary>
    /// Container for all structural elements
    /// </summary>
    public class ElementContainer
    {
        public List<Floor> Floors { get; set; } = new List<Floor>();
        public List<Wall> Walls { get; set; } = new List<Wall>();
        public List<Beam> Beams { get; set; } = new List<Beam>();
        public List<Brace> Braces { get; set; } = new List<Brace>();
        public List<Column> Columns { get; set; } = new List<Column>();
        public List<IsolatedFooting> IsolatedFootings { get; set; } = new List<IsolatedFooting>();
        public List<Joint> Joints { get; set; } = new List<Joint>();
        public List<ContinuousFooting> ContinuousFootings { get; set; } = new List<ContinuousFooting>();
        public List<Pile> Piles { get; set; } = new List<Pile>();
        public List<Pier> Piers { get; set; } = new List<Pier>();
        public List<DrilledPier> DrilledPiers { get; set; } = new List<DrilledPier>();
    }


    /// <summary>
    /// Root model representing a complete building structure
    /// </summary>
    public class BaseModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Main containers
        public ElementContainer Elements { get; set; } = new ElementContainer();
        public LoadContainer Loads { get; set; } = new LoadContainer();
        public ProjectInfo ProjectInfo { get; set; } = new ProjectInfo();
        public Dictionary<string, object> AnalysisResults { get; set; } = new Dictionary<string, object>();
        public StructuralModel Model { get; set; } = new Model.StructuralModel();
        public Dictionary<string, object> VersionControl { get; set; } = new Dictionary<string, object>();
        public Units Units { get; set; } = new Units();
        //public ProjectInfo projectInfo { get; set; } = new Metadata.ProjectInfo();

    }
}
