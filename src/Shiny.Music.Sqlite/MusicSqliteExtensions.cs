using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Shiny.DocumentDb;
using Shiny.DocumentDb.Sqlite;
using Shiny.Music;
using Shiny.Music.Sqlite;

namespace Shiny;

public static class MusicSqliteExtensions
{
    public static IServiceCollection AddMusicManagementSqlite(this IServiceCollection services)
    {
        services.AddDocumentStore(opts =>
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dbPath = Path.Combine(appData, "music.db");

            opts.DatabaseProvider = new SqliteDatabaseProvider($"Data Source={dbPath}");
            opts.UseReflectionFallback = false;
            opts.JsonSerializerOptions = MusicSqliteJsonContext.Default.Options;
        });
        services.AddSingleton<IMusicManager, SqliteMusicManager>();
        return services;
    }
}

[JsonSerializable(typeof(MusicMetadata))]
[JsonSerializable(typeof(PlayCount))]
[JsonSerializable(typeof(PlayCountDoc))]
[JsonSerializable(typeof(PlaylistDoc))]
[JsonSerializable(typeof(PlaylistTrackDoc))]
public partial class MusicSqliteJsonContext : JsonSerializerContext;