using System.ComponentModel.DataAnnotations;

namespace WebComponent.Models;

public sealed record StartScanRequest(
    [Required, MaxLength(200)] string RepositoryUrl);
    