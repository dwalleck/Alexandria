using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Alexandria.Parser.Models
{
    [Serializable()]
    public class Rootfile
    {
        [XmlAttribute("full-path")]
        public string FullPath { get; set; }
        
        [XmlAttribute("media-type")]
        public string MediaType { get; set; }
    }
}
