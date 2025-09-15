using System.IO.Compression;
using System.Xml.Linq;
using Alexandria.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Alexandria.Infrastructure.Parsers;

/// <summary>
/// Detects the EPUB version from the package document
/// </summary>
public sealed class EpubVersionDetector
{
    private readonly ILogger<EpubVersionDetector> _logger;
    private const string ContainerPath = "META-INF/container.xml";

    public EpubVersionDetector(ILogger<EpubVersionDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EpubVersion> DetectVersionAsync(Stream epubStream, CancellationToken cancellationToken = default)
    {
        try
        {
            using var archive = new ZipArchive(epubStream, ZipArchiveMode.Read, leaveOpen: true);

            // Get container.xml to find the OPF file
            var containerEntry = archive.GetEntry(ContainerPath);
            if (containerEntry == null)
            {
                _logger.LogWarning("No container.xml found, cannot determine EPUB version");
                return EpubVersion.Unknown;
            }

            string? opfPath = null;
            using (var containerStream = containerEntry.Open())
            {
                var containerDoc = await XDocument.LoadAsync(containerStream, LoadOptions.None, cancellationToken);
                var ns = containerDoc.Root?.GetDefaultNamespace();
                opfPath = containerDoc.Descendants(ns + "rootfile")
                    .FirstOrDefault()
                    ?.Attribute("full-path")
                    ?.Value;
            }

            if (string.IsNullOrEmpty(opfPath))
            {
                _logger.LogWarning("No OPF path found in container.xml");
                return EpubVersion.Unknown;
            }

            // Read the OPF file to get version
            var opfEntry = archive.GetEntry(opfPath);
            if (opfEntry == null)
            {
                _logger.LogWarning("OPF file not found at path: {Path}", opfPath);
                return EpubVersion.Unknown;
            }

            using var opfStream = opfEntry.Open();
            var opfDoc = await XDocument.LoadAsync(opfStream, LoadOptions.None, cancellationToken);

            var versionAttr = opfDoc.Root?.Attribute("version")?.Value;

            var version = versionAttr switch
            {
                "2.0" => EpubVersion.Epub2,
                "3.0" => EpubVersion.Epub30,
                "3.1" => EpubVersion.Epub31,
                "3.2" => EpubVersion.Epub32,
                "3.3" => EpubVersion.Epub33,
                _ => EpubVersion.Unknown
            };

            _logger.LogInformation("Detected EPUB version: {Version} (raw: {RawVersion})", version, versionAttr);
            return version;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting EPUB version");
            return EpubVersion.Unknown;
        }
    }

    public static bool IsEpub3OrLater(EpubVersion version)
    {
        return version >= EpubVersion.Epub30;
    }

    public static bool IsEpub2(EpubVersion version)
    {
        return version == EpubVersion.Epub2;
    }
}