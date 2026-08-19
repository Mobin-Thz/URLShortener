using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using URLShortener.Infrastructure.Context;

#nullable disable

namespace URLShortener.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260819090000_AddAnalytics")]
public partial class AddAnalytics : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Code",
            table: "ShortenedUrls",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(6)",
            oldMaxLength: 6);

        migrationBuilder.AddColumn<DateTime>(
            name: "ExpiresAt",
            table: "ShortenedUrls",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsActive",
            table: "ShortenedUrls",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastAccessedAt",
            table: "ShortenedUrls",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "TotalClicks",
            table: "ShortenedUrls",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "ClickEvents",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ShortenedUrlId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClickedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                VisitorHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                ReferrerHost = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                Browser = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                DeviceType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClickEvents", item => item.Id);
                table.ForeignKey(
                    name: "FK_ClickEvents_ShortenedUrls_ShortenedUrlId",
                    column: item => item.ShortenedUrlId,
                    principalTable: "ShortenedUrls",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ClickEvents_ShortenedUrlId_ClickedAt",
            table: "ClickEvents",
            columns: new[] { "ShortenedUrlId", "ClickedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ClickEvents_VisitorHash",
            table: "ClickEvents",
            column: "VisitorHash");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ClickEvents");

        migrationBuilder.DropColumn(name: "ExpiresAt", table: "ShortenedUrls");
        migrationBuilder.DropColumn(name: "IsActive", table: "ShortenedUrls");
        migrationBuilder.DropColumn(name: "LastAccessedAt", table: "ShortenedUrls");
        migrationBuilder.DropColumn(name: "TotalClicks", table: "ShortenedUrls");

        migrationBuilder.AlterColumn<string>(
            name: "Code",
            table: "ShortenedUrls",
            type: "nvarchar(6)",
            maxLength: 6,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(32)",
            oldMaxLength: 32);
    }
}
