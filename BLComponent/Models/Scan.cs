using BLComponent.Enums;

namespace BLComponent.Models;

public sealed record Scan(
    Guid Id,
    Uri RepositoryUrl,
    ScanStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUpdatedAt,
    ScanResult? Result,
    string? ErrorMessage);
