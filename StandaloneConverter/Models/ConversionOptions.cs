namespace StandaloneConverter.Models
{
    public class ConversionOptions
    {
        public string InputFilePath { get; set; }
        public string OutputFilePath { get; set; }
        public string IntermediateJsonPath { get; set; }
        public bool IsRamToEtabs { get; set; }
    }
}