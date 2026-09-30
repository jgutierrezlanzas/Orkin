using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkin.Cotizador.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CatalogoTecnicos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tecnico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cargo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    OrigenDatoId = table.Column<byte>(type: "tinyint", nullable: false),
                    CodigoExterno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HashContenido = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    UltimaSyncUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tecnico", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tecnico");
        }
    }
}
