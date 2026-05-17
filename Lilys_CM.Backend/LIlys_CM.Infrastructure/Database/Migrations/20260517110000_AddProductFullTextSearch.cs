using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Lilys_CM.Infrastructure.Database;

#nullable disable

namespace Lilys_CM.Infrastructure.Database.Migrations
{
    [DbContext(typeof(DatabaseContext))]
    [Migration("20260517110000_AddProductFullTextSearch")]
    public partial class AddProductFullTextSearch : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Full-Text Search migration placeholder.
-- Local SQL Server does not have Full-Text Search installed.
-- Search still works through backend fallback search in GetProductsQueryHandler.
SELECT 1;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- No-op rollback for local Full-Text placeholder migration.
SELECT 1;
");
        }
    }
}