using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkin.Cotizador.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ColorCalendarioTecnico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColorCalendario",
                table: "Tecnico",
                type: "varchar(7)",
                unicode: false,
                maxLength: 7,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColorCalendario",
                table: "Tecnico");
        }
    }
}
