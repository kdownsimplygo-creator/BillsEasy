using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using Microsoft.Data.Sqlite;

namespace BillsEasy.Infrastructure.Persistence.Repositories;

public class AccountGroupRepository : IAccountGroupRepository
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;

    public AccountGroupRepository(ISqliteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AccountGroup?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, Name, ParentGroupId, Nature, IsPrimary, IsDefaultSystemGroup, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM AccountGroups
            WHERE CompanyId = @CompanyId AND Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapFromReader(reader);
    }

    public async Task<AccountGroup?> GetByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, Name, ParentGroupId, Nature, IsPrimary, IsDefaultSystemGroup, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM AccountGroups
            WHERE CompanyId = @CompanyId AND LOWER(Name) = LOWER(@Name);
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Name", name.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapFromReader(reader);
    }

    public async Task<IReadOnlyList<AccountGroup>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, Name, ParentGroupId, Nature, IsPrimary, IsDefaultSystemGroup, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM AccountGroups
            WHERE CompanyId = @CompanyId
            ORDER BY Nature ASC, Name ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());

        var list = new List<AccountGroup>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapFromReader(reader));
        }

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<AccountGroup>> GetByParentIdAsync(Guid companyId, Guid parentId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, Name, ParentGroupId, Nature, IsPrimary, IsDefaultSystemGroup, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM AccountGroups
            WHERE CompanyId = @CompanyId AND ParentGroupId = @ParentGroupId
            ORDER BY Name ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@ParentGroupId", parentId.ToString());

        var list = new List<AccountGroup>();
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
            SELECT COUNT(1) FROM AccountGroups
            WHERE CompanyId = @CompanyId 
              AND LOWER(Name) = LOWER(@Name)
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
            SELECT COUNT(1) FROM AccountGroups
            WHERE CompanyId = @CompanyId AND ParentGroupId = @GroupId;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@GroupId", groupId.ToString());

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task<bool> HasLedgersAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        // Check if Ledgers table exists first
        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='Ledgers';";
            var tableCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken));
            if (tableCount == 0)
                return false;
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(1) FROM Ledgers
            WHERE CompanyId = @CompanyId AND GroupId = @GroupId;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@GroupId", groupId.ToString());

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task AddAsync(AccountGroup group, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO AccountGroups (
                Id, CompanyId, Name, ParentGroupId, Nature, IsPrimary, IsDefaultSystemGroup, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            ) VALUES (
                @Id, @CompanyId, @Name, @ParentGroupId, @Nature, @IsPrimary, @IsDefaultSystemGroup, @CreatedAt, @CreatedBy, @ModifiedAt, @ModifiedBy
            );
        ";

        AddParameters(cmd, group);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<AccountGroup> groups, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        foreach (var group in groups)
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT INTO AccountGroups (
                    Id, CompanyId, Name, ParentGroupId, Nature, IsPrimary, IsDefaultSystemGroup, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
                ) VALUES (
                    @Id, @CompanyId, @Name, @ParentGroupId, @Nature, @IsPrimary, @IsDefaultSystemGroup, @CreatedAt, @CreatedBy, @ModifiedAt, @ModifiedBy
                );
            ";

            AddParameters(cmd, group);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateAsync(AccountGroup group, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            UPDATE AccountGroups SET
                Name = @Name,
                ParentGroupId = @ParentGroupId,
                Nature = @Nature,
                IsPrimary = @IsPrimary,
                IsDefaultSystemGroup = @IsDefaultSystemGroup,
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

        cmd.CommandText = "DELETE FROM AccountGroups WHERE CompanyId = @CompanyId AND Id = @Id;";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(SqliteCommand cmd, AccountGroup group)
    {
        cmd.Parameters.AddWithValue("@Id", group.Id.ToString());
        cmd.Parameters.AddWithValue("@CompanyId", group.CompanyId.ToString());
        cmd.Parameters.AddWithValue("@Name", group.Name);
        cmd.Parameters.AddWithValue("@ParentGroupId", group.ParentGroupId.HasValue ? (object)group.ParentGroupId.Value.ToString() : DBNull.Value);
        cmd.Parameters.AddWithValue("@Nature", (int)group.Nature);
        cmd.Parameters.AddWithValue("@IsPrimary", group.IsPrimary ? 1 : 0);
        cmd.Parameters.AddWithValue("@IsDefaultSystemGroup", group.IsDefaultSystemGroup ? 1 : 0);
        cmd.Parameters.AddWithValue("@CreatedAt", group.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@CreatedBy", group.CreatedBy);
        cmd.Parameters.AddWithValue("@ModifiedAt", group.ModifiedAt.HasValue ? (object)group.ModifiedAt.Value.ToString("o") : DBNull.Value);
        cmd.Parameters.AddWithValue("@ModifiedBy", group.ModifiedBy ?? (object)DBNull.Value);
    }

    private static AccountGroup MapFromReader(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var companyId = Guid.Parse(reader.GetString(1));
        var name = reader.GetString(2);
        Guid? parentGroupId = reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3));
        var nature = (GroupNature)reader.GetInt32(4);
        var isPrimary = reader.GetInt32(5) == 1;
        var isDefaultSystemGroup = reader.GetInt32(6) == 1;

        return new AccountGroup(id, companyId, name, nature, parentGroupId, isPrimary, isDefaultSystemGroup)
        {
            CreatedAt = DateTime.Parse(reader.GetString(7)),
            CreatedBy = reader.GetString(8),
            ModifiedAt = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9)),
            ModifiedBy = reader.IsDBNull(10) ? null : reader.GetString(10)
        };
    }
}