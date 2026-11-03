using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MS.Vid_dldr.Manager.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftwareVersionTable_ExtractInfo_GraphPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Analysed",
                table: "Downloads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DownloadStatusDuringPeriods",
                columns: table => new
                {
                    requestedon = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NONE = table.Column<int>(type: "integer", nullable: false),
                    REQUESTED = table.Column<int>(type: "integer", nullable: false),
                    QUEUED = table.Column<int>(type: "integer", nullable: false),
                    ANALYSIS = table.Column<int>(type: "integer", nullable: false),
                    DOWNLOADING = table.Column<int>(type: "integer", nullable: false),
                    FINISHED = table.Column<int>(type: "integer", nullable: false),
                    DISTRIBUTED_LOOKUP = table.Column<int>(type: "integer", nullable: false),
                    DISTRIBUTED = table.Column<int>(type: "integer", nullable: false),
                    DELETED = table.Column<int>(type: "integer", nullable: false),
                    ERRORED = table.Column<int>(type: "integer", nullable: false),
                    RETRY = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "ExtractInfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    RequestedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedUrl = table.Column<string>(type: "text", nullable: false),
                    InfoType = table.Column<string>(type: "text", nullable: false),
                    Info = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    Logged = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Response = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    DownloadSessionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExtractInfos_DownloadSessions_DownloadSessionId",
                        column: x => x.DownloadSessionId,
                        principalTable: "DownloadSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SoftwareVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    ManagerApp = table.Column<string>(type: "text", nullable: false),
                    ManagerFramework = table.Column<string>(type: "text", nullable: false),
                    ManagerDb = table.Column<string>(type: "text", nullable: false),
                    ManagerDbLastMigration = table.Column<string>(type: "text", nullable: false),
                    DownloaderApp = table.Column<string>(type: "text", nullable: false),
                    DownloaderFramework = table.Column<string>(type: "text", nullable: false),
                    DownloaderYtdlp = table.Column<string>(type: "text", nullable: false),
                    UpdaterApp = table.Column<string>(type: "text", nullable: false),
                    UpdaterFramework = table.Column<string>(type: "text", nullable: false),
                    Requested = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    YtdlpGit = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftwareVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractInfos_DownloadSessionId",
                table: "ExtractInfos",
                column: "DownloadSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DownloadStatusDuringPeriods");

            migrationBuilder.DropTable(
                name: "ExtractInfos");

            migrationBuilder.DropTable(
                name: "SoftwareVersions");

            migrationBuilder.DropColumn(
                name: "Analysed",
                table: "Downloads");
        }
    }
}
