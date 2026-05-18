using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AgoraAgentBackend.Data;

#nullable disable

namespace AgoraAgentBackend.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260514_UpdateTradingTransaction_AddStatus")]
    public partial class UpdateTradingTransaction_AddStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "TradingTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "TradingTransactions",
                type: "varchar(1000)",
                nullable: true);
            
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TradingTransactions",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "TradingTransactions");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "TradingTransactions");
        }
    }
}
