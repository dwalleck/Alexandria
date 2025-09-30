using LiteDB;
using Microsoft.Extensions.Options;
using System;

namespace Alexandria.Infrastructure.Persistence.LiteDb;

/// <summary>
/// Configuration options for LiteDB database.
/// </summary>
public sealed class LiteDbOptions
{
    public const string SectionName = "LiteDb";

    /// <summary>
    /// Database file path. Defaults to "alexandria.db" in the application directory.
    /// </summary>
    public string DatabasePath { get; set; } = "alexandria.db";

    /// <summary>
    /// Connection mode. Defaults to Direct for best performance.
    /// </summary>
    public ConnectionType ConnectionMode { get; set; } = ConnectionType.Direct;

    /// <summary>
    /// Whether to upgrade database format if needed.
    /// </summary>
    public bool Upgrade { get; set; } = true;
}

/// <summary>
/// Manages the LiteDB database connection and provides access to collections.
/// Thread-safe singleton that manages a single LiteDatabase instance.
/// </summary>
public sealed class LiteDbContext : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly BsonMapper _mapper;
    private bool _disposed;

    public LiteDbContext(IOptions<LiteDbOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var dbOptions = options.Value;

        // Configure custom BsonMapper
        _mapper = ConfigureBsonMapper();

        // Create connection string
        var connectionString = new ConnectionString
        {
            Filename = dbOptions.DatabasePath,
            Connection = dbOptions.ConnectionMode,
            Upgrade = dbOptions.Upgrade
        };

        // Initialize database with custom mapper
        _database = new LiteDatabase(connectionString, _mapper);

        // Ensure indexes are created
        EnsureIndexes();
    }

    /// <summary>
    /// Gets the underlying LiteDatabase instance.
    /// </summary>
    public ILiteDatabase Database => _database;

    /// <summary>
    /// Gets the BsonMapper instance used for object mapping.
    /// </summary>
    public BsonMapper Mapper => _mapper;

    /// <summary>
    /// Configures the BsonMapper for custom type mappings.
    /// </summary>
    private static BsonMapper ConfigureBsonMapper()
    {
        var mapper = new BsonMapper();

        // Enable auto-mapping for all properties/fields
        mapper.IncludeFields = true;
        mapper.IncludeNonPublic = true;

        // NOTE: Removed custom Guid serialization to allow LiteDB to handle Guid natively
        // This is required for proper BsonId functionality with Guid types

        // Configure DateTime as UTC
        mapper.RegisterType(
            serialize: dt => dt.ToUniversalTime(),
            deserialize: bson => bson.AsDateTime.ToUniversalTime()
        );

        // Configure TimeSpan
        mapper.RegisterType(
            serialize: ts => ts.Ticks,
            deserialize: bson => TimeSpan.FromTicks(bson.AsInt64)
        );

        return mapper;
    }

    /// <summary>
    /// Ensures all required indexes are created for optimal query performance.
    /// </summary>
    private void EnsureIndexes()
    {
        // Book indexes
        var books = _database.GetCollection("books");
        books.EnsureIndex("Title.Value");
        books.EnsureIndex("Metadata.Isbn");
        books.EnsureIndex("Metadata.PublicationDate");
        books.EnsureIndex("Language.Code");

        // Bookmark indexes
        var bookmarks = _database.GetCollection("bookmarks");
        bookmarks.EnsureIndex("BookId");
        bookmarks.EnsureIndex("ChapterId");
        bookmarks.EnsureIndex("CreatedAt");

        // Annotation indexes
        var annotations = _database.GetCollection("annotations");
        annotations.EnsureIndex("BookId");
        annotations.EnsureIndex("ChapterId");
        annotations.EnsureIndex("Color");
        annotations.EnsureIndex("CreatedAt");
    }

    /// <summary>
    /// Executes a function within a transaction.
    /// </summary>
    public T ExecuteInTransaction<T>(Func<ILiteDatabase, T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        _database.BeginTrans();
        try
        {
            var result = action(_database);
            _database.Commit();
            return result;
        }
        catch
        {
            _database.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Executes an action within a transaction.
    /// </summary>
    public void ExecuteInTransaction(Action<ILiteDatabase> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        _database.BeginTrans();
        try
        {
            action(_database);
            _database.Commit();
        }
        catch
        {
            _database.Rollback();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _database?.Dispose();
        _disposed = true;
    }
}