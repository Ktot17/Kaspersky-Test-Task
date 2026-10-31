using BLComponent.Enums;

namespace BLComponent.Models;

public sealed record ScanFinding(
    string Name,
    SeverityLevel Severity);
    