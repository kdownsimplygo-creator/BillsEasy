using System;
using System.IO;
using System.Reflection;
using BillsEasy.Application.Common.Interfaces;
using Microsoft.Data.Sqlite;

namespace BillsEasy.IntegrationTests.Workflows.RealDb;

/// <summary>
/// Guard ensuring automated tests never delete, reset, reseed, overwrite, migrate, or mutate
/// either the protected interactive E2E database or the protected normal user database.
/// </summary>
public static class InteractiveDatabaseGuard
{
    public const string ProtectedInteractiveDbFileName = "BillsEasy_RealDB_E2E.db";
    public static readonly string ProtectedInteractiveDbDirectory = @"W:\BillsEasy\TestData\E2E";
    public static readonly string ProtectedInteractiveDbFilePath =
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(ProtectedInteractiveDbDirectory, ProtectedInteractiveDbFileName)));

    public const string ProtectedUserDbFileName = "billseasy.db";
    public static readonly string ProtectedUserDbFilePath =
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BillsEasy",
            ProtectedUserDbFileName)));

    public const string AllowedSimulationDbFileName = "BillsEasy_RealDB_Simulation_Run.db";
    public static readonly string AllowedSimulationDbFilePath =
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(ProtectedInteractiveDbDirectory, AllowedSimulationDbFileName)));

    /// <summary>
    /// Resolves a database file path or SQLite connection string to a canonical, normalized absolute Windows file path.
    /// Relative paths are resolved against <see cref="Environment.CurrentDirectory"/> (matching <see cref="SqliteConnection"/> semantics),
    /// while drive-relative paths, wildcards, malformed connection strings, in-memory targets, and ambiguous URIs fail closed.
    /// </summary>
    /// <param name="dbPathOrConnectionString">A file path or SQLite connection string.</param>
    /// <returns>The normalized absolute file path with trailing directory separators removed.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the input is null, empty, malformed, or ambiguous.</exception>
    public static string ResolveDatabaseFilePath(string? dbPathOrConnectionString)
    {
        if (string.IsNullOrWhiteSpace(dbPathOrConnectionString))
        {
            throw new InvalidOperationException("Database path or connection string cannot be null or whitespace.");
        }

        var raw = dbPathOrConnectionString.Trim();
        string candidatePath;

        if (raw.Contains('=') || raw.Contains(';'))
        {
            SqliteConnectionStringBuilder builder;
            try
            {
                builder = new SqliteConnectionStringBuilder(raw);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Malformed or unsupported SQLite connection string '{dbPathOrConnectionString}': {ex.Message}", ex);
            }

            if (builder.Mode == SqliteOpenMode.Memory ||
                string.Equals(builder.DataSource?.Trim(), ":memory:", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "In-memory SQLite connection strings do not resolve to a filesystem database path.");
            }

            candidatePath = builder.DataSource?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(candidatePath))
            {
                throw new InvalidOperationException(
                    $"SQLite connection string '{dbPathOrConnectionString}' does not specify a non-empty Data Source.");
            }
        }
        else
        {
            if (raw.Length >= 2 &&
                ((raw.StartsWith('"') && raw.EndsWith('"')) || (raw.StartsWith('\'') && raw.EndsWith('\''))))
            {
                candidatePath = raw[1..^1].Trim();
            }
            else
            {
                candidatePath = raw;
            }

            if (string.IsNullOrWhiteSpace(candidatePath) ||
                candidatePath.Contains('"') ||
                candidatePath.Contains('\''))
            {
                throw new InvalidOperationException(
                    $"Malformed or improperly quoted database path '{dbPathOrConnectionString}'.");
            }

            if (string.Equals(candidatePath, ":memory:", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "In-memory SQLite ':memory:' does not resolve to a filesystem database path.");
            }
        }

        // Only canonical local file:/// URIs without query/fragment parameters are accepted; all other file: URIs fail closed.
        if (candidatePath.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            if (candidatePath.StartsWith("file:///", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(candidatePath, UriKind.Absolute, out var fileUri) &&
                fileUri.IsFile &&
                !fileUri.IsUnc &&
                string.IsNullOrEmpty(fileUri.Query) &&
                string.IsNullOrEmpty(fileUri.Fragment))
            {
                candidatePath = fileUri.LocalPath;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Ambiguous or unsupported SQLite 'file:' URI '{candidatePath}' is rejected by InteractiveDatabaseGuard.");
            }
        }

        // Reject Windows drive-relative paths such as "W:BillsEasy_RealDB_E2E.db"
        if (candidatePath.Length >= 2 &&
            char.IsLetter(candidatePath[0]) &&
            candidatePath[1] == ':' &&
            (candidatePath.Length == 2 || (candidatePath[2] != '\\' && candidatePath[2] != '/')))
        {
            throw new InvalidOperationException(
                $"Ambiguous drive-relative database path '{candidatePath}' is rejected by InteractiveDatabaseGuard.");
        }

        if (candidatePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new InvalidOperationException(
                $"Database path '{candidatePath}' contains invalid path characters.");
        }

        string normalizedFullPath;
        try
        {
            normalizedFullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidatePath));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to normalize database path '{candidatePath}': {ex.Message}", ex);
        }

        var fileName = Path.GetFileName(normalizedFullPath);
        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException(
                $"Database path '{candidatePath}' does not resolve to a valid file name.");
        }

        return normalizedFullPath;
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> if the specified path or connection string resolves to
    /// either the protected interactive E2E database or the protected normal user database, or is malformed/ambiguous.
    /// </summary>
    /// <param name="dbPathOrConnectionString">Database file path or SQLite connection string to validate.</param>
    public static void AssertNotProtectedDatabase(string? dbPathOrConnectionString)
    {
        var fullPath = ResolveDatabaseFilePath(dbPathOrConnectionString);
        var fileName = Path.GetFileName(fullPath);

        if (string.Equals(fullPath, ProtectedInteractiveDbFilePath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, ProtectedInteractiveDbFileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Automated test execution is strictly prohibited from deleting, resetting, reseeding, or mutating " +
                $"the protected interactive E2E database: '{fullPath}'. " +
                $"Automated simulation tests must use dedicated test storage (e.g. '{AllowedSimulationDbFileName}').");
        }

        if (string.Equals(fullPath, ProtectedUserDbFilePath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, ProtectedUserDbFileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Automated test execution is strictly prohibited from deleting, resetting, reseeding, or mutating " +
                $"the protected normal user database: '{fullPath}'. " +
                $"Automated tests must use isolated temporary or simulation test storage.");
        }
    }

    /// <summary>
    /// Inspects the configured connection string on an <see cref="ISqliteDbConnectionFactory"/> without opening a connection,
    /// and throws an <see cref="InvalidOperationException"/> if it targets a protected database or cannot be verified.
    /// </summary>
    /// <param name="connectionFactory">The connection factory to validate.</param>
    public static void AssertNotProtectedDatabase(ISqliteDbConnectionFactory? connectionFactory)
    {
        if (connectionFactory is null)
        {
            throw new InvalidOperationException("Database connection factory cannot be null.");
        }

        var type = connectionFactory.GetType();
        var field = type.GetField("_connectionString", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field?.GetValue(connectionFactory) is string connStrFromField)
        {
            AssertNotProtectedDatabase(connStrFromField);
            return;
        }

        var prop = type.GetProperty("ConnectionString", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (prop?.GetValue(connectionFactory) is string connStrFromProp)
        {
            AssertNotProtectedDatabase(connStrFromProp);
            return;
        }

        throw new InvalidOperationException(
            $"Cannot verify database target safety for connection factory of type '{type.FullName}'.");
    }

    /// <summary>
    /// Safely deletes a test-owned database file and its WAL / SHM auxiliary files.
    /// Fails immediately if the target is either protected database or lies outside the allowed test-owned locations
    /// (<see cref="Path.GetTempPath()"/> or <see cref="AllowedSimulationDbFilePath"/>).
    /// </summary>
    /// <param name="testDbPathOrConnectionString">Path or connection string to the test-owned SQLite database.</param>
    public static void CleanTestDatabaseFiles(string? testDbPathOrConnectionString)
    {
        var resolvedPath = ResolveDatabaseFilePath(testDbPathOrConnectionString);
        AssertNotProtectedDatabase(resolvedPath);

        if (!resolvedPath.EndsWith(".db", StringComparison.OrdinalIgnoreCase) || Directory.Exists(resolvedPath))
        {
            throw new InvalidOperationException(
                $"Cleanup rejected: '{resolvedPath}' is not a valid SQLite '.db' file path.");
        }

        var canonicalTempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))
                                + Path.DirectorySeparatorChar;

        bool isUnderTempRoot = resolvedPath.StartsWith(canonicalTempRoot, StringComparison.OrdinalIgnoreCase) &&
                               resolvedPath.Length > canonicalTempRoot.Length;

        bool isDedicatedSimulationDb = string.Equals(
            resolvedPath,
            AllowedSimulationDbFilePath,
            StringComparison.OrdinalIgnoreCase);

        if (!isUnderTempRoot && !isDedicatedSimulationDb)
        {
            throw new InvalidOperationException(
                $"Cleanup rejected: '{resolvedPath}' is outside the allowed test-owned database scope " +
                $"('{canonicalTempRoot}' or '{AllowedSimulationDbFilePath}').");
        }

        SqliteConnection.ClearAllPools();

        var files = new[]
        {
            resolvedPath,
            resolvedPath + "-wal",
            resolvedPath + "-shm"
        };

        foreach (var file in files)
        {
            if (File.Exists(file))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to clean test database file '{file}': {ex.Message}", ex);
                }
            }
        }
    }
}
