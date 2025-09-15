using System.Xml.Serialization;

namespace Alexandria.Infrastructure.FileSystem.Models
{
    [XmlRoot("container", Namespace = "urn:oasis:names:tc:opendocument:xmlns:container", IsNullable = false)]
    public class Container
    {
        [XmlArray("rootfiles")]
        [XmlArrayItem("rootfile", typeof(Rootfile))]
        public Rootfile[] Rootfiles { get; set; }
    }
}
