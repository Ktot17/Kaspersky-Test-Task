using BLComponent.Enums;
using BLComponent.Models;

namespace WebComponent.Models;

public sealed record GetScanResponse(
    Guid Id,
    ScanStatus Status,
    DateTimeOffset LastUpdatedAt,
    ScanResult? Result,
    string? ErrorMessage);
    