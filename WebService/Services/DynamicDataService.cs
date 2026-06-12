using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WebService.Data;

namespace WebService.Services;

public class DynamicDataService
{
    private readonly string _connectionString;
    private readonly AppDbContext _context;

    public DynamicDataService(IConfiguration configuration, AppDbContext context)
    {
        _connectionString = "Data Source=C:\\inetpub\\wwwroot\\WebService\\data.db";

        _context = context;
    }

    public async Task SaveDynamicRowsAsync(
        string tableName,
        List<Dictionary<string, object?>> rows,
        int definitionId)
    {
        if (string.IsNullOrWhiteSpace(tableName) || rows == null || rows.Count == 0)
            return;

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        await ConfigureConnectionAsync(conn);

        var allColumns = GetAllColumns(rows);

        await EnsureTableExistsAsync(conn, tableName, allColumns);
        await EnsureColumnsExistAsync(conn, tableName, allColumns);

        var definition = await _context.Definitions
            .FirstOrDefaultAsync(x => x.Id == definitionId);

        if (definition == null)
            throw new Exception($"Definition bulunamadı. Id={definitionId}");

        if (definition.TruncateInsert == 1)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM [{tableName}]";
            await cmd.ExecuteNonQueryAsync();

            Console.WriteLine($"TRUNCATE uygulandı → {tableName}");
        }

        foreach (var row in rows)
        {
            await InsertRowAsync(conn, tableName, row);
        }
    }

    private async Task ConfigureConnectionAsync(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        await cmd.ExecuteNonQueryAsync();
    }

    private List<string> GetAllColumns(List<Dictionary<string, object?>> rows)
    {
        return rows
            .SelectMany(r => r.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task EnsureTableExistsAsync(SqliteConnection conn, string tableName, List<string> columns)
    {
        var checkSql = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}'";

        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = checkSql;

        var result = await checkCmd.ExecuteScalarAsync();

        if (result != null && result != DBNull.Value)
            return;

        var columnSql = string.Join(", ", columns.Select(c => $"[{c}] TEXT"));
        var createSql = $"CREATE TABLE [{tableName}] ({columnSql})";

        using var createCmd = conn.CreateCommand();
        createCmd.CommandText = createSql;
        await createCmd.ExecuteNonQueryAsync();
    }

    private async Task EnsureColumnsExistAsync(SqliteConnection conn, string tableName, List<string> columns)
    {
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var pragmaCmd = conn.CreateCommand())
        {
            pragmaCmd.CommandText = $"PRAGMA table_info([{tableName}])";

            using var reader = await pragmaCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                existingColumns.Add(reader["name"]?.ToString() ?? "");
            }
        }

        foreach (var column in columns)
        {
            if (!existingColumns.Contains(column))
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = $"ALTER TABLE [{tableName}] ADD COLUMN [{column}] TEXT";
                await alterCmd.ExecuteNonQueryAsync();
            }
        }
    }

    private async Task InsertRowAsync(SqliteConnection conn, string tableName, Dictionary<string, object?> row)
    {
        var columns = string.Join(", ", row.Keys.Select(k => $"[{k}]"));
        var parameters = string.Join(", ", row.Keys.Select((k, i) => $"@p{i}"));

        var insertSql = $"INSERT INTO [{tableName}] ({columns}) VALUES ({parameters})";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = insertSql;

        int index = 0;
        foreach (var item in row)
        {
            cmd.Parameters.AddWithValue($"@p{index}", NormalizeValue(item.Value));
            index++;
        }

        await cmd.ExecuteNonQueryAsync();
    }

    private object? NormalizeValue(object? value)
    {
        if (value == null)
            return DBNull.Value;

        if (value is JsonElement jsonElement)
        {
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.String:
                    return jsonElement.GetString() ?? (object)DBNull.Value;

                case JsonValueKind.Number:
                    if (jsonElement.TryGetInt32(out var intValue))
                        return intValue;

                    if (jsonElement.TryGetInt64(out var longValue))
                        return longValue;

                    if (jsonElement.TryGetDecimal(out var decimalValue))
                        return decimalValue;

                    return jsonElement.ToString();

                case JsonValueKind.True:
                case JsonValueKind.False:
                    return jsonElement.GetBoolean();

                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return DBNull.Value;

                default:
                    return jsonElement.ToString();
            }
        }

        return value;
    }
}