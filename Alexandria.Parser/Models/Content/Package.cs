using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Alexandria.Parser.Models.Content
{
    [XmlRoot("package", Namespace = "http://www.idpf.org/2007/opf", IsNullable = false)]
    public class Package
    {
        [XmlElement("metadata")]
        public Metadata Metadata { get; set; }

        [XmlArray("manifest")]
        [XmlArrayItem("item", typeof(ManifestItem))]
        public ManifestItem[] ManifestItems { get; set; }

        [XmlArray("spine")]
        [XmlArrayItem("itemref", typeof(SpineItemRef))]
        public SpineItemRef[] SpineItemRefs { get; set; }

        [XmlArray("guide")]
        [XmlArrayItem("reference", typeof(GuideReference))]
        public GuideReference[] GuideReferences { get; set; }
    }
}
