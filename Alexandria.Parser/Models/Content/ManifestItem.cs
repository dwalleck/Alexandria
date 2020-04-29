using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Alexandria.Parser.Models.Content
{
    public class ManifestItem
    {
        [XmlAttribute("href")]
        public string Href { get; set; }

        [XmlAttribute("id")]
        public string Id { get; set; }

        [XmlAttribute("media-type")]
        public string MediaType { get; set; }
    }
}
