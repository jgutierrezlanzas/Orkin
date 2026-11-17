using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkin.Cotizador.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ErpMapeoTipoCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ErpMapeoTipoCliente",
                schema: "int",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodigoCategoriaErp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TipoClienteCatalogoId = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpMapeoTipoCliente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErpMapeoTipoCliente_ErpConexion_ErpConexionId",
                        column: x => x.ErpConexionId,
                        principalSchema: "int",
                        principalTable: "ErpConexion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ErpMapeoTipoCliente_TipoClienteCatalogo_TipoClienteCatalogoId",
                        column: x => x.TipoClienteCatalogoId,
                        principalSchema: "dbo",
                        principalTable: "TipoClienteCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErpMapeoTipoCliente_TipoClienteCatalogoId",
                schema: "int",
                table: "ErpMapeoTipoCliente",
                column: "TipoClienteCatalogoId");

            migrationBuilder.CreateIndex(
                name: "UX_ErpMapeoTipoCliente_ConexionCategoria",
                schema: "int",
                table: "ErpMapeoTipoCliente",
                columns: new[] { "ErpConexionId", "CodigoCategoriaErp" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErpMapeoTipoCliente",
                schema: "int");
        }
    }
}
