using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Alexandria.Parser.Models.Content
{
    public class SpineItemRef
    {
        [XmlAttribute("idref")]
        public string IdRef { get; set; }
    }
}
