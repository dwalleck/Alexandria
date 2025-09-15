using System.Xml.Serialization;

namespace Alexandria.Infrastructure.FileSystem.Models
{
    [Serializable()]
    public class Rootfile
    {
        [XmlAttribute("full-path")]
        public string FullPath { get; set; }
        
        // TODO: This should probably be an enum
        [XmlAttribute("media-type")]
        public string MediaType { get; set; }
    }
}
