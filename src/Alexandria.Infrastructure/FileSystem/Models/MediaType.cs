using System.Xml.Serialization;

namespace Alexandria.Infrastructure.FileSystem.Models
{
    public enum MediaType
    {
        [XmlEnum("image/gif")]
        ImageGif,
        [XmlEnum("image/jpeg")]
        ImageJpeg,
        [XmlEnum("image/png")]
        ImagePng,
        [XmlEnum("image/svg+xml")]
        ImageSvgXml,
        [XmlEnum("audio/mpeg")]
        AudioMpeg,
        [XmlEnum("audio/mp4")]
        AudioMp4,
        [XmlEnum("text/css")]
        TextCss,
        [XmlEnum("font/tff")]
        FontTff,
        [XmlEnum("application/font-sfnt")]
        ApplicationFontSfnt,
        [XmlEnum("application/vnd.ms-opentype")]
        ApplicationVndMsOpentype,
        [XmlEnum("font/woff")]
        FontWoff,
        [XmlEnum("application/font-woff")]
        ApplicationFontWoff,
        [XmlEnum("font/woff2")]
        FontWoff2,
        [XmlEnum("application/xhtml+xml")]
        ApplicationXhtmlXml,
        [XmlEnum("application/javascript")]
        ApplicationJavaScript,
        [XmlEnum("text/javascript")]
        TextJavaScript,
        [XmlEnum("application/x-dtbncx+xml")]
        AplicationXdtbncxXml,
        [XmlEnum("application/smil+xml")]
        ApplicationSmilXml,
        [XmlEnum("application/pls+xml")]
        ApplicationPlsXml,
        [XmlEnum("application/x-font-truetype")]
        ApplicationXfontTrueType,
        [XmlEnum("text/x-markdown")]
        TextXmarkdown,
        [XmlEnum("application/oebps-page-map+xml")]
        ApplicationOebpsPageMapXml,
        [XmlEnum("text/plain")]
        TextPlain,
        [XmlEnum("application/x-font-ttf")]
        ApplicationXFontTtf

    }
}
