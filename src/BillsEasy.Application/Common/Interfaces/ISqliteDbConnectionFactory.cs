using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace BillsEasy.Application.Common.Interfaces;

/// <summary>
/// Factory interface for obtaining configured SQLite database connections.
/// </summary>
public interface ISqliteDbConnectionFactory
{
    /// <summary>
    /// Creates and opens a SQLite connection configured with WAL mode and foreign keys enabled.
    /// </summary>
    DbConnection CreateConnection();

    /// <summary>
    /// Asynchronously creates and opens a SQLite connection configured with WAL mode and foreign keys enabled.
    /// </summary>
    Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
