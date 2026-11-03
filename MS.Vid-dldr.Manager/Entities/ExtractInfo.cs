using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace MS.Vid_dldr.Manager.Entities;

public class ExtractInfo
{
    [Key]
    [Required]
    public required int Id { get; set; }
    [Required]
    public required DateTime RequestedOn { get; set; }
    [Required]
    public required string RequestedUrl { get; set; }
    public required string InfoType { get; set; }
    [Column(TypeName = "jsonb")]
    public JsonDocument? Info { get; set; }
    public required DateTime? Logged { get; set; }
    public required DateTime? Response { get; set; }
    public string? ErrorMessage { get; set; }

    [ForeignKey("DownloadSessions")]
    public required Guid DownloadSessionId { get; set; }
    public required virtual DownloadSession? DownloadSession { get; set; }
}