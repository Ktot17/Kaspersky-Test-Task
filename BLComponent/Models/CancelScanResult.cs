using BLComponent.Enums;

namespace BLComponent.Models;

public sealed record CancelScanResult(
    CancelScanOutcome Outcome,
    Scan? Scan);
