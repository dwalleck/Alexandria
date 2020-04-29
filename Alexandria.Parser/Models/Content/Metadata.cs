using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Alexandria.Parser.Models.Content
{
    public class Metadata
    {
        [XmlElement("title", Namespace = "http://purl.org/dc/elements/1.1/")]
        public string Title { get; set; }

        [XmlElement("language", Namespace = "http://purl.org/dc/elements/1.1/")]
        public string Language { get; set; }

        [XmlElement("meta")]
        public MetaItem[] MetaItems { get; set; }
    }
}
