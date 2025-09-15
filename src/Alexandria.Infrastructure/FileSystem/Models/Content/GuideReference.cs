using System.Xml.Serialization;

namespace Alexandria.Infrastructure.FileSystem.Models.Content
{
    public class GuideReference
    {
        [XmlAttribute("href")]
        public string Href { get; set; }

        [XmlAttribute("title")]
        public string Title { get; set; }

        // This property should probably be an enum. Need to figure out
        // what the valid values are.
        [XmlAttribute("type")]
        public string Type { get; set; }
    }
}
