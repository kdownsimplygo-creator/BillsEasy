using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Entities;
using Microsoft.Data.Sqlite;

namespace BillsEasy.Infrastructure.Persistence.Repositories;

public class ItemGroupRepository : IItemGroupRepository
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;

    public ItemGroupRepository(ISqliteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ItemGroup?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, Name, ParentGroupId, Description, IsDefaultSystemGroup, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM ItemGroups
            WHERE CompanyId = @CompanyId AND Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapFromReader(reader);
    }

    public async Task<ItemGroup?> GetByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, Name, ParentGroupId, Description, IsDefaultSystemGroup, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM ItemGroups
            WHERE CompanyId = @CompanyId AND LOWER(Name) = LOWER(@Name);
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Name", name.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapFromReader(reader);
    }

    public async Task<IReadOnlyList<ItemGroup>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, Name, ParentGroupId, Description, IsDefaultSystemGroup, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM ItemGroups
            WHERE CompanyId = @CompanyId
            ORDER BY Name ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());

        var list = new List<ItemGroup>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapFromReader(reader));
        }

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<ItemGroup>> GetByParentIdAsync(Guid companyId, Guid parentId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, Name, ParentGroupId, Description, IsDefaultSystemGroup, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM ItemGroups
            WHERE CompanyId = @CompanyId AND ParentGroupId = @ParentGroupId
            ORDER BY Name ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@ParentGroupId", parentId.ToString());

        var list = new List<ItemGroup>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapFromReader(reader));
        }

        return list.AsReadOnly();
    }

    public async Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? parentGroupId = null, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        var sql = @"
            SELECT COUNT(1) FROM ItemGroups
            WHERE CompanyId = @CompanyId AND LOWER(Name) = LOWER(@Name)
        ";

        if (parentGroupId.HasValue)
        {
            sql += " AND ParentGroupId = @ParentGroupId";
            cmd.Parameters.AddWithValue("@ParentGroupId", parentGroupId.Value.ToString());
        }
        else
        {
            sql += " AND ParentGroupId IS NULL";
        }

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

    public async Task<bool> HasChildrenAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT COUNT(1) FROM ItemGroups
            WHERE CompanyId = @CompanyId AND ParentGroupId = @GroupId;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@GroupId", groupId.ToString());

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task<bool> HasReferencingItemsAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT COUNT(1) FROM Items
            WHERE CompanyId = @CompanyId AND ItemGroupId = @GroupId;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@GroupId", groupId.ToString());

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task AddAsync(ItemGroup group, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO ItemGroups (Id, CompanyId, Name, ParentGroupId, Description, IsDefaultSystemGroup, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy)
            VALUES (@Id, @CompanyId, @Name, @ParentGroupId, @Description, @IsDefaultSystemGroup, @CreatedAt, @CreatedBy, @ModifiedAt, @ModifiedBy);
        ";
        AddParameters(cmd, group);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(ItemGroup group, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            UPDATE ItemGroups
            SET Name = @Name,
                ParentGroupId = @ParentGroupId,
                Description = @Description,
                ModifiedAt = @ModifiedAt,
                ModifiedBy = @ModifiedBy
            WHERE CompanyId = @CompanyId AND Id = @Id;
        ";
        AddParameters(cmd, group);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "DELETE FROM ItemGroups WHERE CompanyId = @CompanyId AND Id = @Id;";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(SqliteCommand cmd, ItemGroup group)
    {
        cmd.Parameters.AddWithValue("@Id", group.Id.ToString());
        cmd.Parameters.AddWithValue("@CompanyId", group.CompanyId.ToString());
        cmd.Parameters.AddWithValue("@Name", group.Name);
        cmd.Parameters.AddWithValue("@ParentGroupId", group.ParentGroupId.HasValue ? group.ParentGroupId.Value.ToString() : DBNull.Value);
        cmd.Parameters.AddWithValue("@Description", (object?)group.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IsDefaultSystemGroup", group.IsDefaultSystemGroup ? 1 : 0);
        cmd.Parameters.AddWithValue("@CreatedAt", group.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@CreatedBy", group.CreatedBy ?? "system");
        cmd.Parameters.AddWithValue("@ModifiedAt", group.ModifiedAt.HasValue ? group.ModifiedAt.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@ModifiedBy", (object?)group.ModifiedBy ?? DBNull.Value);
    }

    private static ItemGroup MapFromReader(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id")));
        var companyId = Guid.Parse(reader.GetString(reader.GetOrdinal("CompanyId")));
        var name = reader.GetString(reader.GetOrdinal("Name"));
        var parentGroupId = reader.IsDBNull(reader.GetOrdinal("ParentGroupId")) ? (Guid?)null : Guid.Parse(reader.GetString(reader.GetOrdinal("ParentGroupId")));
        var description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description"));
        var isDefault = reader.GetInt32(reader.GetOrdinal("IsDefaultSystemGroup")) == 1;

        var group = new ItemGroup(id, companyId, name, parentGroupId, description, isDefault);

        group.CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")), null, System.Globalization.DateTimeStyles.RoundtripKind);
        group.CreatedBy = reader.GetString(reader.GetOrdinal("CreatedBy"));

        if (!reader.IsDBNull(reader.GetOrdinal("ModifiedAt")))
        {
            group.ModifiedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("ModifiedAt")), null, System.Globalization.DateTimeStyles.RoundtripKind);
            group.ModifiedBy = reader.IsDBNull(reader.GetOrdinal("ModifiedBy")) ? null : reader.GetString(reader.GetOrdinal("ModifiedBy"));
        }

        return group;
    }
}
