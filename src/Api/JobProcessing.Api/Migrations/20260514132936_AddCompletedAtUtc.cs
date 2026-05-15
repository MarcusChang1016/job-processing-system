using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobProcessing.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "Jobs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "Jobs");
        }
    }
}
