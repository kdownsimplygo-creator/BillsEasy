using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BillsEasy.IntegrationTests.Infrastructure.Persistence;

public class FinancialYearRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly CompanyRepository _companyRepository;
    private readonly FinancialYearRepository _financialYearRepository;

    public FinancialYearRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_FyTest_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _initializer = new DatabaseInitializer(_connectionFactory);
        _companyRepository = new CompanyRepository(_connectionFactory);
        _financialYearRepository = new FinancialYearRepository(_connectionFactory);
    }

    [Fact]
    public async Task SetActive_SwitchesActiveFinancialYearAtomically()
    {
        await _initializer.InitializeAsync();

        var company = Company.Create(
            "FY Switch Test Co",
            "FY Switch Test Co",
            null,
            "Gujarat",
            "24",
            new DateOnly(2024, 4, 1),
            new DateOnly(2024, 4, 1));

        var fy1 = company.FinancialYears.First();

        var fy2 = FinancialYear.Create(
            company.Id,
            new DateOnly(2025, 4, 1),
            new DateOnly(2026, 3, 31),
            "2025-2026",
            isActive: false);

        await _companyRepository.AddAsync(company);
        await _financialYearRepository.AddAsync(fy2);

        // Switch active year to FY2
        await _financialYearRepository.SetActiveAsync(company.Id, fy2.Id);

        var activeFy = await _financialYearRepository.GetActiveFinancialYearAsync(company.Id);
        activeFy.Should().NotBeNull();
        activeFy!.Id.Should().Be(fy2.Id);
        activeFy.YearName.Should().Be("2025-2026");

        var allFys = await _financialYearRepository.GetByCompanyIdAsync(company.Id);
        allFys.Should().HaveCount(2);
        allFys.First(f => f.Id == fy1.Id).IsActive.Should().BeFalse();
        allFys.First(f => f.Id == fy2.Id).IsActive.Should().BeTrue();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
            var wal = _testDbPath + "-wal";
            if (File.Exists(wal)) File.Delete(wal);
            var shm = _testDbPath + "-shm";
            if (File.Exists(shm)) File.Delete(shm);
        }
        catch { }
    }
}
