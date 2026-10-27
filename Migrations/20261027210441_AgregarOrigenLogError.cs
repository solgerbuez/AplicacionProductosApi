using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppProductos.Migrations
{
    /// <inheritdoc />
    public partial class AgregarOrigenLogError : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Origen",
                table: "LogErrores",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "API");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Origen",
                table: "LogErrores");
        }
    }
}
