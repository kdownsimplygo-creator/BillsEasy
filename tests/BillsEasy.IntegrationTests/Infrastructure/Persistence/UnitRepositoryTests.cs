using System;
using System.IO;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BillsEasy.IntegrationTests.Infrastructure.Persistence;

public class UnitRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly CompanyRepository _companyRepository;
    private readonly UnitRepository _unitRepository;

    public UnitRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_UnitTest_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _initializer = new DatabaseInitializer(_connectionFactory);
        _companyRepository = new CompanyRepository(_connectionFactory);
        _unitRepository = new UnitRepository(_connectionFactory);
    }

    private async Task<Company> CreateTestCompanyAsync(string name = "Test Company")
    {
        var company = Company.Create(
            companyName: name,
            printName: name,
            shortName: "TC",
            stateName: "Gujarat",
            stateCode: "24",
            financialYearBeginning: new DateOnly(2024, 4, 1),
            booksBeginningFrom: new DateOnly(2024, 4, 1));

        company.CreatedAt = DateTime.UtcNow;
        company.CreatedBy = "tester";

        await _companyRepository.AddAsync(company);
        return company;
    }

    [Fact]
    public async Task AddAndGetById_PersistsUnitSuccessfully()
    {
        await _initializer.InitializeAsync();
        var company = await CreateTestCompanyAsync();

        var unit = Unit.Create(company.Id, "Pcs", "Pieces", 0);
        unit.CreatedAt = DateTime.UtcNow;
        unit.CreatedBy = "tester";

        await _unitRepository.AddAsync(unit);

        var retrieved = await _unitRepository.GetByIdAsync(company.Id, unit.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(unit.Id);
        retrieved.CompanyId.Should().Be(company.Id);
        retrieved.Name.Should().Be("Pcs");
        retrieved.PrintName.Should().Be("Pieces");
        retrieved.DecimalPlaces.Should().Be(0);
    }

    [Fact]
    public async Task Update_PersistsModifiedProperties()
    {
        await _initializer.InitializeAsync();
        var company = await CreateTestCompanyAsync();

        var unit = Unit.Create(company.Id, "Box", "Box", 0);
        unit.CreatedAt = DateTime.UtcNow;
        unit.CreatedBy = "tester";
        await _unitRepository.AddAsync(unit);

        unit.Update("Box", "Boxes", 2);
        unit.ModifiedAt = DateTime.UtcNow;
        unit.ModifiedBy = "updater";
        await _unitRepository.UpdateAsync(unit);

        var retrieved = await _unitRepository.GetByIdAsync(company.Id, unit.Id);

        retrieved.Should().NotBeNull();
        retrieved!.PrintName.Should().Be("Boxes");
        retrieved.DecimalPlaces.Should().Be(2);
        retrieved.ModifiedBy.Should().Be("updater");
    }

    [Fact]
    public async Task Delete_RemovesUnit()
    {
        await _initializer.InitializeAsync();
        var company = await CreateTestCompanyAsync();

        var unit = Unit.Create(company.Id, "Kg", "Kilograms", 3);
        unit.CreatedAt = DateTime.UtcNow;
        unit.CreatedBy = "tester";
        await _unitRepository.AddAsync(unit);

        await _unitRepository.DeleteAsync(company.Id, unit.Id);

        var retrieved = await _unitRepository.GetByIdAsync(company.Id, unit.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task Add_DuplicateNameInSameCompany_ThrowsSqliteException()
    {
        await _initializer.InitializeAsync();
        var company = await CreateTestCompanyAsync();

        var unit1 = Unit.Create(company.Id, "Pcs", "Pieces", 0);
        unit1.CreatedAt = DateTime.UtcNow;
        unit1.CreatedBy = "tester";
        await _unitRepository.AddAsync(unit1);

        var unit2 = Unit.Create(company.Id, "pcs", "Pieces 2", 0);
        unit2.CreatedAt = DateTime.UtcNow;
        unit2.CreatedBy = "tester";

        var act = async () => await _unitRepository.AddAsync(unit2);
        await act.Should().ThrowAsync<SqliteException>();
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }
}
