using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.Errors;
using Alexandria.Parser.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using OneOf;

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

    public async Task<OneOf<Book, ParsingError>> ParseAsync(Stream epubStream, CancellationToken cancellationToken = default)
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
            var parserResult = await _parserFactory.CreateParserAsync(memoryStream, cancellationToken);

            if (parserResult.IsT1) // ParsingError
            {
                return parserResult.AsT1;
            }

            var parser = parserResult.AsT0;

            // Reset stream for actual parsing
            memoryStream.Position = 0;

            // Parse with the version-specific parser
            var result = await parser.ParseAsync(memoryStream, cancellationToken);

            if (result.IsT0) // Book
            {
                _logger.LogInformation("Successfully parsed EPUB with adaptive parser");
            }

            return result;
        }
        finally
        {
            await memoryStream.DisposeAsync();
        }
    }

    public async Task<OneOf<Success, ValidationError>> ValidateAsync(Stream epubStream, CancellationToken cancellationToken = default)
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
            var parserResult = await _parserFactory.CreateParserAsync(memoryStream, cancellationToken);

            if (parserResult.IsT1) // ParsingError
            {
                return new ValidationError(new[] { parserResult.AsT1.Message });
            }

            var parser = parserResult.AsT0;

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