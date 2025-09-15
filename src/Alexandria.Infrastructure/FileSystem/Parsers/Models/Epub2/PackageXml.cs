using System.Xml.Serialization;

namespace Alexandria.Infrastructure.Parsers.Models.Epub2;

[XmlRoot("package", Namespace = "http://www.idpf.org/2007/opf", IsNullable = false)]
public sealed class PackageXml
{
    [XmlElement("metadata")]
    public MetadataXml? Metadata { get; set; }

    [XmlArray("manifest")]
    [XmlArrayItem("item", typeof(ManifestItemXml))]
    public ManifestItemXml[]? ManifestItems { get; set; }

    [XmlArray("spine")]
    [XmlArrayItem("itemref", typeof(SpineItemRefXml))]
    public SpineItemRefXml[]? SpineItemRefs { get; set; }

    [XmlArray("guide")]
    [XmlArrayItem("reference", typeof(GuideReferenceXml))]
    public GuideReferenceXml[]? GuideReferences { get; set; }
}

public sealed class MetadataXml
{
    [XmlElement("identifier", Namespace = "http://purl.org/dc/elements/1.1/")]
    public IdentifierXml[]? Identifiers { get; set; }

    [XmlElement("title", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string[]? Titles { get; set; }

    [XmlElement("language", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string[]? Languages { get; set; }

    [XmlElement("creator", Namespace = "http://purl.org/dc/elements/1.1/")]
    public CreatorXml[]? Authors { get; set; }

    [XmlElement("publisher", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string? Publisher { get; set; }

    [XmlElement("date", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string? Date { get; set; }

    [XmlElement("description", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string? Description { get; set; }

    [XmlElement("rights", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string? Rights { get; set; }

    [XmlElement("subject", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string? Subject { get; set; }

    [XmlElement("meta")]
    public MetaItemXml[]? MetaItems { get; set; }
}

public sealed class IdentifierXml
{
    [XmlAttribute("scheme", Namespace = "http://www.idpf.org/2007/opf")]
    public string? Scheme { get; set; }

    [XmlText]
    public string? Value { get; set; }
}

public sealed class CreatorXml
{
    [XmlAttribute("role", Namespace = "http://www.idpf.org/2007/opf")]
    public string? Role { get; set; }

    [XmlAttribute("file-as", Namespace = "http://www.idpf.org/2007/opf")]
    public string? FileAs { get; set; }

    [XmlText]
    public string? Name { get; set; }
}

public sealed class ManifestItemXml
{
    [XmlAttribute("href")]
    public string? Href { get; set; }

    [XmlAttribute("id")]
    public string? Id { get; set; }

    [XmlAttribute("media-type")]
    public string? MediaType { get; set; }
}

public sealed class SpineItemRefXml
{
    [XmlAttribute("idref")]
    public string? IdRef { get; set; }

    [XmlAttribute("linear")]
    public string? Linear { get; set; }
}

public sealed class GuideReferenceXml
{
    [XmlAttribute("href")]
    public string? Href { get; set; }

    [XmlAttribute("title")]
    public string? Title { get; set; }

    [XmlAttribute("type")]
    public string? Type { get; set; }
}

public sealed class MetaItemXml
{
    [XmlAttribute("name")]
    public string? Name { get; set; }

    [XmlAttribute("content")]
    public string? Content { get; set; }
}