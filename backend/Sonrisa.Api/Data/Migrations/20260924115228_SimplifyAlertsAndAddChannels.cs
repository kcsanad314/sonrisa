using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sonrisa.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyAlertsAndAddChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertChannels",
                columns: table => new
                {
                    AlertId = table.Column<int>(type: "INTEGER", nullable: false),
                    Channel = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertChannels", x => new { x.AlertId, x.Channel });
                    table.ForeignKey(
                        name: "FK_AlertChannels_Alerts_AlertId",
                        column: x => x.AlertId,
                        principalTable: "Alerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("INSERT INTO \"AlertChannels\" (\"AlertId\", \"Channel\") SELECT \"Id\", 'Email' FROM \"Alerts\" WHERE \"EmailEnabled\" = 1;");
            migrationBuilder.Sql("INSERT INTO \"AlertChannels\" (\"AlertId\", \"Channel\") SELECT \"Id\", 'Slack' FROM \"Alerts\" WHERE \"SlackEnabled\" = 1;");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "EmailEnabled",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "SlackEnabled",
                table: "Alerts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Alerts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailEnabled",
                table: "Alerts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SlackEnabled",
                table: "Alerts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE \"Alerts\" SET \"EmailEnabled\" = 1 WHERE \"Id\" IN (SELECT \"AlertId\" FROM \"AlertChannels\" WHERE \"Channel\" = 'Email');");
            migrationBuilder.Sql("UPDATE \"Alerts\" SET \"SlackEnabled\" = 1 WHERE \"Id\" IN (SELECT \"AlertId\" FROM \"AlertChannels\" WHERE \"Channel\" = 'Slack');");

            migrationBuilder.DropTable(
                name: "AlertChannels");
        }
    }
}
