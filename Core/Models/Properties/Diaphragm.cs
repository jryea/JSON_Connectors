using Core.Utilities;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Core.Models.Properties
{
    public class Diaphragm : IIdentifiable
    {
        public string Id { get; set; }

        public string Name { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DiaphragmType Type { get; set; } = DiaphragmType.Rigid;  

        public Diaphragm()
        {
            Id = IdGenerator.Generate(IdGenerator.Properties.DIAPHRAGM);
        }

        public Diaphragm(string name, DiaphragmType type) : this()
        {
            Name = name;
            Type = type;
        }
    }
}