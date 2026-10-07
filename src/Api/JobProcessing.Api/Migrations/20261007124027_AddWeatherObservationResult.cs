using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobProcessing.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWeatherObservationResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AirTemperatureCelsius",
                table: "Jobs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WeatherObservedAtUtc",
                table: "Jobs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AirTemperatureCelsius",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "WeatherObservedAtUtc",
                table: "Jobs");
        }
    }
}
