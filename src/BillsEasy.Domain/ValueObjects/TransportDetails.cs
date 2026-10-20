using System;

namespace BillsEasy.Domain.ValueObjects;

/// <summary>
/// Value object capturing logistics and transport details prompted after purchase voucher save.
/// Class 8 observed fields: Transport, GR/RR No., Date, Vehicle No., Station/From Place.
/// </summary>
public sealed record TransportDetails
{
    public string? TransportName { get; init; }
    public string? GrRrNumber { get; init; }
    public DateOnly? TransportDate { get; init; }
    public string? VehicleNumber { get; init; }
    public string? StationFromPlace { get; init; }

    public TransportDetails(
        string? transportName = null,
        string? grRrNumber = null,
        DateOnly? transportDate = null,
        string? vehicleNumber = null,
        string? stationFromPlace = null)
    {
        TransportName = string.IsNullOrWhiteSpace(transportName) ? null : transportName.Trim();
        GrRrNumber = string.IsNullOrWhiteSpace(grRrNumber) ? null : grRrNumber.Trim();
        TransportDate = transportDate;
        VehicleNumber = string.IsNullOrWhiteSpace(vehicleNumber) ? null : vehicleNumber.Trim();
        StationFromPlace = string.IsNullOrWhiteSpace(stationFromPlace) ? null : stationFromPlace.Trim();
    }

    public static TransportDetails Empty => new();

    public bool IsEmpty =>
        string.IsNullOrEmpty(TransportName) &&
        string.IsNullOrEmpty(GrRrNumber) &&
        !TransportDate.HasValue &&
        string.IsNullOrEmpty(VehicleNumber) &&
        string.IsNullOrEmpty(StationFromPlace);
}
