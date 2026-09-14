using System;
using BillsEasy.Application.Common.Interfaces;

namespace BillsEasy.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Now => DateTime.Now;
}

public class StandaloneCurrentUserService : ICurrentUserService
{
    public string UserId => Environment.UserName.ToLowerInvariant();
    public string UserName => Environment.UserName;
    public string WorkstationName => Environment.MachineName;
}
