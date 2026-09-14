namespace BillsEasy.Application.Common.Interfaces;

/// <summary>
/// Service contract to supply user attribution and workstation identity context.
/// </summary>
public interface ICurrentUserService
{
    string UserId { get; }
    string UserName { get; }
    string WorkstationName { get; }
}
