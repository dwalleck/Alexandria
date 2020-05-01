using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Alexandria.Parser.Models.Content
{
    public class Metadata
    {
        [XmlElement("identifier", Namespace = "http://purl.org/dc/elements/1.1/")]
        public string[] Identifiers { get; set; }
        
        [XmlElement("title", Namespace = "http://purl.org/dc/elements/1.1/")]
        public string[] Titles { get; set; }

        [XmlElement("language", Namespace = "http://purl.org/dc/elements/1.1/")]
        public string[] Languages { get; set; }

        [XmlElement("creator", Namespace = "http://purl.org/dc/elements/1.1/")]
        public string Author { get; set; }

        [XmlElement("meta")]
        public MetaItem[] MetaItems { get; set; }
    }
}
