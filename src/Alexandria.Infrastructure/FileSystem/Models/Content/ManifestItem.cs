using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Alexandria.Infrastructure.FileSystem.Models.Content
{
    public class ManifestItem
    {
        [XmlAttribute("href")]
        public string Href { get; set; }

        [XmlAttribute("id")]
        public string Id { get; set; }

        [XmlAttribute("media-type")]
        public MediaType MediaType { get; set; }
    }
}
