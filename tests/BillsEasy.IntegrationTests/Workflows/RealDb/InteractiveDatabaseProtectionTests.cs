using System;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.IntegrationTests.Workflows.Simulation;
using FluentAssertions;
using Xunit;

namespace BillsEasy.IntegrationTests.Workflows.RealDb;

public class InteractiveDatabaseProtectionTests
{
    // 1. Bare filenames (Interactive E2E DB and Normal User DB)
    [Theory]
    [InlineData("BillsEasy_RealDB_E2E.db")]
    [InlineData("BILLSEASY_REALDB_E2E.DB")]
    [InlineData("billseasy.db")]
    [InlineData("BILLSEASY.DB")]
    public void AssertNotProtectedDatabase_WhenTargetIsProtectedBareFileName_ThrowsInvalidOperationException(string bareFileName)
    {
        var action = () => InteractiveDatabaseGuard.AssertNotProtectedDatabase(bareFileName);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*strictly prohibited from deleting, resetting, reseeding, or mutating*");
    }

    // 2. Canonical absolute paths for both protected databases
    [Fact]
    public void AssertNotProtectedDatabase_WhenTargetIsCanonicalProtectedInteractivePath_ThrowsInvalidOperationException()
    {
        var action = () => InteractiveDatabaseGuard.AssertNotProtectedDatabase(InteractiveDatabaseGuard.ProtectedInteractiveDbFilePath);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*protected interactive E2E database*");
    }

    [Fact]
    public void AssertNotProtectedDatabase_WhenTargetIsCanonicalProtectedUserDbPath_ThrowsInvalidOperationException()
    {
        var expectedDynamicUserDbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BillsEasy",
            "billseasy.db");

        InteractiveDatabaseGuard.ProtectedUserDbFilePath.Should().BeEquivalentTo(expectedDynamicUserDbPath);

        var action = () => InteractiveDatabaseGuard.AssertNotProtectedDatabase(expectedDynamicUserDbPath);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*protected normal user database*");
    }

    // 3, 4, 5. Forward slashes, lowercase characters, and '.' / '..' traversal segments
    [Theory]
    [InlineData("W:/BillsEasy/TestData/E2E/BillsEasy_RealDB_E2E.db")]
    [InlineData(@"w:\billseasy\testdata\e2e\billseasy_realdb_e2e.db")]
    [InlineData("w:/billseasy/testdata/e2e/billseasy_realdb_e2e.db")]
    [InlineData(@"W:\BillsEasy\TestData\.\E2E\..\E2E\BillsEasy_RealDB_E2E.db")]
    [InlineData(@"W:\BillsEasy\src\..\TestData\E2E\.\BillsEasy_RealDB_E2E.db")]
    [InlineData("W:/BillsEasy/TestData/../TestData/./E2E/BillsEasy_RealDB_E2E.db")]
    [InlineData(@"W:\BillsEasy\TestData\E2E\BillsEasy_RealDB_E2E.db\")]
    [InlineData("file:///W:/BillsEasy/TestData/E2E/BillsEasy_RealDB_E2E.db")]
    public void AssertNotProtectedDatabase_WhenInteractiveDbPathUsesSlashesCaseOrDotSegments_ThrowsInvalidOperationException(string pathVariant)
    {
        var action = () => InteractiveDatabaseGuard.AssertNotProtectedDatabase(pathVariant);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*protected interactive E2E database*");
    }

    [Fact]
    public void AssertNotProtectedDatabase_WhenUserDbPathUsesSlashesCaseOrDotSegments_ThrowsInvalidOperationException()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var variants = new[]
        {
            Path.Combine(localAppData, "BillsEasy", "billseasy.db").Replace('\\', '/'),
            Path.Combine(localAppData, "BillsEasy", "billseasy.db").ToLowerInvariant(),
            Path.Combine(localAppData, "BillsEasy", ".", "..", "BillsEasy", "billseasy.db"),
            $"\"{Path.Combine(localAppData, "BillsEasy", "billseasy.db")}\""
        };

        foreach (var variant in variants)
        {
            var action = () => InteractiveDatabaseGuard.AssertNotProtectedDatabase(variant);
            action.Should().Throw<InvalidOperationException>($"variant '{variant}' must be rejected as the protected user database")
                .WithMessage("*protected normal user database*");
        }
    }

    // 6, 7, 8, 9. Connection string variations (Data Source first, Data Source after other keys, DataSource, Filename, whitespace, quotes)
    [Theory]
    [InlineData(@"Data Source=W:\BillsEasy\TestData\E2E\BillsEasy_RealDB_E2E.db;Mode=ReadWrite")]
    [InlineData(@"Mode=ReadWriteCreate;Cache=Shared;Data Source=W:\BillsEasy\TestData\E2E\BillsEasy_RealDB_E2E.db")]
    [InlineData(@"Pooling=True;Data Source=w:/billseasy/testdata/e2e/./BillsEasy_RealDB_E2E.db;Mode=ReadWrite")]
    [InlineData(@"DataSource=W:\BillsEasy\TestData\E2E\BillsEasy_RealDB_E2E.db;Mode=ReadWrite")]
    [InlineData(@"Mode=ReadWriteCreate;DataSource=W:\BillsEasy\TestData\E2E\BillsEasy_RealDB_E2E.db")]
    [InlineData(@"Filename=W:\BillsEasy\TestData\E2E\BillsEasy_RealDB_E2E.db;Mode=ReadWrite")]
    [InlineData(@"Cache=Shared;Filename=W:\BillsEasy\TestData\E2E\BillsEasy_RealDB_E2E.db;Mode=ReadWriteCreate")]
    [InlineData(@"Data Source = W:\BillsEasy\TestData\E2E\BillsEasy_RealDB_E2E.db ; Mode = ReadWrite")]
    [InlineData("Data Source=\"W:\\BillsEasy\\TestData\\E2E\\BillsEasy_RealDB_E2E.db\";Mode=ReadWrite")]
    [InlineData("Mode=ReadWriteCreate;Data Source='W:\\BillsEasy\\TestData\\E2E\\BillsEasy_RealDB_E2E.db';Cache=Shared")]
    public void AssertNotProtectedDatabase_WhenConnectionStringTargetsInteractiveDb_ThrowsInvalidOperationException(string connectionString)
    {
        var action = () => InteractiveDatabaseGuard.AssertNotProtectedDatabase(connectionString);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*protected interactive E2E database*");
    }

    [Fact]
    public void AssertNotProtectedDatabase_WhenConnectionStringTargetsProtectedUserDb_ThrowsInvalidOperationException()
    {
        var userDb = InteractiveDatabaseGuard.ProtectedUserDbFilePath;
        var connectionStrings = new[]
        {
            $"Data Source={userDb};Mode=ReadWrite",
            $"Mode=ReadWriteCreate;Cache=Shared;Data Source={userDb}",
            $"DataSource={userDb};Pooling=True",
            $"Mode=ReadWrite;Filename={userDb}",
            $"Data Source=\"{userDb}\";Mode=ReadWrite"
        };

        foreach (var cs in connectionStrings)
        {
            var action = () => InteractiveDatabaseGuard.AssertNotProtectedDatabase(cs);
            action.Should().Throw<InvalidOperationException>($"connection string '{cs}' must be rejected")
                .WithMessage("*protected normal user database*");
        }
    }

    // 10. Malformed connection strings and ambiguous destructive-operation inputs fail closed
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Data Source=")]
    [InlineData("Data Source=   ;Mode=ReadWrite")]
    [InlineData("Mode=ReadWrite;Cache=Shared")]
    [InlineData("InvalidKeyword=Foo;Data Source=W:\\BillsEasy\\TestData\\E2E\\SomeTest.db")]
    [InlineData("Data Source=\"W:\\BillsEasy\\TestData\\E2E\\UnclosedQuote.db")]
    [InlineData("NotAValidConnectionString;AlsoInvalid")]
    [InlineData(":memory:")]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=Test.db;Mode=Memory")]
    [InlineData("W:BillsEasy_RealDB_E2E.db")]
    [InlineData("C:billseasy.db")]
    [InlineData(@"W:\BillsEasy\TestData\E2E\*.db")]
    [InlineData(@"W:\BillsEasy\TestData\E2E\BillsEasy_RealDB_?.db")]
    [InlineData("file:BillsEasy_RealDB_E2E.db?mode=rwc")]
    [InlineData("Data Source=file:W:/BillsEasy/TestData/E2E/BillsEasy_RealDB_E2E.db?cache=shared")]
    public void ResolveDatabaseFilePath_And_CleanTestDatabaseFiles_WhenInputIsMalformedOrAmbiguous_FailsClosed(string? invalidInput)
    {
        var resolveAction = () => InteractiveDatabaseGuard.ResolveDatabaseFilePath(invalidInput);
        resolveAction.Should().Throw<InvalidOperationException>();

        var guardAction = () => InteractiveDatabaseGuard.AssertNotProtectedDatabase(invalidInput);
        guardAction.Should().Throw<InvalidOperationException>();

        var cleanAction = () => InteractiveDatabaseGuard.CleanTestDatabaseFiles(invalidInput);
        cleanAction.Should().Throw<InvalidOperationException>();
    }

    // 12. Cleanup attempts targeting either protected database
    [Fact]
    public void CleanTestDatabaseFiles_WhenTargetIsProtectedInteractiveDatabase_ThrowsAndDoesNotDeleteFile()
    {
        var variants = new[]
        {
            InteractiveDatabaseGuard.ProtectedInteractiveDbFilePath,
            "BillsEasy_RealDB_E2E.db",
            @"W:\BillsEasy\TestData\.\E2E\BillsEasy_RealDB_E2E.db",
            $"Mode=ReadWrite;Data Source={InteractiveDatabaseGuard.ProtectedInteractiveDbFilePath}"
        };

        foreach (var target in variants)
        {
            var action = () => InteractiveDatabaseGuard.CleanTestDatabaseFiles(target);
            action.Should().Throw<InvalidOperationException>();
        }

        File.Exists(InteractiveDatabaseGuard.ProtectedInteractiveDbFilePath).Should().BeTrue(
            "Protected interactive database must never be deleted by test cleanup");
    }

    [Fact]
    public void CleanTestDatabaseFiles_WhenTargetIsProtectedUserDatabase_ThrowsAndDoesNotDeleteFile()
    {
        var variants = new[]
        {
            InteractiveDatabaseGuard.ProtectedUserDbFilePath,
            "billseasy.db",
            $"Mode=ReadWriteCreate;Data Source={InteractiveDatabaseGuard.ProtectedUserDbFilePath}"
        };

        foreach (var target in variants)
        {
            var action = () => InteractiveDatabaseGuard.CleanTestDatabaseFiles(target);
            action.Should().Throw<InvalidOperationException>();
        }

        if (File.Exists(InteractiveDatabaseGuard.ProtectedUserDbFilePath))
        {
            File.Exists(InteractiveDatabaseGuard.ProtectedUserDbFilePath).Should().BeTrue(
                "Protected normal user database must never be deleted by test cleanup");
        }
    }

    // 13. Direct simulation-runner invocation targeting either protected database
    [Theory]
    [InlineData(@"Data Source=W:\BillsEasy\TestData\E2E\BillsEasy_RealDB_E2E.db")]
    [InlineData(@"Mode=ReadWriteCreate;Data Source=W:\BillsEasy\TestData\E2E\BillsEasy_RealDB_E2E.db;Cache=Shared")]
    [InlineData(@"Filename=w:/billseasy/testdata/e2e/./BillsEasy_RealDB_E2E.db")]
    public void Class01to08SimulationRunner_WhenConstructedWithProtectedInteractiveDatabase_ThrowsImmediately(string protectedConnectionString)
    {
        var factory = new SqliteConnectionFactory(protectedConnectionString);

        var action = () => new Class01to08SimulationRunner(factory);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*protected interactive E2E database*");
    }

    [Fact]
    public void Class01to08SimulationRunner_WhenConstructedWithProtectedUserDatabase_ThrowsImmediately()
    {
        var factory = new SqliteConnectionFactory(
            $"Mode=ReadWriteCreate;Data Source={InteractiveDatabaseGuard.ProtectedUserDbFilePath}");

        var action = () => new Class01to08SimulationRunner(factory);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*protected normal user database*");
    }

    private sealed class MutableTestConnectionFactory : ISqliteDbConnectionFactory
    {
        public string ConnectionString { get; set; }

        public MutableTestConnectionFactory(string connectionString)
        {
            ConnectionString = connectionString;
        }

        public DbConnection CreateConnection() =>
            throw new InvalidOperationException("Connection must never be opened when guard rejects target.");

        public Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Connection must never be opened when guard rejects target.");
    }

    private sealed class UnverifiableConnectionFactory : ISqliteDbConnectionFactory
    {
        public DbConnection CreateConnection() => throw new NotSupportedException();
        public Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Class01to08SimulationRunner_WhenFactoryTargetMutatedBeforeInitializeOrStage_ThrowsBeforeOpeningConnection()
    {
        var safeTempDb = Path.Combine(Path.GetTempPath(), $"BillsEasy_RunnerGuard_{Guid.NewGuid():N}.db");
        var mutableFactory = new MutableTestConnectionFactory($"Data Source={safeTempDb}");

        var runner = new Class01to08SimulationRunner(mutableFactory);

        // Mutate connection string after construction to point to protected interactive DB
        mutableFactory.ConnectionString = $"Mode=ReadWrite;Data Source={InteractiveDatabaseGuard.ProtectedInteractiveDbFilePath}";

        var initAction = () => runner.InitializeDatabaseAsync();
        await initAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*protected interactive E2E database*");

        var stageAction = () => runner.RunStage01_CompanyAndFinancialYearAsync();
        await stageAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*protected interactive E2E database*");

        // Unverifiable factory must also fail closed
        var unverifiableAction = () => new Class01to08SimulationRunner(new UnverifiableConnectionFactory());
        unverifiableAction.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot verify database target safety*");
    }

    // 14. Valid test-owned simulation and temporary database paths
    [Fact]
    public void AssertNotProtectedDatabase_WhenTargetIsDedicatedSimulationOrTempDatabase_Succeeds()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_ValidTemp_{Guid.NewGuid():N}.db");

        var simAction = () => InteractiveDatabaseGuard.AssertNotProtectedDatabase(RealDbBusinessSimulationTests.SimulationDbFilePath);
        simAction.Should().NotThrow();

        var simConnStrAction = () => InteractiveDatabaseGuard.AssertNotProtectedDatabase(
            $"Mode=ReadWriteCreate;Data Source={RealDbBusinessSimulationTests.SimulationDbFilePath};Cache=Shared");
        simConnStrAction.Should().NotThrow();

        var tempAction = () => InteractiveDatabaseGuard.AssertNotProtectedDatabase(tempDbPath);
        tempAction.Should().NotThrow();

        var tempConnStrAction = () => InteractiveDatabaseGuard.AssertNotProtectedDatabase($"Data Source=\"{tempDbPath}\"");
        tempConnStrAction.Should().NotThrow();
    }

    [Fact]
    public void CleanTestDatabaseFiles_WhenTargetIsIsolatedTempDatabase_DeletesOnlyOwnedDbWalAndShmFiles()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_CleanTest_{Guid.NewGuid():N}.db");
        var walPath = tempDbPath + "-wal";
        var shmPath = tempDbPath + "-shm";
        var unrelatedSiblingPath = tempDbPath + ".bak";

        try
        {
            File.WriteAllText(tempDbPath, "dummy-db");
            File.WriteAllText(walPath, "dummy-wal");
            File.WriteAllText(shmPath, "dummy-shm");
            File.WriteAllText(unrelatedSiblingPath, "keep-me");

            InteractiveDatabaseGuard.CleanTestDatabaseFiles($"Mode=ReadWrite;Data Source={tempDbPath}");

            File.Exists(tempDbPath).Should().BeFalse("owned .db file should be deleted");
            File.Exists(walPath).Should().BeFalse("owned -wal file should be deleted");
            File.Exists(shmPath).Should().BeFalse("owned -shm file should be deleted");
            File.Exists(unrelatedSiblingPath).Should().BeTrue("unrelated sibling file must not be deleted");
        }
        finally
        {
            if (File.Exists(tempDbPath)) File.Delete(tempDbPath);
            if (File.Exists(walPath)) File.Delete(walPath);
            if (File.Exists(shmPath)) File.Delete(shmPath);
            if (File.Exists(unrelatedSiblingPath)) File.Delete(unrelatedSiblingPath);
        }
    }

    // 15. Cleanup restrictions for a similarly named but unapproved directory and arbitrary paths
    [Fact]
    public void CleanTestDatabaseFiles_WhenTargetIsInSimilarlyNamedUnapprovedDirectoryOrOutsideScope_ThrowsInvalidOperationException()
    {
        var canonicalTempDirNoSlash = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var siblingTempPrefixDir = canonicalTempDirNoSlash + "_Unapproved";
        var siblingPath = Path.Combine(siblingTempPrefixDir, "BillsEasy_Test.db");
        var traversalOutOfTempPath = Path.Combine(canonicalTempDirNoSlash, "..", "Temp_Unapproved", "BillsEasy_Test.db");
        var unapprovedE2EDirFile = Path.Combine(InteractiveDatabaseGuard.ProtectedInteractiveDbDirectory, "OtherDatabaseFile.db");
        var repoRootFile = @"W:\BillsEasy\UnexpectedTest.db";

        var rejectedPaths = new[]
        {
            siblingPath,
            traversalOutOfTempPath,
            unapprovedE2EDirFile,
            repoRootFile
        };

        foreach (var path in rejectedPaths)
        {
            var action = () => InteractiveDatabaseGuard.CleanTestDatabaseFiles(path);
            action.Should().Throw<InvalidOperationException>($"path '{path}' outside allowed cleanup scope must be rejected")
                .WithMessage("*outside the allowed test-owned database scope*");
        }
    }

    [Fact]
    public void AutomatedRealDbTests_TargetSimulationDatabase_NotEitherProtectedDatabase()
    {
        RealDbBusinessSimulationTests.SimulationDbFilePath.Should().NotBe(InteractiveDatabaseGuard.ProtectedInteractiveDbFilePath);
        RealDbBusinessSimulationTests.SimulationDbFilePath.Should().NotBe(InteractiveDatabaseGuard.ProtectedUserDbFilePath);
        RealDbBusinessSimulationTests.SimulationDbFilePath.Should().EndWith("BillsEasy_RealDB_Simulation_Run.db");
    }
}
