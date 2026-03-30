using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CouponManagement.Shared.Migrations.CouponDefinitionUniqueCode
{
    /// <inheritdoc />
    public partial class Add_UK_CouponDefinitions_Code : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create unique index on CouponDefinitions.Code only if it doesn't already exist
            var sql = @"
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes i
    INNER JOIN sys.objects o ON i.object_id = o.object_id
    WHERE i.name = 'UK_CouponDefinitions_Code' AND o.name = 'CouponDefinitions')
BEGIN
    CREATE UNIQUE INDEX [UK_CouponDefinitions_Code] ON dbo.CouponDefinitions([Code]);
END
";
            migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var sql = @"
IF EXISTS (
    SELECT 1 FROM sys.indexes i
    INNER JOIN sys.objects o ON i.object_id = o.object_id
    WHERE i.name = 'UK_CouponDefinitions_Code' AND o.name = 'CouponDefinitions')
BEGIN
    DROP INDEX [UK_CouponDefinitions_Code] ON dbo.CouponDefinitions;
END
";
            migrationBuilder.Sql(sql);
        }
    }
}
