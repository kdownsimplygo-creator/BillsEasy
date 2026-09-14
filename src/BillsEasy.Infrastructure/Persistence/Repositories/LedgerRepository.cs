using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using Microsoft.Data.Sqlite;

namespace BillsEasy.Infrastructure.Persistence.Repositories;

public class LedgerRepository : ILedgerRepository
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;

    public LedgerRepository(ISqliteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Ledger?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, GroupId, Name, PrintName, Alias,
                   OpeningBalance, OpeningBalanceType, BillWiseEnabled, CreditPeriodDays, CreditLimit,
                   AddressLine1, AddressLine2, City, StateName, StateCode, PinCode, Country,
                   GSTIN, GSTRegistrationType, PAN, AadhaarNumber,
                   ContactPerson, MobileNumber, WhatsAppNumber, Email,
                   BankName, BankAccountNumber, BankIFSC, BankBranch,
                   IsDefaultSystemLedger, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Ledgers
            WHERE CompanyId = @CompanyId AND Id = @Id;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapFromReader(reader);
    }

    public async Task<Ledger?> GetByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, GroupId, Name, PrintName, Alias,
                   OpeningBalance, OpeningBalanceType, BillWiseEnabled, CreditPeriodDays, CreditLimit,
                   AddressLine1, AddressLine2, City, StateName, StateCode, PinCode, Country,
                   GSTIN, GSTRegistrationType, PAN, AadhaarNumber,
                   ContactPerson, MobileNumber, WhatsAppNumber, Email,
                   BankName, BankAccountNumber, BankIFSC, BankBranch,
                   IsDefaultSystemLedger, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Ledgers
            WHERE CompanyId = @CompanyId AND LOWER(Name) = LOWER(@Name);
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Name", name.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapFromReader(reader);
    }

    public async Task<IReadOnlyList<Ledger>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, GroupId, Name, PrintName, Alias,
                   OpeningBalance, OpeningBalanceType, BillWiseEnabled, CreditPeriodDays, CreditLimit,
                   AddressLine1, AddressLine2, City, StateName, StateCode, PinCode, Country,
                   GSTIN, GSTRegistrationType, PAN, AadhaarNumber,
                   ContactPerson, MobileNumber, WhatsAppNumber, Email,
                   BankName, BankAccountNumber, BankIFSC, BankBranch,
                   IsDefaultSystemLedger, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Ledgers
            WHERE CompanyId = @CompanyId
            ORDER BY Name ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());

        var list = new List<Ledger>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapFromReader(reader));
        }

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyList<Ledger>> GetByGroupIdAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, CompanyId, GroupId, Name, PrintName, Alias,
                   OpeningBalance, OpeningBalanceType, BillWiseEnabled, CreditPeriodDays, CreditLimit,
                   AddressLine1, AddressLine2, City, StateName, StateCode, PinCode, Country,
                   GSTIN, GSTRegistrationType, PAN, AadhaarNumber,
                   ContactPerson, MobileNumber, WhatsAppNumber, Email,
                   BankName, BankAccountNumber, BankIFSC, BankBranch,
                   IsDefaultSystemLedger, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            FROM Ledgers
            WHERE CompanyId = @CompanyId AND GroupId = @GroupId
            ORDER BY Name ASC;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@GroupId", groupId.ToString());

        var list = new List<Ledger>();
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
            SELECT COUNT(1) FROM Ledgers
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

    public async Task<bool> HasTransactionsAsync(Guid companyId, Guid ledgerId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='VoucherLines';";
            var tableCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken));
            if (tableCount == 0)
                return false;
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(1) FROM VoucherLines vl
            INNER JOIN Ledgers l ON vl.LedgerId = l.Id
            WHERE l.CompanyId = @CompanyId AND vl.LedgerId = @LedgerId;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@LedgerId", ledgerId.ToString());

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task<bool> HasBillReferencesAsync(Guid companyId, Guid ledgerId, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='BillReferences';";
            var tableCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(cancellationToken));
            if (tableCount == 0)
                return false;
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(1) FROM BillReferences
            WHERE CompanyId = @CompanyId AND LedgerId = @LedgerId;
        ";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@LedgerId", ledgerId.ToString());

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    public async Task AddAsync(Ledger ledger, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO Ledgers (
                Id, CompanyId, GroupId, Name, PrintName, Alias,
                OpeningBalance, OpeningBalanceType, BillWiseEnabled, CreditPeriodDays, CreditLimit,
                AddressLine1, AddressLine2, City, StateName, StateCode, PinCode, Country,
                GSTIN, GSTRegistrationType, PAN, AadhaarNumber,
                ContactPerson, MobileNumber, WhatsAppNumber, Email,
                BankName, BankAccountNumber, BankIFSC, BankBranch,
                IsDefaultSystemLedger, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
            ) VALUES (
                @Id, @CompanyId, @GroupId, @Name, @PrintName, @Alias,
                @OpeningBalance, @OpeningBalanceType, @BillWiseEnabled, @CreditPeriodDays, @CreditLimit,
                @AddressLine1, @AddressLine2, @City, @StateName, @StateCode, @PinCode, @Country,
                @GSTIN, @GSTRegistrationType, @PAN, @AadhaarNumber,
                @ContactPerson, @MobileNumber, @WhatsAppNumber, @Email,
                @BankName, @BankAccountNumber, @BankIFSC, @BankBranch,
                @IsDefaultSystemLedger, @CreatedAt, @CreatedBy, @ModifiedAt, @ModifiedBy
            );
        ";

        AddParameters(cmd, ledger);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<Ledger> ledgers, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        foreach (var ledger in ledgers)
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT INTO Ledgers (
                    Id, CompanyId, GroupId, Name, PrintName, Alias,
                    OpeningBalance, OpeningBalanceType, BillWiseEnabled, CreditPeriodDays, CreditLimit,
                    AddressLine1, AddressLine2, City, StateName, StateCode, PinCode, Country,
                    GSTIN, GSTRegistrationType, PAN, AadhaarNumber,
                    ContactPerson, MobileNumber, WhatsAppNumber, Email,
                    BankName, BankAccountNumber, BankIFSC, BankBranch,
                    IsDefaultSystemLedger, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
                ) VALUES (
                    @Id, @CompanyId, @GroupId, @Name, @PrintName, @Alias,
                    @OpeningBalance, @OpeningBalanceType, @BillWiseEnabled, @CreditPeriodDays, @CreditLimit,
                    @AddressLine1, @AddressLine2, @City, @StateName, @StateCode, @PinCode, @Country,
                    @GSTIN, @GSTRegistrationType, @PAN, @AadhaarNumber,
                    @ContactPerson, @MobileNumber, @WhatsAppNumber, @Email,
                    @BankName, @BankAccountNumber, @BankIFSC, @BankBranch,
                    @IsDefaultSystemLedger, @CreatedAt, @CreatedBy, @ModifiedAt, @ModifiedBy
                );
            ";

            AddParameters(cmd, ledger);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateAsync(Ledger ledger, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
            UPDATE Ledgers SET
                GroupId = @GroupId,
                Name = @Name,
                PrintName = @PrintName,
                Alias = @Alias,
                OpeningBalance = @OpeningBalance,
                OpeningBalanceType = @OpeningBalanceType,
                BillWiseEnabled = @BillWiseEnabled,
                CreditPeriodDays = @CreditPeriodDays,
                CreditLimit = @CreditLimit,
                AddressLine1 = @AddressLine1,
                AddressLine2 = @AddressLine2,
                City = @City,
                StateName = @StateName,
                StateCode = @StateCode,
                PinCode = @PinCode,
                Country = @Country,
                GSTIN = @GSTIN,
                GSTRegistrationType = @GSTRegistrationType,
                PAN = @PAN,
                AadhaarNumber = @AadhaarNumber,
                ContactPerson = @ContactPerson,
                MobileNumber = @MobileNumber,
                WhatsAppNumber = @WhatsAppNumber,
                Email = @Email,
                BankName = @BankName,
                BankAccountNumber = @BankAccountNumber,
                BankIFSC = @BankIFSC,
                BankBranch = @BankBranch,
                IsDefaultSystemLedger = @IsDefaultSystemLedger,
                ModifiedAt = @ModifiedAt,
                ModifiedBy = @ModifiedBy
            WHERE CompanyId = @CompanyId AND Id = @Id;
        ";

        AddParameters(cmd, ledger);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "DELETE FROM Ledgers WHERE CompanyId = @CompanyId AND Id = @Id;";
        cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(SqliteCommand cmd, Ledger ledger)
    {
        cmd.Parameters.AddWithValue("@Id", ledger.Id.ToString());
        cmd.Parameters.AddWithValue("@CompanyId", ledger.CompanyId.ToString());
        cmd.Parameters.AddWithValue("@GroupId", ledger.GroupId.ToString());
        cmd.Parameters.AddWithValue("@Name", ledger.Name);
        cmd.Parameters.AddWithValue("@PrintName", ledger.PrintName);
        cmd.Parameters.AddWithValue("@Alias", (object?)ledger.Alias ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@OpeningBalance", ledger.OpeningBalance.ToMinorUnits());
        cmd.Parameters.AddWithValue("@OpeningBalanceType", (int)ledger.OpeningBalanceType);
        cmd.Parameters.AddWithValue("@BillWiseEnabled", ledger.BillWiseEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@CreditPeriodDays", (object?)ledger.CreditPeriodDays ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreditLimit", ledger.CreditLimit.HasValue ? (object)ledger.CreditLimit.Value.ToMinorUnits() : DBNull.Value);
        cmd.Parameters.AddWithValue("@AddressLine1", (object?)ledger.AddressLine1 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AddressLine2", (object?)ledger.AddressLine2 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@City", (object?)ledger.City ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StateName", (object?)ledger.StateName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StateCode", (object?)ledger.StateCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PinCode", (object?)ledger.PinCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Country", ledger.Country);
        cmd.Parameters.AddWithValue("@GSTIN", (object?)ledger.GSTIN ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@GSTRegistrationType", (int)ledger.GSTRegistrationType);
        cmd.Parameters.AddWithValue("@PAN", (object?)ledger.PAN ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AadhaarNumber", (object?)ledger.AadhaarNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ContactPerson", (object?)ledger.ContactPerson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MobileNumber", (object?)ledger.MobileNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@WhatsAppNumber", (object?)ledger.WhatsAppNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Email", (object?)ledger.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@BankName", (object?)ledger.BankName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@BankAccountNumber", (object?)ledger.BankAccountNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@BankIFSC", (object?)ledger.BankIFSC ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@BankBranch", (object?)ledger.BankBranch ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IsDefaultSystemLedger", ledger.IsDefaultSystemLedger ? 1 : 0);
        cmd.Parameters.AddWithValue("@CreatedAt", ledger.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@CreatedBy", ledger.CreatedBy);
        cmd.Parameters.AddWithValue("@ModifiedAt", ledger.ModifiedAt.HasValue ? (object)ledger.ModifiedAt.Value.ToString("o") : DBNull.Value);
        cmd.Parameters.AddWithValue("@ModifiedBy", (object?)ledger.ModifiedBy ?? DBNull.Value);
    }

    private static Ledger MapFromReader(SqliteDataReader reader)
    {
        var id = Guid.Parse(reader.GetString(0));
        var companyId = Guid.Parse(reader.GetString(1));
        var groupId = Guid.Parse(reader.GetString(2));
        var name = reader.GetString(3);
        var printName = reader.GetString(4);
        var alias = reader.IsDBNull(5) ? null : reader.GetString(5);
        var openingBalance = Money.FromMinorUnits(reader.GetInt64(6));
        var openingBalanceType = (DebitCreditType)reader.GetInt32(7);
        var billWiseEnabled = reader.GetInt32(8) == 1;
        int? creditPeriodDays = reader.IsDBNull(9) ? null : reader.GetInt32(9);
        Money? creditLimit = reader.IsDBNull(10) ? null : Money.FromMinorUnits(reader.GetInt64(10));
        var addressLine1 = reader.IsDBNull(11) ? null : reader.GetString(11);
        var addressLine2 = reader.IsDBNull(12) ? null : reader.GetString(12);
        var city = reader.IsDBNull(13) ? null : reader.GetString(13);
        var stateName = reader.IsDBNull(14) ? null : reader.GetString(14);
        var stateCode = reader.IsDBNull(15) ? null : reader.GetString(15);
        var pinCode = reader.IsDBNull(16) ? null : reader.GetString(16);
        var country = reader.GetString(17);
        var gstin = reader.IsDBNull(18) ? null : reader.GetString(18);
        var gstRegistrationType = (GSTRegistrationType)reader.GetInt32(19);
        var pan = reader.IsDBNull(20) ? null : reader.GetString(20);
        var aadhaarNumber = reader.IsDBNull(21) ? null : reader.GetString(21);
        var contactPerson = reader.IsDBNull(22) ? null : reader.GetString(22);
        var mobileNumber = reader.IsDBNull(23) ? null : reader.GetString(23);
        var whatsAppNumber = reader.IsDBNull(24) ? null : reader.GetString(24);
        var email = reader.IsDBNull(25) ? null : reader.GetString(25);
        var bankName = reader.IsDBNull(26) ? null : reader.GetString(26);
        var bankAccountNumber = reader.IsDBNull(27) ? null : reader.GetString(27);
        var bankIfsc = reader.IsDBNull(28) ? null : reader.GetString(28);
        var bankBranch = reader.IsDBNull(29) ? null : reader.GetString(29);
        var isDefaultSystemLedger = reader.GetInt32(30) == 1;

        return new Ledger(
            id, companyId, groupId, name, printName, alias,
            openingBalance, openingBalanceType, billWiseEnabled, creditPeriodDays, creditLimit,
            addressLine1, addressLine2, city, stateName, stateCode, pinCode, country,
            gstin, gstRegistrationType, pan, aadhaarNumber,
            contactPerson, mobileNumber, whatsAppNumber, email,
            bankName, bankAccountNumber, bankIfsc, bankBranch,
            isDefaultSystemLedger)
        {
            CreatedAt = DateTime.Parse(reader.GetString(31)),
            CreatedBy = reader.GetString(32),
            ModifiedAt = reader.IsDBNull(33) ? null : DateTime.Parse(reader.GetString(33)),
            ModifiedBy = reader.IsDBNull(34) ? null : reader.GetString(34)
        };
    }
}