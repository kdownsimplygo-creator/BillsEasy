using System;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillsEasy.Presentation.Wpf.ViewModels;

public partial class UnitEditViewModel : ViewModelBase
{
    private readonly IUnitService _unitService;
    private readonly Guid _companyId;
    private readonly UnitDto? _existingUnit;

    [ObservableProperty]
    private string _dialogTitle = "Add Unit of Measurement";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _printName = string.Empty;

    [ObservableProperty]
    private int _decimalPlaces = 0;

    [ObservableProperty]
    private string? _errorMessage;

    public UnitDto? SavedUnit { get; private set; }
    public bool IsSaved { get; private set; }

    public event Action? RequestClose;

    public UnitEditViewModel(
        IUnitService unitService,
        Guid companyId,
        UnitDto? existingUnit = null)
    {
        _unitService = unitService;
        _companyId = companyId;
        _existingUnit = existingUnit;

        if (_existingUnit != null)
        {
            DialogTitle = $"Modify Unit — {_existingUnit.Name}";
            Name = _existingUnit.Name;
            PrintName = _existingUnit.PrintName;
            DecimalPlaces = _existingUnit.DecimalPlaces;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Unit name is required.";
            return;
        }

        if (DecimalPlaces < 0 || DecimalPlaces > 4)
        {
            ErrorMessage = "Decimal places must be between 0 and 4.";
            return;
        }

        try
        {
            if (_existingUnit == null)
            {
                var dto = new CreateUnitDto(
                    _companyId,
                    Name.Trim(),
                    string.IsNullOrWhiteSpace(PrintName) ? Name.Trim() : PrintName.Trim(),
                    DecimalPlaces);

                SavedUnit = await _unitService.CreateUnitAsync(dto);
            }
            else
            {
                var dto = new UpdateUnitDto(
                    _existingUnit.Id,
                    _companyId,
                    Name.Trim(),
                    string.IsNullOrWhiteSpace(PrintName) ? Name.Trim() : PrintName.Trim(),
                    DecimalPlaces);

                SavedUnit = await _unitService.UpdateUnitAsync(dto);
            }

            IsSaved = true;
            RequestClose?.Invoke();
        }
        catch (DomainException dex)
        {
            ErrorMessage = dex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving unit: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        IsSaved = false;
        RequestClose?.Invoke();
    }
}
