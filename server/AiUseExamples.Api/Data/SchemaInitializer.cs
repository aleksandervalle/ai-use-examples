using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace AiUseExamples.Api.Data;

public static class SchemaInitializer
{
    public static async Task EnsureCreatedAsync(IConfiguration configuration, CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("AppDb") ?? "Data Source=./data/app.db";
        var builder = new SqliteConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource;

        var dir = Path.GetDirectoryName(dataSource);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var createSql = @"CREATE TABLE IF NOT EXISTS Documents (
  Id TEXT PRIMARY KEY,
  OriginalFileName TEXT NOT NULL,
  StoredFileName TEXT NOT NULL,
  FilePath TEXT NOT NULL,
  MimeType TEXT NOT NULL,
  FileSize INTEGER NOT NULL,
  DocType TEXT,
  ClassificationConfidence REAL,
  BetterName TEXT,
  ExtractedDataJson TEXT,
  Description TEXT,
  ProcessingStatus TEXT NOT NULL,
  ErrorMessage TEXT,
  EmbeddedAt TEXT,
  CreatedAt TEXT NOT NULL,
  UpdatedAt TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_Documents_DocType ON Documents(DocType);
CREATE INDEX IF NOT EXISTS IX_Documents_ProcessingStatus ON Documents(ProcessingStatus);";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = createSql;
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        // Add Description column if it doesn't exist (for existing databases)
        // SQLite doesn't support IF NOT EXISTS for ALTER TABLE, so we check first
        try
        {
            await using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Documents') WHERE name='Description'";
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(cancellationToken));
            if (count == 0)
            {
                await using var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Documents ADD COLUMN Description TEXT";
                await alterCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        catch
        {
            // If the check fails, the column might already exist or table doesn't exist yet
            // Ignore and continue
        }
    }
}


