using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.DTOs;

namespace BillsEasy.Application.Common.Interfaces;

public interface ILedgerService
{
    Task<LedgerDto> CreateLedgerAsync(CreateLedgerDto dto, CancellationToken cancellationToken = default);
    Task<LedgerDto> UpdateLedgerAsync(UpdateLedgerDto dto, CancellationToken cancellationToken = default);
    Task DeleteLedgerAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<LedgerDto?> GetLedgerByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LedgerDto>> GetAllLedgersAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LedgerDto>> GetLedgersByGroupAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LedgerDto>> SearchLedgersAsync(Guid companyId, string searchTerm, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LedgerLookupDto>> GetLookupLedgersAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task SeedDefaultLedgersAsync(Guid companyId, string createdBy, CancellationToken cancellationToken = default);
}