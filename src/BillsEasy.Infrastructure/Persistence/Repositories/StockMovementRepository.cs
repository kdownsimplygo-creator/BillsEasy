using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using BillsEasy.Infrastructure.Common;
using Microsoft.Data.Sqlite;

namespace BillsEasy.Infrastructure.Persistence.Repositories;

public class StockMovementRepository : IStockMovementRepository
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;

    public StockMovementRepository(ISqliteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task AddRangeAsync(IEnumerable<StockMovement> movements, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        var movementList = movements.ToList();
        if (movementList.Count == 0)
            return;

        if (transaction != null)
        {
            await InsertMovementsInternalAsync(transaction.Connection!, transaction, movementList, cancellationToken);
        }
        else
        {
            await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
            await using var localTx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await InsertMovementsInternalAsync(connection, localTx, movementList, cancellationToken);
                await localTx.CommitAsync(cancellationToken);
            }
            catch
            {
                await localTx.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    public async Task<IReadOnlyList<StockMovement>> GetByItemIdAsync(
        Guid companyId,
        Guid itemId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var query = @"
            SELECT Id, CompanyId, ItemId, MovementDate, MovementType,
                   SourceDocumentType, SourceDocumentId, Quantity, UnitId,
                   Rate, TotalValue, MaterialCentre, IsActive,
                   CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM StockMovements
            WHERE CompanyId = @CompanyId AND ItemId = @ItemId AND IsActive = 1
        ";

        if (fromDate.HasValue)
            query += " AND MovementDate >= @FromDate";

        if (toDate.HasValue)
            query += " AND MovementDate <= @ToDate";

        query += " ORDER BY MovementDate ASC, CreatedAt ASC;";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@ItemId", itemId.ToString());

        if (fromDate.HasValue)
            cmd.Parameters.AddWithValue("@FromDate", fromDate.Value.ToString("yyyy-MM-dd"));

        if (toDate.HasValue)
            cmd.Parameters.AddWithValue("@ToDate", toDate.Value.ToString("yyyy-MM-dd"));

        var list = new List<StockMovement>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapMovementFromReader(reader));
        }

        return list;
    }

    public async Task<decimal> GetCurrentQuantityAsync(
        Guid companyId,
        Guid itemId,
        string materialCentre = "Main Store",
        CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        var query = @"
            SELECT MovementType, Quantity
            FROM StockMovements
            WHERE CompanyId = @CompanyId AND ItemId = @ItemId AND MaterialCentre = @MaterialCentre AND IsActive = 1;
        ";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@ItemId", itemId.ToString());
        cmd.Parameters.AddWithValue("@MaterialCentre", string.IsNullOrWhiteSpace(materialCentre) ? "Main Store" : materialCentre.Trim());

        decimal totalQty = 0;
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var movementType = (StockMovementType)reader.GetInt32(0);
            var qty = QuantityStorageHelper.FromStorageString(reader.GetString(1));

            if (movementType == StockMovementType.PurchaseInward || movementType == StockMovementType.OpeningStock)
            {
                totalQty += qty;
            }
            else if (movementType == StockMovementType.SalesOutward)
            {
                totalQty -= qty;
            }
        }

        return totalQty;
    }

    private static async Task InsertMovementsInternalAsync(DbConnection connection, DbTransaction transaction, List<StockMovement> movements, CancellationToken cancellationToken)
    {
        foreach (var movement in movements)
        {
            await using var cmd = (SqliteCommand)connection.CreateCommand();
            cmd.Transaction = (SqliteTransaction)transaction;
            cmd.CommandText = @"
                INSERT INTO StockMovements (
                    Id, CompanyId, ItemId, MovementDate, MovementType,
                    SourceDocumentType, SourceDocumentId, Quantity, UnitId,
                    Rate, TotalValue, MaterialCentre, IsActive,
                    CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
                ) VALUES (
                    @Id, @CompanyId, @ItemId, @MovementDate, @MovementType,
                    @SourceDocumentType, @SourceDocumentId, @Quantity, @UnitId,
                    @Rate, @TotalValue, @MaterialCentre, @IsActive,
                    @CreatedAt, @CreatedBy, @ModifiedAt, @ModifiedBy
                );
            ";

            cmd.Parameters.AddWithValue("@Id", movement.Id.ToString());
            cmd.Parameters.AddWithValue("@CompanyId", movement.CompanyId.ToString());
            cmd.Parameters.AddWithValue("@ItemId", movement.ItemId.ToString());
            cmd.Parameters.AddWithValue("@MovementDate", movement.MovementDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@MovementType", (int)movement.MovementType);
            cmd.Parameters.AddWithValue("@SourceDocumentType", movement.SourceDocumentType);
            cmd.Parameters.AddWithValue("@SourceDocumentId", movement.SourceDocumentId.ToString());
            cmd.Parameters.AddWithValue("@Quantity", QuantityStorageHelper.ToStorageString(movement.Quantity));
            cmd.Parameters.AddWithValue("@UnitId", movement.UnitId.ToString());
            cmd.Parameters.AddWithValue("@Rate", movement.Rate.ToMinorUnits());
            cmd.Parameters.AddWithValue("@TotalValue", movement.TotalValue.ToMinorUnits());
            cmd.Parameters.AddWithValue("@MaterialCentre", movement.MaterialCentre);
            cmd.Parameters.AddWithValue("@IsActive", movement.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@CreatedAt", movement.CreatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("@CreatedBy", movement.CreatedBy);
            cmd.Parameters.AddWithValue("@ModifiedAt", (object?)movement.ModifiedAt?.ToString("O") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ModifiedBy", (object?)movement.ModifiedBy ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static StockMovement MapMovementFromReader(DbDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var companyId = Guid.Parse(reader.GetString(1));
        var itemId = Guid.Parse(reader.GetString(2));
        var movementDate = DateOnly.Parse(reader.GetString(3));
        var movementType = (StockMovementType)reader.GetInt32(4);
        var srcType = reader.GetString(5);
        var srcId = Guid.Parse(reader.GetString(6));
        var qty = QuantityStorageHelper.FromStorageString(reader.GetString(7));
        var unitId = Guid.Parse(reader.GetString(8));
        var rate = Money.FromMinorUnits(reader.GetInt64(9));
        var materialCentre = reader.GetString(11);
        var isActive = reader.GetInt32(12) == 1;

        var movement = new StockMovement(
            id,
            companyId,
            itemId,
            movementDate,
            movementType,
            srcType,
            srcId,
            qty,
            unitId,
            rate,
            materialCentre,
            isActive)
        {
            CreatedAt = DateTime.Parse(reader.GetString(13)),
            CreatedBy = reader.GetString(14),
            ModifiedAt = reader.IsDBNull(15) ? null : DateTime.Parse(reader.GetString(15)),
            ModifiedBy = reader.IsDBNull(16) ? null : reader.GetString(16)
        };

        return movement;
    }
}
