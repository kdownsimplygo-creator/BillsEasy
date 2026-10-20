using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.ValueObjects;
using Microsoft.Data.Sqlite;

namespace BillsEasy.Infrastructure.Persistence.Repositories;

public class ItemRepository : IItemRepository
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;

    public ItemRepository(ISqliteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Item?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, ItemGroupId, UnitId, AlternateUnitId, ConversionFactor,
                   Name, PrintName, Code,
                   OpeningStockQuantity, OpeningStockRate, OpeningStockValue,
                   SalesPrice, PurchasePrice, MRP, MinSalePrice, DiscountPercent,
                   MinLevel, MaxLevel, ReorderLevel,
                   Description, Barcode, IsActive,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Items
            WHERE CompanyId = @CompanyId AND Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapFromReader(reader);
    }

    public async Task<Item?> GetByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, ItemGroupId, UnitId, AlternateUnitId, ConversionFactor,
                   Name, PrintName, Code,
                   OpeningStockQuantity, OpeningStockRate, OpeningStockValue,
                   SalesPrice, PurchasePrice, MRP, MinSalePrice, DiscountPercent,
                   MinLevel, MaxLevel, ReorderLevel,
                   Description, Barcode, IsActive,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Items
            WHERE CompanyId = @CompanyId AND LOWER(Name) = LOWER(@Name);
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Name", name.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapFromReader(reader);
    }

    public async Task<Item?> GetByCodeAsync(Guid companyId, string code, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, ItemGroupId, UnitId, AlternateUnitId, ConversionFactor,
                   Name, PrintName, Code,
                   OpeningStockQuantity, OpeningStockRate, OpeningStockValue,
                   SalesPrice, PurchasePrice, MRP, MinSalePrice, DiscountPercent,
                   MinLevel, MaxLevel, ReorderLevel,
                   Description, Barcode, IsActive,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Items
            WHERE CompanyId = @CompanyId AND LOWER(Code) = LOWER(@Code);
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Code", code.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapFromReader(reader);
    }

    public async Task<IReadOnlyList<Item>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, ItemGroupId, UnitId, AlternateUnitId, ConversionFactor,
                   Name, PrintName, Code,
                   OpeningStockQuantity, OpeningStockRate, OpeningStockValue,
                   SalesPrice, PurchasePrice, MRP, MinSalePrice, DiscountPercent,
                   MinLevel, MaxLevel, ReorderLevel,
                   Description, Barcode, IsActive,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Items
            WHERE CompanyId = @CompanyId
            ORDER BY Name ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());

        var list = new List<Item>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapFromReader(reader));
        }

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<Item>> GetByGroupIdAsync(Guid companyId, Guid itemGroupId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, ItemGroupId, UnitId, AlternateUnitId, ConversionFactor,
                   Name, PrintName, Code,
                   OpeningStockQuantity, OpeningStockRate, OpeningStockValue,
                   SalesPrice, PurchasePrice, MRP, MinSalePrice, DiscountPercent,
                   MinLevel, MaxLevel, ReorderLevel,
                   Description, Barcode, IsActive,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Items
            WHERE CompanyId = @CompanyId AND ItemGroupId = @ItemGroupId
            ORDER BY Name ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@ItemGroupId", itemGroupId.ToString());

        var list = new List<Item>();
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
            SELECT COUNT(1) FROM Items
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

    public async Task<bool> ExistsByCodeAsync(Guid companyId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        var sql = @"
            SELECT COUNT(1) FROM Items
            WHERE CompanyId = @CompanyId AND LOWER(Code) = LOWER(@Code)
        ";

        if (excludeId.HasValue)
        {
            sql += " AND Id <> @ExcludeId";
            cmd.Parameters.AddWithValue("@ExcludeId", excludeId.Value.ToString());
        }

        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Code", code.Trim());

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task AddAsync(Item item, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO Items (
                Id, CompanyId, ItemGroupId, UnitId, AlternateUnitId, ConversionFactor,
                Name, PrintName, Code,
                OpeningStockQuantity, OpeningStockRate, OpeningStockValue,
                SalesPrice, PurchasePrice, MRP, MinSalePrice, DiscountPercent,
                MinLevel, MaxLevel, ReorderLevel,
                Description, Barcode, IsActive,
                CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            ) VALUES (
                @Id, @CompanyId, @ItemGroupId, @UnitId, @AlternateUnitId, @ConversionFactor,
                @Name, @PrintName, @Code,
                @OpeningStockQuantity, @OpeningStockRate, @OpeningStockValue,
                @SalesPrice, @PurchasePrice, @MRP, @MinSalePrice, @DiscountPercent,
                @MinLevel, @MaxLevel, @ReorderLevel,
                @Description, @Barcode, @IsActive,
                @CreatedAt, @CreatedBy, @ModifiedAt, @ModifiedBy
            );
        ";
        AddParameters(cmd, item);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Item item, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            UPDATE Items
            SET ItemGroupId = @ItemGroupId,
                UnitId = @UnitId,
                AlternateUnitId = @AlternateUnitId,
                ConversionFactor = @ConversionFactor,
                Name = @Name,
                PrintName = @PrintName,
                Code = @Code,
                OpeningStockQuantity = @OpeningStockQuantity,
                OpeningStockRate = @OpeningStockRate,
                OpeningStockValue = @OpeningStockValue,
                SalesPrice = @SalesPrice,
                PurchasePrice = @PurchasePrice,
                MRP = @MRP,
                MinSalePrice = @MinSalePrice,
                DiscountPercent = @DiscountPercent,
                MinLevel = @MinLevel,
                MaxLevel = @MaxLevel,
                ReorderLevel = @ReorderLevel,
                Description = @Description,
                Barcode = @Barcode,
                IsActive = @IsActive,
                ModifiedAt = @ModifiedAt,
                ModifiedBy = @ModifiedBy
            WHERE CompanyId = @CompanyId AND Id = @Id;
        ";
        AddParameters(cmd, item);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "DELETE FROM Items WHERE CompanyId = @CompanyId AND Id = @Id;";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(SqliteCommand cmd, Item item)
    {
        cmd.Parameters.AddWithValue("@Id", item.Id.ToString());
        cmd.Parameters.AddWithValue("@CompanyId", item.CompanyId.ToString());
        cmd.Parameters.AddWithValue("@ItemGroupId", item.ItemGroupId.ToString());
        cmd.Parameters.AddWithValue("@UnitId", item.UnitId.ToString());
        cmd.Parameters.AddWithValue("@AlternateUnitId", item.AlternateUnitId.HasValue ? item.AlternateUnitId.Value.ToString() : DBNull.Value);
        cmd.Parameters.AddWithValue("@ConversionFactor", item.ConversionFactor.HasValue ? (object)item.ConversionFactor.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@Name", item.Name);
        cmd.Parameters.AddWithValue("@PrintName", item.PrintName);
        cmd.Parameters.AddWithValue("@Code", (object?)item.Code ?? DBNull.Value);

        cmd.Parameters.AddWithValue("@OpeningStockQuantity", item.OpeningStockQuantity);
        cmd.Parameters.AddWithValue("@OpeningStockRate", item.OpeningStockRate.ToMinorUnits());
        cmd.Parameters.AddWithValue("@OpeningStockValue", item.OpeningStockValue.ToMinorUnits());

        cmd.Parameters.AddWithValue("@SalesPrice", item.SalesPrice.HasValue ? (object)item.SalesPrice.Value.ToMinorUnits() : DBNull.Value);
        cmd.Parameters.AddWithValue("@PurchasePrice", item.PurchasePrice.HasValue ? (object)item.PurchasePrice.Value.ToMinorUnits() : DBNull.Value);
        cmd.Parameters.AddWithValue("@MRP", item.MRP.HasValue ? (object)item.MRP.Value.ToMinorUnits() : DBNull.Value);
        cmd.Parameters.AddWithValue("@MinSalePrice", item.MinSalePrice.HasValue ? (object)item.MinSalePrice.Value.ToMinorUnits() : DBNull.Value);
        cmd.Parameters.AddWithValue("@DiscountPercent", item.DiscountPercent.HasValue ? (object)item.DiscountPercent.Value : DBNull.Value);

        cmd.Parameters.AddWithValue("@MinLevel", item.MinLevel.HasValue ? (object)item.MinLevel.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@MaxLevel", item.MaxLevel.HasValue ? (object)item.MaxLevel.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@ReorderLevel", item.ReorderLevel.HasValue ? (object)item.ReorderLevel.Value : DBNull.Value);

        cmd.Parameters.AddWithValue("@Description", (object?)item.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Barcode", (object?)item.Barcode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IsActive", item.IsActive ? 1 : 0);

        cmd.Parameters.AddWithValue("@CreatedAt", item.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@CreatedBy", item.CreatedBy ?? "system");
        cmd.Parameters.AddWithValue("@ModifiedAt", item.ModifiedAt.HasValue ? item.ModifiedAt.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@ModifiedBy", (object?)item.ModifiedBy ?? DBNull.Value);
    }

    private static Item MapFromReader(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id")));
        var companyId = Guid.Parse(reader.GetString(reader.GetOrdinal("CompanyId")));
        var itemGroupId = Guid.Parse(reader.GetString(reader.GetOrdinal("ItemGroupId")));
        var unitId = Guid.Parse(reader.GetString(reader.GetOrdinal("UnitId")));
        var altUnitId = reader.IsDBNull(reader.GetOrdinal("AlternateUnitId")) ? (Guid?)null : Guid.Parse(reader.GetString(reader.GetOrdinal("AlternateUnitId")));
        var conversionFactor = reader.IsDBNull(reader.GetOrdinal("ConversionFactor")) ? (decimal?)null : Convert.ToDecimal(reader.GetDouble(reader.GetOrdinal("ConversionFactor")));

        var name = reader.GetString(reader.GetOrdinal("Name"));
        var printName = reader.GetString(reader.GetOrdinal("PrintName"));
        var code = reader.IsDBNull(reader.GetOrdinal("Code")) ? null : reader.GetString(reader.GetOrdinal("Code"));

        var openingStockQuantity = Convert.ToDecimal(reader.GetDouble(reader.GetOrdinal("OpeningStockQuantity")));
        var openingStockRate = Money.FromMinorUnits(reader.GetInt64(reader.GetOrdinal("OpeningStockRate")));
        var openingStockValue = Money.FromMinorUnits(reader.GetInt64(reader.GetOrdinal("OpeningStockValue")));

        Money? salesPrice = reader.IsDBNull(reader.GetOrdinal("SalesPrice")) ? null : Money.FromMinorUnits(reader.GetInt64(reader.GetOrdinal("SalesPrice")));
        Money? purchasePrice = reader.IsDBNull(reader.GetOrdinal("PurchasePrice")) ? null : Money.FromMinorUnits(reader.GetInt64(reader.GetOrdinal("PurchasePrice")));
        Money? mrp = reader.IsDBNull(reader.GetOrdinal("MRP")) ? null : Money.FromMinorUnits(reader.GetInt64(reader.GetOrdinal("MRP")));
        Money? minSalePrice = reader.IsDBNull(reader.GetOrdinal("MinSalePrice")) ? null : Money.FromMinorUnits(reader.GetInt64(reader.GetOrdinal("MinSalePrice")));
        decimal? discountPercent = reader.IsDBNull(reader.GetOrdinal("DiscountPercent")) ? null : Convert.ToDecimal(reader.GetDouble(reader.GetOrdinal("DiscountPercent")));

        decimal? minLevel = reader.IsDBNull(reader.GetOrdinal("MinLevel")) ? null : Convert.ToDecimal(reader.GetDouble(reader.GetOrdinal("MinLevel")));
        decimal? maxLevel = reader.IsDBNull(reader.GetOrdinal("MaxLevel")) ? null : Convert.ToDecimal(reader.GetDouble(reader.GetOrdinal("MaxLevel")));
        decimal? reorderLevel = reader.IsDBNull(reader.GetOrdinal("ReorderLevel")) ? null : Convert.ToDecimal(reader.GetDouble(reader.GetOrdinal("ReorderLevel")));

        var description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description"));
        var barcode = reader.IsDBNull(reader.GetOrdinal("Barcode")) ? null : reader.GetString(reader.GetOrdinal("Barcode"));
        var isActive = reader.GetInt32(reader.GetOrdinal("IsActive")) == 1;

        var item = new Item(
            id,
            companyId,
            itemGroupId,
            unitId,
            name,
            printName,
            code,
            altUnitId,
            conversionFactor,
            openingStockQuantity,
            openingStockRate,
            openingStockValue,
            salesPrice,
            purchasePrice,
            mrp,
            minSalePrice,
            discountPercent,
            minLevel,
            maxLevel,
            reorderLevel,
            description,
            barcode,
            isActive);

        item.CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")), null, System.Globalization.DateTimeStyles.RoundtripKind);
        item.CreatedBy = reader.GetString(reader.GetOrdinal("CreatedBy"));

        if (!reader.IsDBNull(reader.GetOrdinal("ModifiedAt")))
        {
            item.ModifiedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("ModifiedAt")), null, System.Globalization.DateTimeStyles.RoundtripKind);
            item.ModifiedBy = reader.IsDBNull(reader.GetOrdinal("ModifiedBy")) ? null : reader.GetString(reader.GetOrdinal("ModifiedBy"));
        }

        return item;
    }
}
