using System;

namespace BillsEasy.Application.Common.Interfaces;

/// <summary>
/// Provider abstraction for system date and time to guarantee testability.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
}
