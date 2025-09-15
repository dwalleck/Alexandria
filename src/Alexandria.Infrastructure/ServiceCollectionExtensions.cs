using Alexandria.Application.Features.LoadBook;
using Alexandria.Domain.Interfaces;
using Alexandria.Domain.Services;
using Alexandria.Infrastructure.Parsers;
using Alexandria.Infrastructure.Repositories;
using Alexandria.Infrastructure.Services;
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
        // Domain services
        services.AddSingleton<IContentAnalyzer, AngleSharpContentAnalyzer>();

        // Parser services
        services.AddSingleton<IEpubParserFactory, EpubParserFactory>();
        services.AddScoped<IEpubParser, AdaptiveEpubParser>();
        services.AddScoped<EpubVersionDetector>();

        // Loader services
        services.AddScoped<IEpubLoader, EpubLoader>();

        // Application services
        services.AddScoped<ILoadBookHandler, LoadBookHandler>();

        return services;
    }
}