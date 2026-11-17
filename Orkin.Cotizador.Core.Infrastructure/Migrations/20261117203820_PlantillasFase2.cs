using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkin.Cotizador.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlantillasFase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Plantilla",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LocacionCodigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TecnicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Contacto = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    VehiculoCodigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DescripcionTrabajo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plantilla", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Plantilla_Cliente_ClienteId",
                        column: x => x.ClienteId,
                        principalSchema: "dbo",
                        principalTable: "Cliente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Plantilla_Tecnico_TecnicoId",
                        column: x => x.TecnicoId,
                        principalTable: "Tecnico",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlantillaDetalle",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PlantillaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Consecutivo = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DescripcionArticulo = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    TipoPlagaCodigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LugarTratarCodigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Area = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Concentracion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DosificacionCodigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MetodoAplicacionCodigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnidadMedidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CostoUnitario = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CostoTotal = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    MaximoPermitido = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantillaDetalle", x => x.Id);
                    table.CheckConstraint("CK_PlantillaDetalle_Cantidad", "[Cantidad] >= 0");
                    table.CheckConstraint("CK_PlantillaDetalle_Consecutivo", "[Consecutivo] >= 1");
                    table.CheckConstraint("CK_PlantillaDetalle_CostoTotal", "[CostoTotal] >= 0");
                    table.CheckConstraint("CK_PlantillaDetalle_MaximoPermitido", "[MaximoPermitido] >= 0");
                    table.ForeignKey(
                        name: "FK_PlantillaDetalle_Plantilla_PlantillaId",
                        column: x => x.PlantillaId,
                        principalSchema: "dbo",
                        principalTable: "Plantilla",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlantillaDetalle_Producto_ProductoId",
                        column: x => x.ProductoId,
                        principalSchema: "dbo",
                        principalTable: "Producto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlantillaDetalle_UnidadMedida_UnidadMedidaId",
                        column: x => x.UnidadMedidaId,
                        principalSchema: "dbo",
                        principalTable: "UnidadMedida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlantillaEquipo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PlantillaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Consecutivo = table.Column<int>(type: "int", nullable: false),
                    EquipoCodigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Utilizado = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantillaEquipo", x => x.Id);
                    table.CheckConstraint("CK_PlantillaEquipo_Consecutivo", "[Consecutivo] >= 1");
                    table.ForeignKey(
                        name: "FK_PlantillaEquipo_Plantilla_PlantillaId",
                        column: x => x.PlantillaId,
                        principalSchema: "dbo",
                        principalTable: "Plantilla",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlantillaServicio",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PlantillaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Consecutivo = table.Column<int>(type: "int", nullable: false),
                    TipoServicioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FrecuenciaCodigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantillaServicio", x => x.Id);
                    table.CheckConstraint("CK_PlantillaServicio_Consecutivo", "[Consecutivo] >= 1");
                    table.ForeignKey(
                        name: "FK_PlantillaServicio_Plantilla_PlantillaId",
                        column: x => x.PlantillaId,
                        principalSchema: "dbo",
                        principalTable: "Plantilla",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlantillaServicio_TipoServicio_TipoServicioId",
                        column: x => x.TipoServicioId,
                        principalSchema: "dbo",
                        principalTable: "TipoServicio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plantilla_ClienteId",
                schema: "dbo",
                table: "Plantilla",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Plantilla_TecnicoId",
                schema: "dbo",
                table: "Plantilla",
                column: "TecnicoId");

            migrationBuilder.CreateIndex(
                name: "UX_Plantilla_Codigo",
                schema: "dbo",
                table: "Plantilla",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlantillaDetalle_ProductoId",
                schema: "dbo",
                table: "PlantillaDetalle",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_PlantillaDetalle_UnidadMedidaId",
                schema: "dbo",
                table: "PlantillaDetalle",
                column: "UnidadMedidaId");

            migrationBuilder.CreateIndex(
                name: "UX_PlantillaDetalle_Plantilla_Consecutivo",
                schema: "dbo",
                table: "PlantillaDetalle",
                columns: new[] { "PlantillaId", "Consecutivo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PlantillaEquipo_Plantilla_Consecutivo",
                schema: "dbo",
                table: "PlantillaEquipo",
                columns: new[] { "PlantillaId", "Consecutivo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlantillaServicio_TipoServicioId",
                schema: "dbo",
                table: "PlantillaServicio",
                column: "TipoServicioId");

            migrationBuilder.CreateIndex(
                name: "UX_PlantillaServicio_Plantilla_Consecutivo",
                schema: "dbo",
                table: "PlantillaServicio",
                columns: new[] { "PlantillaId", "Consecutivo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlantillaDetalle",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PlantillaEquipo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PlantillaServicio",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Plantilla",
                schema: "dbo");
        }
    }
}
