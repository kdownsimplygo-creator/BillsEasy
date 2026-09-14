using System;
using System.IO;
using System.Threading.Tasks;
using BillsEasy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BillsEasy.IntegrationTests.Infrastructure.Persistence;

public class SqliteConnectionFactoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _connectionString;

    public SqliteConnectionFactoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_Test_{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_testDbPath}";
    }

    [Fact]
    public void CreateConnection_OpensConnectionAndConfiguresWalAndForeignKeys()
    {
        var factory = new SqliteConnectionFactory(_connectionString);

        using (var connection = factory.CreateConnection())
        {
            connection.State.Should().Be(System.Data.ConnectionState.Open);

            // Verify Foreign Keys Pragma
            using (var fkCmd = connection.CreateCommand())
            {
                fkCmd.CommandText = "PRAGMA foreign_keys;";
                var fkResult = fkCmd.ExecuteScalar();
                Convert.ToInt64(fkResult).Should().Be(1L, "foreign_keys should be enabled");
            }

            // Verify WAL Mode Pragma
            using (var walCmd = connection.CreateCommand())
            {
                walCmd.CommandText = "PRAGMA journal_mode;";
                var walResult = walCmd.ExecuteScalar();
                walResult?.ToString().Should().BeEquivalentTo("wal", "journal_mode should be WAL");
            }
        }
    }

    [Fact]
    public async Task CreateConnectionAsync_ExecutesSqlTableCreationInsertAndSelect()
    {
        var factory = new SqliteConnectionFactory(_connectionString);

        await using var connection = await factory.CreateConnectionAsync();

        // 1. Create table
        await using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = @"
                CREATE TABLE TestAccountingMasters (
                    Id TEXT PRIMARY KEY,
                    Code TEXT NOT NULL UNIQUE,
                    Name TEXT NOT NULL,
                    OpeningBalance REAL NOT NULL
                );";
            await createCmd.ExecuteNonQueryAsync();
        }

        // 2. Insert row
        var masterId = Guid.NewGuid().ToString();
        await using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.CommandText = @"
                INSERT INTO TestAccountingMasters (Id, Code, Name, OpeningBalance)
                VALUES (@Id, @Code, @Name, @OpeningBalance);";

            var idParam = insertCmd.CreateParameter();
            idParam.ParameterName = "@Id";
            idParam.Value = masterId;
            insertCmd.Parameters.Add(idParam);

            var codeParam = insertCmd.CreateParameter();
            codeParam.ParameterName = "@Code";
            codeParam.Value = "SD-001";
            insertCmd.Parameters.Add(codeParam);

            var nameParam = insertCmd.CreateParameter();
            nameParam.ParameterName = "@Name";
            nameParam.Value = "Sundry Debtors North";
            insertCmd.Parameters.Add(nameParam);

            var balParam = insertCmd.CreateParameter();
            balParam.ParameterName = "@OpeningBalance";
            balParam.Value = 45000.50;
            insertCmd.Parameters.Add(balParam);

            var rowsAffected = await insertCmd.ExecuteNonQueryAsync();
            rowsAffected.Should().Be(1);
        }

        // 3. Select and verify row
        await using (var selectCmd = connection.CreateCommand())
        {
            selectCmd.CommandText = "SELECT Name, OpeningBalance FROM TestAccountingMasters WHERE Code = @Code;";

            var codeParam = selectCmd.CreateParameter();
            codeParam.ParameterName = "@Code";
            codeParam.Value = "SD-001";
            selectCmd.Parameters.Add(codeParam);

            await using var reader = await selectCmd.ExecuteReaderAsync();
            var hasRow = await reader.ReadAsync();

            hasRow.Should().BeTrue();
            reader.GetString(0).Should().Be("Sundry Debtors North");
            reader.GetDouble(1).Should().Be(45000.50);
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
            var walPath = _testDbPath + "-wal";
            if (File.Exists(walPath)) File.Delete(walPath);
            var shmPath = _testDbPath + "-shm";
            if (File.Exists(shmPath)) File.Delete(shmPath);
        }
        catch
        {
            // Best effort
        }
    }
}
