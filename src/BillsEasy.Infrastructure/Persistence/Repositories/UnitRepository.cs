using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Entities;
using Microsoft.Data.Sqlite;

namespace BillsEasy.Infrastructure.Persistence.Repositories;

public class UnitRepository : IUnitRepository
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;

    public UnitRepository(ISqliteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Unit?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, Name, PrintName, DecimalPlaces, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Units
            WHERE CompanyId = @CompanyId AND Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapFromReader(reader);
    }

    public async Task<Unit?> GetByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, Name, PrintName, DecimalPlaces, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Units
            WHERE CompanyId = @CompanyId AND LOWER(Name) = LOWER(@Name);
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Name", name.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapFromReader(reader);
    }

    public async Task<IReadOnlyList<Unit>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, Name, PrintName, DecimalPlaces, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Units
            WHERE CompanyId = @CompanyId
            ORDER BY Name ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());

        var list = new List<Unit>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapFromReader(reader));
        }

        return list.AsReadOnly();
    }

    public async Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        var sql = @"
            SELECT COUNT(1) FROM Units
            WHERE CompanyId = @CompanyId AND LOWER(Name) = LOWER(@Name)
        ";

        if (excludeId.HasValue)
        {
            sql += " AND Id <> @ExcludeId";
            cmd.Parameters.AddWithValue("@ExcludeId", excludeId.Value.ToString());
        }

        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Name", name.Trim());

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task<bool> HasReferencingItemsAsync(Guid companyId, Guid unitId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT COUNT(1) FROM Items
            WHERE CompanyId = @CompanyId AND (UnitId = @UnitId OR AlternateUnitId = @UnitId);
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@UnitId", unitId.ToString());

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task AddAsync(Unit unit, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO Units (Id, CompanyId, Name, PrintName, DecimalPlaces, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy)
            VALUES (@Id, @CompanyId, @Name, @PrintName, @DecimalPlaces, @CreatedAt, @CreatedBy, @ModifiedAt, @ModifiedBy);
        ";
        AddParameters(cmd, unit);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Unit unit, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            UPDATE Units
            SET Name = @Name,
                PrintName = @PrintName,
                DecimalPlaces = @DecimalPlaces,
                ModifiedAt = @ModifiedAt,
                ModifiedBy = @ModifiedBy
            WHERE CompanyId = @CompanyId AND Id = @Id;
        ";
        AddParameters(cmd, unit);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "DELETE FROM Units WHERE CompanyId = @CompanyId AND Id = @Id;";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(SqliteCommand cmd, Unit unit)
    {
        cmd.Parameters.AddWithValue("@Id", unit.Id.ToString());
        cmd.Parameters.AddWithValue("@CompanyId", unit.CompanyId.ToString());
        cmd.Parameters.AddWithValue("@Name", unit.Name);
        cmd.Parameters.AddWithValue("@PrintName", unit.PrintName);
        cmd.Parameters.AddWithValue("@DecimalPlaces", unit.DecimalPlaces);
        cmd.Parameters.AddWithValue("@CreatedAt", unit.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@CreatedBy", unit.CreatedBy ?? "system");
        cmd.Parameters.AddWithValue("@ModifiedAt", unit.ModifiedAt.HasValue ? unit.ModifiedAt.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@ModifiedBy", (object?)unit.ModifiedBy ?? DBNull.Value);
    }

    private static Unit MapFromReader(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id")));
        var companyId = Guid.Parse(reader.GetString(reader.GetOrdinal("CompanyId")));
        var name = reader.GetString(reader.GetOrdinal("Name"));
        var printName = reader.GetString(reader.GetOrdinal("PrintName"));
        var decimalPlaces = reader.GetInt32(reader.GetOrdinal("DecimalPlaces"));

        var unit = new Unit(id, companyId, name, printName, decimalPlaces);

        unit.CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")), null, System.Globalization.DateTimeStyles.RoundtripKind);
        unit.CreatedBy = reader.GetString(reader.GetOrdinal("CreatedBy"));

        if (!reader.IsDBNull(reader.GetOrdinal("ModifiedAt")))
        {
            unit.ModifiedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("ModifiedAt")), null, System.Globalization.DateTimeStyles.RoundtripKind);
            unit.ModifiedBy = reader.IsDBNull(reader.GetOrdinal("ModifiedBy")) ? null : reader.GetString(reader.GetOrdinal("ModifiedBy"));
        }

        return unit;
    }
}
