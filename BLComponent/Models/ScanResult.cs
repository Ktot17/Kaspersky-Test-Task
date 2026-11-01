namespace BLComponent.Models;

public sealed record ScanResult(
    IReadOnlyList<ScanFinding> Findings);
    