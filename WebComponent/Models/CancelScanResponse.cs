namespace WebComponent.Models;

public sealed record CancelScanResponse(
    Guid Id,
    bool Success,
    DateTimeOffset? CancelledAt,
    string? Reason);
    