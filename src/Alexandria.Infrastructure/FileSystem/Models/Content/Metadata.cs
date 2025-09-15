using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Alexandria.Infrastructure.FileSystem.Models.Content
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
        public string[] Authors { get; set; }

        [XmlElement("meta")]
        public MetaItem[] MetaItems { get; set; }
    }
}
