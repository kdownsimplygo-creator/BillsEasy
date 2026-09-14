using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using Microsoft.Data.Sqlite;

namespace BillsEasy.Infrastructure.Persistence;

/// <summary>
/// Thread-safe factory for SQLite connections configured with WAL mode and foreign key enforcement.
/// </summary>
public class SqliteConnectionFactory : ISqliteDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));

        _connectionString = connectionString;
    }

    public DbConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        ConfigurePragmas(connection);
        return connection;
    }

    public async Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await ConfigurePragmasAsync(connection, cancellationToken);
        return connection;
    }

    private static void ConfigurePragmas(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        if (!connection.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
        }
        else
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
        }
        command.ExecuteNonQuery();
    }

    private static async Task ConfigurePragmasAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        if (!connection.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
        }
        else
        {
            command.CommandText = "PRAGMA foreign_keys = ON;";
        }
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
