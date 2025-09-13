using Alexandria.Parser.Domain.Enums;
using Alexandria.Parser.Domain.Errors;
using Alexandria.Parser.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Alexandria.Parser.Infrastructure.Parsers;

/// <summary>
/// Factory for creating the appropriate EPUB parser based on version
/// </summary>
public sealed class EpubParserFactory : IEpubParserFactory
{
    private readonly ILogger<EpubParserFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly EpubVersionDetector _versionDetector;

    public EpubParserFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<EpubParserFactory>();
        _versionDetector = new EpubVersionDetector(loggerFactory.CreateLogger<EpubVersionDetector>());
    }

    public async Task<OneOf<IEpubParser, ParsingError>> CreateParserAsync(Stream epubStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(epubStream);

        try
        {
            // Detect version
            var version = await _versionDetector.DetectVersionAsync(epubStream, cancellationToken);

            _logger.LogInformation("Creating parser for EPUB version: {Version}", version);

            // Reset stream position after version detection
            if (epubStream.CanSeek)
            {
                epubStream.Position = 0;
            }

            return OneOf<IEpubParser, ParsingError>.FromT0(CreateParser(version));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create parser");
            return new ParsingFailedError("Failed to detect EPUB version", ex);
        }
    }

    public IEpubParser CreateParser(EpubVersion version)
    {
        return version switch
        {
            EpubVersion.Epub2 => new Epub2Parser(_loggerFactory.CreateLogger<Epub2Parser>()),
            EpubVersion.Epub30 or EpubVersion.Epub31 or EpubVersion.Epub32 or EpubVersion.Epub33
                => new Epub3Parser(_loggerFactory.CreateLogger<Epub3Parser>()),
            _ => CreateDefaultParser()
        };
    }

    private IEpubParser CreateDefaultParser()
    {
        _logger.LogWarning("Unknown EPUB version, defaulting to EPUB 2 parser");
        return new Epub2Parser(_loggerFactory.CreateLogger<Epub2Parser>());
    }
}

public interface IEpubParserFactory
{
    Task<OneOf<IEpubParser, ParsingError>> CreateParserAsync(Stream epubStream, CancellationToken cancellationToken = default);
    IEpubParser CreateParser(EpubVersion version);
}