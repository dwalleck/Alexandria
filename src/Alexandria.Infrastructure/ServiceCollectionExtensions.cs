using Alexandria.Application.Features.LoadBook;
using Alexandria.Application.Services;
using Alexandria.Domain.Interfaces;
using Alexandria.Domain.Services;
using Alexandria.Infrastructure.Caching;
using Alexandria.Infrastructure.Parsers;
using Alexandria.Infrastructure.Repositories;
using Alexandria.Infrastructure.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
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
    public static IServiceCollection AddAlexandriaParser(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // Domain services
        services.AddSingleton<IContentAnalyzer, AngleSharpContentAnalyzer>();

        // Parser services
        services.AddSingleton<IEpubParserFactory, EpubParserFactory>();
        services.AddScoped<IEpubParser, AdaptiveEpubParser>();
        services.AddScoped<EpubVersionDetector>();

        // Loader services
        services.AddScoped<IEpubLoader, EpubLoader>();

        // MediatR and validation
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(LoadBookHandler).Assembly));
        services.AddValidatorsFromAssembly(typeof(LoadBookValidator).Assembly);

        // Caching services
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 100; // Limit to 100 cached books
        });

        if (configuration != null)
        {
            services.Configure<BookCacheOptions>(configuration.GetSection("BookCache"));
        }

        services.AddSingleton<IBookCache, BookCache>();

        // Progress reporting (optional - can be overridden by caller)
        services.AddTransient<IProgress<LoadProgress>>(sp => new Progress<LoadProgress>());

        return services;
    }
}