using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Alexandria.Parser.Models.Content
{
    public class MetaItem
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("content")]
        public string Content { get; set; }
    }
}