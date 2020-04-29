using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Alexandria.Parser.Models
{
    [XmlRoot("container", Namespace = "urn:oasis:names:tc:opendocument:xmlns:container", IsNullable = false)]
    public class Container
    {
        [XmlArray("rootfiles")]
        [XmlArrayItem("rootfile", typeof(Rootfile))]
        public Rootfile[] Rootfiles { get; set; }
    }
}
