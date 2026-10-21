using Dapper;
using IoTCommTool.Core.Abstractions;
using Microsoft.Data.Sqlite;

namespace IoTCommTool.Infrastructure;

/// <summary>连接配置的 SQLite 仓储</summary>
public sealed class ConnectionConfigRepository
{
    private const string SelectColumns =
        "id AS Id, protocol AS Protocol, display_name AS DisplayName, params_json AS ParamsJson, last_used_at AS LastUsedAt";

    private readonly string _connectionString;

    /// <summary>创建仓储；确保数据库目录存在并建表</summary>
    public ConnectionConfigRepository(string dbPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);

        string fullPath = Path.GetFullPath(dbPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _connectionString = $"Data Source={fullPath}";
        InitializeDatabase();
    }

    /// <summary>按协议列出配置，按最近使用时间降序</summary>
    public async Task<List<ConnectionConfigEntity>> ListByProtocolAsync(ProtocolType protocol)
    {
        await using var connection = new SqliteConnection(_connectionString);
        var rows = await connection.QueryAsync<ConnectionConfigEntity>(
            $"SELECT {SelectColumns} FROM connection_configs WHERE protocol = @protocol ORDER BY last_used_at DESC",
            new { protocol = protocol.ToString() }).ConfigureAwait(false);
        return rows.AsList();
    }

    /// <summary>列出全部配置，按最近使用时间降序</summary>
    public async Task<List<ConnectionConfigEntity>> ListAllAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        var rows = await connection.QueryAsync<ConnectionConfigEntity>(
            $"SELECT {SelectColumns} FROM connection_configs ORDER BY last_used_at DESC").ConfigureAwait(false);
        return rows.AsList();
    }

    /// <summary>同协议且参数相同则更新名称与最近使用时间，否则插入新记录；返回最终记录</summary>
    public async Task<ConnectionConfigEntity> UpsertAsync(ProtocolType protocol, string displayName, string paramsJson)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(paramsJson);

        string now = DateTime.Now.ToString("o");
        string protocolName = protocol.ToString();

        await using var connection = new SqliteConnection(_connectionString);
        var existing = await connection.QueryFirstOrDefaultAsync<ConnectionConfigEntity>(
            $"SELECT {SelectColumns} FROM connection_configs WHERE protocol = @protocolName AND params_json = @paramsJson",
            new { protocolName, paramsJson }).ConfigureAwait(false);

        if (existing is not null)
        {
            await connection.ExecuteAsync(
                "UPDATE connection_configs SET display_name = @displayName, last_used_at = @now WHERE id = @id",
                new { displayName, now, id = existing.Id }).ConfigureAwait(false);

            existing.DisplayName = displayName;
            existing.LastUsedAt = now;
            return existing;
        }

        long id = await connection.ExecuteScalarAsync<long>(
            "INSERT INTO connection_configs (protocol, display_name, params_json, last_used_at) VALUES (@protocolName, @displayName, @paramsJson, @now); SELECT last_insert_rowid();",
            new { protocolName, displayName, paramsJson, now }).ConfigureAwait(false);

        return new ConnectionConfigEntity
        {
            Id = id,
            Protocol = protocolName,
            DisplayName = displayName,
            ParamsJson = paramsJson,
            LastUsedAt = now
        };
    }

    /// <summary>按主键删除</summary>
    public async Task DeleteAsync(long id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.ExecuteAsync(
            "DELETE FROM connection_configs WHERE id = @id",
            new { id }).ConfigureAwait(false);
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Execute("""
            CREATE TABLE IF NOT EXISTS connection_configs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                protocol TEXT NOT NULL,
                display_name TEXT NOT NULL,
                params_json TEXT NOT NULL,
                last_used_at TEXT NOT NULL
            );
            """);
    }
}
