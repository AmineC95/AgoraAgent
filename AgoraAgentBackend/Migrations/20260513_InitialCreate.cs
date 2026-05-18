using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AgoraAgentBackend.Data;

#nullable disable

namespace AgoraAgentBackend.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260513_InitialCreate")]
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", nullable: false),
                    WalletAddress = table.Column<string>(type: "varchar(100)", nullable: false),
                    BondBalance = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    CurrentStatus = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradingTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    AgentId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TxHash = table.Column<string>(type: "varchar(66)", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    PriceAtTrade = table.Column<decimal>(type: "decimal(18,8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingTransactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradingTransactions_AgentId",
                table: "TradingTransactions",
                column: "AgentId");

            migrationBuilder.CreateTable(
                name: "UserFollowers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    WalletAddress = table.Column<string>(type: "varchar(100)", nullable: false),
                    AgentId = table.Column<Guid>(type: "char(36)", nullable: false),
                    AmountStaked = table.Column<decimal>(type: "decimal(18,8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFollowers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserFollowers_AgentId",
                table: "UserFollowers",
                column: "AgentId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserFollowers");

            migrationBuilder.DropTable(
                name: "TradingTransactions");

            migrationBuilder.DropTable(
                name: "Agents");
        }
    }
}
