using System.Xml.Serialization;

namespace Alexandria.Infrastructure.FileSystem.Models.Content
{
    public class MetaItem
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("content")]
        public string Content { get; set; }
    }
}