using System.Xml.Serialization;

namespace Alexandria.Parser.Infrastructure.Parsers.Models.Epub3;

// Container.xml is the same for EPUB 2 and EPUB 3
[XmlRoot("container", Namespace = "urn:oasis:names:tc:opendocument:xmlns:container", IsNullable = false)]
public sealed class ContainerXml
{
    [XmlArray("rootfiles")]
    [XmlArrayItem("rootfile", typeof(RootfileXml))]
    public RootfileXml[]? Rootfiles { get; set; }
}

public sealed class RootfileXml
{
    [XmlAttribute("full-path")]
    public string? FullPath { get; set; }

    [XmlAttribute("media-type")]
    public string? MediaType { get; set; }
}