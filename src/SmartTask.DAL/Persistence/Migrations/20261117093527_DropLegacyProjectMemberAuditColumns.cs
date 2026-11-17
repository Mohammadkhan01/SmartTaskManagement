using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTask.DAL.Persistence.Migrations
{
    /// <summary>
    /// InitialCreate added CreatedAtUtc/UpdatedAtUtc to ProjectMembers, but the
    /// model (and snapshot) dropped them without a migration. Fresh databases
    /// therefore rejected every membership insert. Columns are dropped only if
    /// present so databases that were already repaired manually are unaffected.
    /// </summary>
    public partial class DropLegacyProjectMemberAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.ProjectMembers', 'CreatedAtUtc') IS NOT NULL
                    ALTER TABLE [dbo].[ProjectMembers] DROP COLUMN [CreatedAtUtc];

                IF COL_LENGTH('dbo.ProjectMembers', 'UpdatedAtUtc') IS NOT NULL
                    ALTER TABLE [dbo].[ProjectMembers] DROP COLUMN [UpdatedAtUtc];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
