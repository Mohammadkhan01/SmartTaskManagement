
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTask.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyProjectMemberId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove the unique index before dropping the obsolete column.
            migrationBuilder.DropIndex(
                name: "IX_ProjectMembers_Id",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ProjectMembers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the legacy column with newly generated IDs.
            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "ProjectMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [dbo].[ProjectMembers] SET [Id] = NEWID() WHERE [Id] IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ProjectMembers",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_Id",
                table: "ProjectMembers",
                column: "Id",
                unique: true);
        }
    }
}
