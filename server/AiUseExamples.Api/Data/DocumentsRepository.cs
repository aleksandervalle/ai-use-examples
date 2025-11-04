using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiUseExamples.Api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace AiUseExamples.Api.Data;

public class DocumentsRepository
{
    private readonly string _connectionString;

    public DocumentsRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("AppDb") ?? "Data Source=./data/app.db";
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    public async Task InsertAsync(Document document, CancellationToken cancellationToken)
    {
        const string sql = @"INSERT INTO Documents (
            Id, OriginalFileName, StoredFileName, FilePath, MimeType, FileSize,
            DocType, ClassificationConfidence, BetterName, ExtractedDataJson,
            ProcessingStatus, ErrorMessage, EmbeddedAt, CreatedAt, UpdatedAt)
            VALUES (@Id, @OriginalFileName, @StoredFileName, @FilePath, @MimeType, @FileSize,
                    @DocType, @ClassificationConfidence, @BetterName, @ExtractedDataJson,
                    @ProcessingStatus, @ErrorMessage, @EmbeddedAt, @CreatedAt, @UpdatedAt);";

        await using var conn = CreateConnection();
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", document.Id.ToString());
        cmd.Parameters.AddWithValue("@OriginalFileName", document.OriginalFileName);
        cmd.Parameters.AddWithValue("@StoredFileName", document.StoredFileName);
        cmd.Parameters.AddWithValue("@FilePath", document.FilePath);
        cmd.Parameters.AddWithValue("@MimeType", document.MimeType);
        cmd.Parameters.AddWithValue("@FileSize", document.FileSize);
        cmd.Parameters.AddWithValue("@DocType", (object?)document.DocType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ClassificationConfidence", (object?)document.ClassificationConfidence ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@BetterName", (object?)document.BetterName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ExtractedDataJson", (object?)document.ExtractedDataJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ProcessingStatus", document.ProcessingStatus);
        cmd.Parameters.AddWithValue("@ErrorMessage", (object?)document.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EmbeddedAt", (object?)ToIso(document.EmbeddedAt) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", ToIso(document.CreatedAt));
        cmd.Parameters.AddWithValue("@UpdatedAt", ToIso(document.UpdatedAt));
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAfterClassificationAsync(Guid id, string storedFileName, string filePath, string docType, double confidence, string betterName, CancellationToken cancellationToken)
    {
        const string sql = @"UPDATE Documents SET
            StoredFileName = @StoredFileName,
            FilePath = @FilePath,
            DocType = @DocType,
            ClassificationConfidence = @ClassificationConfidence,
            BetterName = @BetterName,
            UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        await using var conn = CreateConnection();
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@StoredFileName", storedFileName);
        cmd.Parameters.AddWithValue("@FilePath", filePath);
        cmd.Parameters.AddWithValue("@DocType", docType);
        cmd.Parameters.AddWithValue("@ClassificationConfidence", confidence);
        cmd.Parameters.AddWithValue("@BetterName", betterName);
        cmd.Parameters.AddWithValue("@UpdatedAt", ToIso(DateTime.UtcNow));
        cmd.Parameters.AddWithValue("@Id", id.ToString());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateExtractionAsync(Guid id, string extractedDataJson, CancellationToken cancellationToken)
    {
        const string sql = @"UPDATE Documents SET
            ExtractedDataJson = @ExtractedDataJson,
            UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        await using var conn = CreateConnection();
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@ExtractedDataJson", extractedDataJson);
        cmd.Parameters.AddWithValue("@UpdatedAt", ToIso(DateTime.UtcNow));
        cmd.Parameters.AddWithValue("@Id", id.ToString());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateEmbeddedAsync(Guid id, DateTime embeddedAtUtc, CancellationToken cancellationToken)
    {
        const string sql = @"UPDATE Documents SET
            EmbeddedAt = @EmbeddedAt,
            ProcessingStatus = @ProcessingStatus,
            UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        await using var conn = CreateConnection();
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@EmbeddedAt", ToIso(embeddedAtUtc));
        cmd.Parameters.AddWithValue("@ProcessingStatus", "Completed");
        cmd.Parameters.AddWithValue("@UpdatedAt", ToIso(DateTime.UtcNow));
        cmd.Parameters.AddWithValue("@Id", id.ToString());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetFailedAsync(Guid id, string errorMessage, CancellationToken cancellationToken)
    {
        const string sql = @"UPDATE Documents SET
            ProcessingStatus = @ProcessingStatus,
            ErrorMessage = @ErrorMessage,
            UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        await using var conn = CreateConnection();
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@ProcessingStatus", "Failed");
        cmd.Parameters.AddWithValue("@ErrorMessage", errorMessage);
        cmd.Parameters.AddWithValue("@UpdatedAt", ToIso(DateTime.UtcNow));
        cmd.Parameters.AddWithValue("@Id", id.ToString());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Document>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var idList = ids?.ToList() ?? new List<Guid>();
        if (idList.Count == 0)
        {
            return Array.Empty<Document>();
        }

        var parameters = string.Join(",", idList.Select((_, i) => "@p" + i));
        var sql = $"SELECT * FROM Documents WHERE Id IN ({parameters})";

        await using var conn = CreateConnection();
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        for (int i = 0; i < idList.Count; i++)
        {
            cmd.Parameters.AddWithValue("@p" + i, idList[i].ToString());
        }

        var results = new List<Document>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(Map(reader));
        }
        return results;
    }

    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT * FROM Documents WHERE Id = @Id LIMIT 1";
        await using var conn = CreateConnection();
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id.ToString());
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return Map(reader);
        }
        return null;
    }

    public async Task<(IReadOnlyList<Document> Documents, int TotalCount)> GetPaginatedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        const string countSql = "SELECT COUNT(*) FROM Documents WHERE ProcessingStatus = 'Completed'";
        const string dataSql = "SELECT * FROM Documents WHERE ProcessingStatus = 'Completed' ORDER BY CreatedAt DESC LIMIT @Limit OFFSET @Offset";

        await using var conn = CreateConnection();
        await conn.OpenAsync(cancellationToken);

        // Get total count
        int totalCount = 0;
        await using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = countSql;
            var countResult = await countCmd.ExecuteScalarAsync(cancellationToken);
            totalCount = Convert.ToInt32(countResult ?? 0, CultureInfo.InvariantCulture);
        }

        // Get paginated results
        var results = new List<Document>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = dataSql;
            cmd.Parameters.AddWithValue("@Limit", pageSize);
            cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(Map(reader));
            }
        }

        return (results, totalCount);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM Documents WHERE Id = @Id";
        await using var conn = CreateConnection();
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id.ToString());
        var rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    private static Document Map(SqliteDataReader reader)
    {
        return new Document
        {
            Id = Guid.Parse(reader["Id"]?.ToString() ?? Guid.Empty.ToString()),
            OriginalFileName = reader["OriginalFileName"]?.ToString() ?? string.Empty,
            StoredFileName = reader["StoredFileName"]?.ToString() ?? string.Empty,
            FilePath = reader["FilePath"]?.ToString() ?? string.Empty,
            MimeType = reader["MimeType"]?.ToString() ?? string.Empty,
            FileSize = Convert.ToInt64(reader["FileSize"] ?? 0, CultureInfo.InvariantCulture),
            DocType = reader["DocType"] as string,
            ClassificationConfidence = reader["ClassificationConfidence"] == DBNull.Value ? null : Convert.ToDouble(reader["ClassificationConfidence"], CultureInfo.InvariantCulture),
            BetterName = reader["BetterName"] as string,
            ExtractedDataJson = reader["ExtractedDataJson"] as string,
            ProcessingStatus = reader["ProcessingStatus"]?.ToString() ?? string.Empty,
            ErrorMessage = reader["ErrorMessage"] as string,
            EmbeddedAt = ParseIso(reader["EmbeddedAt"] as string),
            CreatedAt = ParseIso(reader["CreatedAt"]?.ToString()) ?? DateTime.UtcNow,
            UpdatedAt = ParseIso(reader["UpdatedAt"]?.ToString()) ?? DateTime.UtcNow
        };
    }

    private static string ToIso(DateTime? dt)
    {
        return dt?.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static DateTime? ParseIso(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
        {
            return dt.ToUniversalTime();
        }
        return null;
    }
}


