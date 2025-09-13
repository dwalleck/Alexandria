using Alexandria.Parser.Application.UseCases.LoadBook;
using Alexandria.Parser.Domain.Interfaces;
using Alexandria.Parser.Infrastructure.Parsers;
using Alexandria.Parser.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Alexandria.Parser;

/// <summary>
/// Extension methods for registering Alexandria.Parser services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Alexandria EPUB parser services to the service collection
    /// </summary>
    public static IServiceCollection AddAlexandriaParser(this IServiceCollection services)
    {
        // Parser services
        services.AddSingleton<IEpubParserFactory, EpubParserFactory>();
        services.AddScoped<IEpubParser, AdaptiveEpubParser>();
        services.AddScoped<EpubVersionDetector>();

        // Repository services
        services.AddScoped<IBookRepository, BookRepository>();

        // Application services
        services.AddScoped<ILoadBookHandler, LoadBookHandler>();

        return services;
    }
}