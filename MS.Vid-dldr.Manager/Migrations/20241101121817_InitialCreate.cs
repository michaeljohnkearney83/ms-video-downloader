using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MS.Vid_dldr.Manager.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Disabled = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DownloadSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Fid = table.Column<string>(type: "text", nullable: false),
                    ClientName = table.Column<string>(type: "text", nullable: false),
                    DeploymentNo = table.Column<string>(type: "text", nullable: false),
                    RequestedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivity = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Disabled = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Status",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Disabled = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Downloads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedUrl = table.Column<string>(type: "text", nullable: false),
                    RequestedDownloadFolder = table.Column<string>(type: "text", nullable: true),
                    RequestedDownloadName = table.Column<string>(type: "text", nullable: false),
                    RequestedDownloadExt = table.Column<string>(type: "text", nullable: false),
                    VideoId = table.Column<string>(type: "text", nullable: true),
                    VideoDescription = table.Column<string>(type: "text", nullable: true),
                    VideoTitle = table.Column<string>(type: "text", nullable: true),
                    VideoDuration = table.Column<string>(type: "text", nullable: true),
                    VideoThumbnail = table.Column<string>(type: "text", nullable: true),
                    VideoUrl = table.Column<string>(type: "text", nullable: true),
                    VideoResolution = table.Column<string>(type: "text", nullable: true),
                    VideoAspecRatio = table.Column<string>(type: "text", nullable: true),
                    VideoFileSizeApprox = table.Column<string>(type: "text", nullable: true),
                    LastActivity = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Filename = table.Column<string>(type: "text", nullable: true),
                    ProgressEta = table.Column<string>(type: "text", nullable: true),
                    ProgressSpeed = table.Column<string>(type: "text", nullable: true),
                    ProgressPercent = table.Column<string>(type: "text", nullable: true),
                    ProgressElapsed = table.Column<string>(type: "text", nullable: true),
                    ProgressDefaultTemplate = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    RetryOnErroredAttemptNo = table.Column<int>(type: "integer", nullable: false),
                    RetryOnErroredMaxCount = table.Column<int>(type: "integer", nullable: false),
                    DownloadSessionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Downloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Downloads_DownloadSessions_DownloadSessionId",
                        column: x => x.DownloadSessionId,
                        principalTable: "DownloadSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Downloads_DownloadSessionId",
                table: "Downloads",
                column: "DownloadSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Downloads");

            migrationBuilder.DropTable(
                name: "Status");

            migrationBuilder.DropTable(
                name: "DownloadSessions");
        }
    }
}
