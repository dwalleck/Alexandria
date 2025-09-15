using Alexandria.Application.Features.LoadBook;
using Alexandria.Domain.Interfaces;
using Alexandria.Infrastructure.Parsers;
using Alexandria.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Alexandria.Infrastructure;

/// <summary>
/// Extension methods for registering Alexandria.Infrastructure services
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