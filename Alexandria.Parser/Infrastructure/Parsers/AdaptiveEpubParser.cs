using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Alexandria.Parser.Infrastructure.Parsers;

/// <summary>
/// Adaptive EPUB parser that automatically detects and uses the correct version-specific parser
/// </summary>
public sealed class AdaptiveEpubParser : IEpubParser
{
    private readonly IEpubParserFactory _parserFactory;
    private readonly ILogger<AdaptiveEpubParser> _logger;

    public AdaptiveEpubParser(IEpubParserFactory parserFactory, ILogger<AdaptiveEpubParser> logger)
    {
        _parserFactory = parserFactory ?? throw new ArgumentNullException(nameof(parserFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Book> ParseAsync(Stream epubStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(epubStream);

        _logger.LogInformation("Starting adaptive EPUB parsing");

        // Create a memory stream to allow multiple reads
        var memoryStream = new MemoryStream();
        await epubStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        try
        {
            // Get the appropriate parser based on version
            var parser = await _parserFactory.CreateParserAsync(memoryStream, cancellationToken);

            // Reset stream for actual parsing
            memoryStream.Position = 0;

            // Parse with the version-specific parser
            var book = await parser.ParseAsync(memoryStream, cancellationToken);

            _logger.LogInformation("Successfully parsed EPUB with adaptive parser");

            return book;
        }
        finally
        {
            await memoryStream.DisposeAsync();
        }
    }

    public async Task<ValidationResult> ValidateAsync(Stream epubStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(epubStream);

        _logger.LogInformation("Starting adaptive EPUB validation");

        // Create a memory stream to allow multiple reads
        var memoryStream = new MemoryStream();
        await epubStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        try
        {
            // Get the appropriate parser based on version
            var parser = await _parserFactory.CreateParserAsync(memoryStream, cancellationToken);

            // Reset stream for actual validation
            memoryStream.Position = 0;

            // Validate with the version-specific parser
            return await parser.ValidateAsync(memoryStream, cancellationToken);
        }
        finally
        {
            await memoryStream.DisposeAsync();
        }
    }
}