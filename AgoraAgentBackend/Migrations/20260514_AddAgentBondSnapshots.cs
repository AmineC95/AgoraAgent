using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AgoraAgentBackend.Data;

#nullable disable

namespace AgoraAgentBackend.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260514_AddAgentBondSnapshots")]
    public partial class AddAgentBondSnapshots : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentBondSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    AgentId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    SnapshotAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentBondSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentBondSnapshots_AgentId",
                table: "AgentBondSnapshots",
                column: "AgentId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentBondSnapshots");
        }
    }
}
