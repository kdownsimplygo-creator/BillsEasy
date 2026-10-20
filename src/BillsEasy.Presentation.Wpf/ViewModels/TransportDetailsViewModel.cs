using System;
using System.Windows;
using BillsEasy.Application.DTOs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillsEasy.Presentation.Wpf.ViewModels;

public partial class TransportDetailsViewModel : ViewModelBase
{
    [ObservableProperty]
    private string? _transportName;

    [ObservableProperty]
    private string? _grRrNumber;

    [ObservableProperty]
    private DateTime? _transportDate = DateTime.Today;

    [ObservableProperty]
    private string? _vehicleNumber;

    [ObservableProperty]
    private string? _stationFromPlace;

    public bool? DialogResult { get; private set; }
    public TransportDetailsDto? Details { get; private set; }

    [RelayCommand]
    private void Save(Window? window)
    {
        DateOnly? tDate = TransportDate.HasValue ? DateOnly.FromDateTime(TransportDate.Value) : null;
        Details = new TransportDetailsDto(
            string.IsNullOrWhiteSpace(TransportName) ? null : TransportName.Trim(),
            string.IsNullOrWhiteSpace(GrRrNumber) ? null : GrRrNumber.Trim(),
            tDate,
            string.IsNullOrWhiteSpace(VehicleNumber) ? null : VehicleNumber.Trim(),
            string.IsNullOrWhiteSpace(StationFromPlace) ? null : StationFromPlace.Trim());

        DialogResult = true;
        if (window != null)
            window.DialogResult = true;
    }

    [RelayCommand]
    private void Skip(Window? window)
    {
        Details = null;
        DialogResult = true;
        if (window != null)
            window.DialogResult = true;
    }

    [RelayCommand]
    private void Cancel(Window? window)
    {
        Details = null;
        DialogResult = false;
        if (window != null)
            window.DialogResult = false;
    }
}
