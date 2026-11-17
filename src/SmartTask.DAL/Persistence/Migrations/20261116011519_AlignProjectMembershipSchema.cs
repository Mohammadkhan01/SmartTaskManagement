using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTask.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignProjectMembershipSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_AspNetUsers_OwnerId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OwnerId",
                table: "Projects");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProjectMembers",
                table: "ProjectMembers");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_Id",
                table: "ProjectMembers",
                column: "Id",
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_ProjectMembers_ProjectId_UserId",
                table: "ProjectMembers");

            migrationBuilder.DropIndex(
                name: "IX_ProjectMembers_UserId",
                table: "ProjectMembers");

            migrationBuilder.RenameColumn(
                name: "OwnerId",
                table: "Projects",
                newName: "CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_OwnerId_Status",
                table: "Projects",
                newName: "IX_Projects_CreatedByUserId_Status");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "ProjectMembers",
                newName: "ProjectRole");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProjectMembers",
                table: "ProjectMembers",
                columns: new[] { "ProjectId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status_CreatedAtUtc",
                table: "Projects",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Projects_Status",
                table: "Projects",
                sql: "[Status] IN (1, 2, 3, 4, 5)");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_UserId_ProjectId",
                table: "ProjectMembers",
                columns: new[] { "UserId", "ProjectId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectMembers_ProjectRole",
                table: "ProjectMembers",
                sql: "[ProjectRole] IN (1, 2)");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_AspNetUsers_CreatedByUserId",
                table: "Projects",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_AspNetUsers_CreatedByUserId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Status_CreatedAtUtc",
                table: "Projects");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Projects_Status",
                table: "Projects");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProjectMembers",
                table: "ProjectMembers");

            migrationBuilder.DropIndex(
                name: "IX_ProjectMembers_Id",
                table: "ProjectMembers");

            migrationBuilder.DropIndex(
                name: "IX_ProjectMembers_UserId_ProjectId",
                table: "ProjectMembers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectMembers_ProjectRole",
                table: "ProjectMembers");

            migrationBuilder.RenameColumn(
                name: "CreatedByUserId",
                table: "Projects",
                newName: "OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_CreatedByUserId_Status",
                table: "Projects",
                newName: "IX_Projects_OwnerId_Status");

            migrationBuilder.RenameColumn(
                name: "ProjectRole",
                table: "ProjectMembers",
                newName: "Role");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProjectMembers",
                table: "ProjectMembers",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerId",
                table: "Projects",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_ProjectId_UserId",
                table: "ProjectMembers",
                columns: new[] { "ProjectId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_UserId",
                table: "ProjectMembers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_AspNetUsers_OwnerId",
                table: "Projects",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
