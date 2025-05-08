using Core.Utilities;
using System.Text.Json.Serialization;

namespace Core.Models.Loads
{
    public class LoadDefinition
    {
        public string Id { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LoadType Type { get; set; }

        public string Name { get; set; }
        public double SelfWeight { get; set; }

        public LoadDefinition()
        {
            Id = IdGenerator.Generate(IdGenerator.Loads.LOAD_DEFINITION);
        }

        public LoadDefinition(string name, LoadType type, double selfWeight = 0) : this()
        {
            Name = name;
            Type = type;
            SelfWeight = selfWeight;
        }
    }
    public enum LoadType
    {
        Dead,
        Live,
        Snow,
        Wind,
        Seismic,
        Thermal,
        Other
    }
}