using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MS.Vid_dldr.Manager.Entities.Scripts;

[Keyless]
public class DownloadStatusDuringPeriod
{
    public required DateTime requestedon { get; set; }
    public required int NONE { get; set; }
    public required int REQUESTED { get; set; }
    public required int QUEUED { get; set; }
    public required int ANALYSIS { get; set; }
    public required int DOWNLOADING { get; set; }
    public required int FINISHED { get; set; }
    public required int DISTRIBUTED_LOOKUP { get; set; }
    public required int DISTRIBUTED { get; set; }
    public required int DELETED { get; set; }
    public required int ERRORED { get; set; }
    public required int RETRY { get; set; }
}