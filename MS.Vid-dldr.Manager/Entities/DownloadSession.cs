using System.ComponentModel.DataAnnotations;

namespace MS.Vid_dldr.Manager.Entities;

public class DownloadSession
{
    [Key]
    [Required]
    public Guid Id { get; set; }
    [Required]
    public required string Username { get; set; }
    [Required]
    public required string Fid { get; set; }
    [Required]
    public required string ClientName { get; set; }
    [Required]
    public required string DeploymentNo { get; set; }
    [Required]
    public required DateTime RequestedOn { get; set; }
    [Required]
    public required DateTime LastActivity { get; set; }
    public DateTime? Disabled { get; set; } = null;
}