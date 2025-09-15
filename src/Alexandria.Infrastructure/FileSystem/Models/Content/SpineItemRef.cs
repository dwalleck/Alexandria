using System.Xml.Serialization;

namespace Alexandria.Infrastructure.FileSystem.Models.Content
{
    public class SpineItemRef
    {
        [XmlAttribute("idref")]
        public string IdRef { get; set; }
    }
}
