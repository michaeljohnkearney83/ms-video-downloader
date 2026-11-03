using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MS.Vid_dldr.Manager.Entities;

public class Download
{
    [Key]
    [Required]
    public required int Id { get; set; }
    [Required]
    public required Guid ClientId { get; set; }
    [Required]
    public required DateTime RequestedOn { get; set; }
    [Required]
    public required string RequestedUrl { get; set; }
    public string? RequestedDownloadFolder { get; set; }
    [Required]
    public required string RequestedDownloadName { get; set; }
    [Required]
    public required string RequestedDownloadExt { get; set; }
    [NotMapped]
    public required int RequestedRetryCount { get; set; }
    [NotMapped]
    public required int RequestedTimeoutSeconds { get; set; }
    [NotMapped]
    public required int RequestedDownloadStalledTimeoutSeconds { get; set; }
    public DateTime? Analysed { get; set; }
    public string? VideoId { get; set; }
    public string? VideoDescription { get; set; }
    public string? VideoTitle { get; set; }
    public string? VideoDuration { get; set; }
    public string? VideoThumbnail { get; set; }
    public string? VideoUrl { get; set; }
    public string? VideoResolution { get; set; }
    public string? VideoAspecRatio { get; set; }
    public string? VideoFileSizeApprox { get; set; }
    [Required]
    public required DateTime LastActivity { get; set; }
    [Required]
    public required string Status { get; set; }
    public string? Filename { get; set; }
    public string? ProgressEta { get; set; }
    public string? ProgressSpeed { get; set; }
    public string? ProgressPercent { get; set; }
    public string? ProgressElapsed { get; set; }
    public string? ProgressDefaultTemplate { get; set; }
    public string? ErrorMessage { get; set; }
    [Required]
    public required int RetryOnErroredAttemptNo { get; set; }
    [Required]
    public required int RetryOnErroredMaxCount { get; set; }

    [ForeignKey("DownloadSessions")]
    public required Guid DownloadSessionId { get; set; }
    public required virtual DownloadSession? DownloadSession { get; set; }
}