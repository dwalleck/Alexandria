namespace Alexandria.Parser.Domain.Enums;

/// <summary>
/// Represents the EPUB specification version
/// </summary>
public enum EpubVersion
{
    /// <summary>
    /// Unknown or unsupported version
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// EPUB 2.0.1 specification
    /// </summary>
    Epub2 = 2,

    /// <summary>
    /// EPUB 3.0 specification
    /// </summary>
    Epub30 = 30,

    /// <summary>
    /// EPUB 3.1 specification
    /// </summary>
    Epub31 = 31,

    /// <summary>
    /// EPUB 3.2 specification
    /// </summary>
    Epub32 = 32,

    /// <summary>
    /// EPUB 3.3 specification
    /// </summary>
    Epub33 = 33
}