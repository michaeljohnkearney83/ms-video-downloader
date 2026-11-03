using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MS.Vid_dldr.Manager.Entities;

public class SoftwareVersion
{
    [Key]
    [Required]
    public int Id { get; set; }
    [Required]
    public string ManagerApp { get; set; }
    [Required]
    public string ManagerFramework { get; set; }
    [Required]
    public string ManagerDb { get; set; }
    [Required]
    public string ManagerDbLastMigration { get; set; }
    [Required]
    public string DownloaderApp { get; set; }
    [Required]
    public string DownloaderFramework { get; set; }
    [Required]
    public string DownloaderYtdlp { get; set; }
    [Required]
    public string UpdaterApp { get; set; }
    [Required]
    public string UpdaterFramework { get; set; }
    [Required]
    public DateTime Requested { get; set; }
    public string? YtdlpGit { get; set; }
}