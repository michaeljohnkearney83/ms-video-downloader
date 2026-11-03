using Microsoft.EntityFrameworkCore;
using MS.Vid_dldr.Manager.Entities;
using MS.Vid_dldr.Manager.Entities.Scripts;

namespace MS.Vid_dldr.Manager.Data;

//dotnet ef migrations add InitialCreate
//dotnet ef database update  <-- unless auto migration run on program.cs
//etc etc...
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseSerialColumns();
    }
    public DbSet<DownloadSession> DownloadSessions { get; set; }
    public DbSet<Download> Downloads { get; set; }
    public DbSet<ExtractInfo> ExtractInfos { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<Status> Status { get; set; }
    public DbSet<SoftwareVersion> SoftwareVersions { get; set; }

    public virtual DbSet<DownloadStatusDuringPeriod> DownloadStatusDuringPeriods { get; set; }
}