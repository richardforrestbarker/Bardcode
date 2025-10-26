using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bardcoded.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddBarcodeDataProvided : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Barcodes_Bard",
                table: "Barcodes",
                column: "Bard");

            migrationBuilder.CreateTable(
                name: "BarcodeDataProvided",
                columns: table => new
                {
                    Bard = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProviderJson = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BarcodeDataProvided", x => x.Bard);
                    table.ForeignKey(
                        name: "FK_BarcodeDataProvided_Barcodes_Bard",
                        column: x => x.Bard,
                        principalTable: "Barcodes",
                        principalColumn: "Bard",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BarcodeDataProvided");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Barcodes_Bard",
                table: "Barcodes");
        }
    }
}
