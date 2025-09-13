using System.Xml.Serialization;

namespace Alexandria.Parser.Infrastructure.Parsers.Models.Epub3;

[XmlRoot("package", Namespace = "http://www.idpf.org/2007/opf", IsNullable = false)]
public sealed class PackageXml
{
    [XmlAttribute("version")]
    public string? Version { get; set; }

    [XmlAttribute("unique-identifier")]
    public string? UniqueIdentifier { get; set; }

    [XmlAttribute("dir")]
    public string? Dir { get; set; }

    [XmlAttribute("id")]
    public string? Id { get; set; }

    [XmlElement("metadata")]
    public MetadataXml? Metadata { get; set; }

    [XmlArray("manifest")]
    [XmlArrayItem("item", typeof(ManifestItemXml))]
    public ManifestItemXml[]? ManifestItems { get; set; }

    [XmlArray("spine")]
    [XmlArrayItem("itemref", typeof(SpineItemRefXml))]
    public SpineItemRefXml[]? SpineItemRefs { get; set; }
}

public sealed class MetadataXml
{
    // Dublin Core elements
    [XmlElement("identifier", Namespace = "http://purl.org/dc/elements/1.1/")]
    public IdentifierXml[]? Identifiers { get; set; }

    [XmlElement("title", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string[]? Titles { get; set; }

    [XmlElement("language", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string[]? Languages { get; set; }

    [XmlElement("creator", Namespace = "http://purl.org/dc/elements/1.1/")]
    public CreatorXml[]? Creators { get; set; }

    [XmlElement("contributor", Namespace = "http://purl.org/dc/elements/1.1/")]
    public ContributorXml[]? Contributors { get; set; }

    [XmlElement("publisher", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string? Publisher { get; set; }

    [XmlElement("date", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string? Date { get; set; }

    [XmlElement("description", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string? Description { get; set; }

    [XmlElement("rights", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string? Rights { get; set; }

    [XmlElement("subject", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string[]? Subjects { get; set; }

    [XmlElement("type", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string? Type { get; set; }

    [XmlElement("format", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string? Format { get; set; }

    [XmlElement("source", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string? Source { get; set; }

    [XmlElement("relation", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string? Relation { get; set; }

    [XmlElement("coverage", Namespace = "http://purl.org/dc/elements/1.1/")]
    public string? Coverage { get; set; }

    // EPUB 3 specific meta elements
    [XmlElement("meta")]
    public MetaXml[]? MetaItems { get; set; }

    [XmlElement("link")]
    public LinkXml[]? Links { get; set; }
}

public sealed class IdentifierXml
{
    [XmlAttribute("id")]
    public string? Id { get; set; }

    [XmlText]
    public string? Value { get; set; }
}

public sealed class CreatorXml
{
    [XmlAttribute("id")]
    public string? Id { get; set; }

    [XmlAttribute("dir")]
    public string? Dir { get; set; }

    [XmlText]
    public string? Name { get; set; }
}

public sealed class ContributorXml
{
    [XmlAttribute("id")]
    public string? Id { get; set; }

    [XmlAttribute("dir")]
    public string? Dir { get; set; }

    [XmlText]
    public string? Name { get; set; }
}

public sealed class MetaXml
{
    [XmlAttribute("property")]
    public string? Property { get; set; }

    [XmlAttribute("refines")]
    public string? Refines { get; set; }

    [XmlAttribute("id")]
    public string? Id { get; set; }

    [XmlAttribute("scheme")]
    public string? Scheme { get; set; }

    [XmlAttribute("dir")]
    public string? Dir { get; set; }

    [XmlText]
    public string? Content { get; set; }
}

public sealed class LinkXml
{
    [XmlAttribute("rel")]
    public string? Rel { get; set; }

    [XmlAttribute("href")]
    public string? Href { get; set; }

    [XmlAttribute("id")]
    public string? Id { get; set; }

    [XmlAttribute("media-type")]
    public string? MediaType { get; set; }

    [XmlAttribute("properties")]
    public string? Properties { get; set; }

    [XmlAttribute("refines")]
    public string? Refines { get; set; }
}

public sealed class ManifestItemXml
{
    [XmlAttribute("id")]
    public string? Id { get; set; }

    [XmlAttribute("href")]
    public string? Href { get; set; }

    [XmlAttribute("media-type")]
    public string? MediaType { get; set; }

    [XmlAttribute("fallback")]
    public string? Fallback { get; set; }

    [XmlAttribute("properties")]
    public string? Properties { get; set; }

    [XmlAttribute("media-overlay")]
    public string? MediaOverlay { get; set; }
}

public sealed class SpineItemRefXml
{
    [XmlAttribute("idref")]
    public string? IdRef { get; set; }

    [XmlAttribute("linear")]
    public string? Linear { get; set; }

    [XmlAttribute("id")]
    public string? Id { get; set; }

    [XmlAttribute("properties")]
    public string? Properties { get; set; }
}