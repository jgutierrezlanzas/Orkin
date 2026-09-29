using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkin.Cotizador.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EtlOrigenes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "int");

            migrationBuilder.CreateTable(
                name: "ErpConexion",
                schema: "int",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TipoProveedor = table.Column<byte>(type: "tinyint", nullable: false),
                    Servidor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Puerto = table.Column<int>(type: "int", nullable: true),
                    NombreBaseDatos = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Esquema = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MetodoAutenticacion = table.Column<byte>(type: "tinyint", nullable: false),
                    Usuario = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContrasenaCifrada = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    TiempoEsperaSegundos = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
                    LoteRegistros = table.Column<int>(type: "int", nullable: false, defaultValue: 500),
                    EsSoloLectura = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    UltimaConexionOkUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UltimoError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UltimoErrorUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpConexion", x => x.Id);
                    table.CheckConstraint("CK_ErpConexion_SoloLectura", "[EsSoloLectura] = 1");
                });

            migrationBuilder.CreateTable(
                name: "ErpFlujo",
                schema: "int",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Catalogo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    MapeoJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TipoDisparador = table.Column<byte>(type: "tinyint", nullable: false),
                    IntervaloSegundos = table.Column<int>(type: "int", nullable: true),
                    ModoExtraccion = table.Column<byte>(type: "tinyint", nullable: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    UltimaEjecucionUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UltimoEstado = table.Column<byte>(type: "tinyint", nullable: true),
                    UltimoError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UltimosInsertados = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UltimosActualizados = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UltimosOmitidos = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UltimosRechazados = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    FallosConsecutivos = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpFlujo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErpFlujo_ErpConexion_ErpConexionId",
                        column: x => x.ErpConexionId,
                        principalSchema: "int",
                        principalTable: "ErpConexion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SincronizacionEjecucion",
                schema: "int",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ErpFlujoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoDisparador = table.Column<byte>(type: "tinyint", nullable: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    FinUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    DuracionMs = table.Column<int>(type: "int", nullable: true),
                    RegistrosLeidos = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RegistrosCreados = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RegistrosActualizados = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RegistrosSinCambio = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RegistrosRechazados = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RegistrosDesactivados = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MensajeError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SolicitadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SincronizacionEjecucion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SincronizacionEjecucion_ErpConexion_ErpConexionId",
                        column: x => x.ErpConexionId,
                        principalSchema: "int",
                        principalTable: "ErpConexion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SincronizacionEjecucion_ErpFlujo_ErpFlujoId",
                        column: x => x.ErpFlujoId,
                        principalSchema: "int",
                        principalTable: "ErpFlujo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnidadMedida_ErpConexionId",
                schema: "dbo",
                table: "UnidadMedida",
                column: "ErpConexionId");

            migrationBuilder.CreateIndex(
                name: "IX_TipoServicio_ErpConexionId",
                schema: "dbo",
                table: "TipoServicio",
                column: "ErpConexionId");

            migrationBuilder.CreateIndex(
                name: "IX_TipoCambio_ErpConexionId",
                schema: "dbo",
                table: "TipoCambio",
                column: "ErpConexionId");

            migrationBuilder.CreateIndex(
                name: "IX_Producto_ErpPendientes",
                schema: "dbo",
                table: "Producto",
                columns: new[] { "ErpConexionId", "HashContenido" });

            migrationBuilder.CreateIndex(
                name: "UX_Producto_ErpCodigoExterno",
                schema: "dbo",
                table: "Producto",
                columns: new[] { "ErpConexionId", "CodigoExterno" },
                unique: true,
                filter: "[ErpConexionId] IS NOT NULL AND [CodigoExterno] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cliente_ErpPendientes",
                schema: "dbo",
                table: "Cliente",
                columns: new[] { "ErpConexionId", "HashContenido" });

            migrationBuilder.CreateIndex(
                name: "UX_Cliente_ErpCodigoExterno",
                schema: "dbo",
                table: "Cliente",
                columns: new[] { "ErpConexionId", "CodigoExterno" },
                unique: true,
                filter: "[ErpConexionId] IS NOT NULL AND [CodigoExterno] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriaProducto_ErpConexionId",
                schema: "dbo",
                table: "CategoriaProducto",
                column: "ErpConexionId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivoAmortizable_ErpPendientes",
                schema: "dbo",
                table: "ActivoAmortizable",
                columns: new[] { "ErpConexionId", "HashContenido" });

            migrationBuilder.CreateIndex(
                name: "UX_ActivoAmortizable_ErpCodigoExterno",
                schema: "dbo",
                table: "ActivoAmortizable",
                columns: new[] { "ErpConexionId", "CodigoExterno" },
                unique: true,
                filter: "[ErpConexionId] IS NOT NULL AND [CodigoExterno] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ErpConexion_Nombre",
                schema: "int",
                table: "ErpConexion",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ErpFlujo_ConexionNombre",
                schema: "int",
                table: "ErpFlujo",
                columns: new[] { "ErpConexionId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SincronizacionEjecucion_ErpConexionId",
                schema: "int",
                table: "SincronizacionEjecucion",
                column: "ErpConexionId");

            migrationBuilder.CreateIndex(
                name: "IX_SincronizacionEjecucion_EstadoInicio",
                schema: "int",
                table: "SincronizacionEjecucion",
                columns: new[] { "Estado", "InicioUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SincronizacionEjecucion_FlujoInicio",
                schema: "int",
                table: "SincronizacionEjecucion",
                columns: new[] { "ErpFlujoId", "InicioUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_ActivoAmortizable_ErpConexion_ErpConexionId",
                schema: "dbo",
                table: "ActivoAmortizable",
                column: "ErpConexionId",
                principalSchema: "int",
                principalTable: "ErpConexion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CategoriaProducto_ErpConexion_ErpConexionId",
                schema: "dbo",
                table: "CategoriaProducto",
                column: "ErpConexionId",
                principalSchema: "int",
                principalTable: "ErpConexion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cliente_ErpConexion_ErpConexionId",
                schema: "dbo",
                table: "Cliente",
                column: "ErpConexionId",
                principalSchema: "int",
                principalTable: "ErpConexion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Producto_ErpConexion_ErpConexionId",
                schema: "dbo",
                table: "Producto",
                column: "ErpConexionId",
                principalSchema: "int",
                principalTable: "ErpConexion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TipoCambio_ErpConexion_ErpConexionId",
                schema: "dbo",
                table: "TipoCambio",
                column: "ErpConexionId",
                principalSchema: "int",
                principalTable: "ErpConexion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TipoServicio_ErpConexion_ErpConexionId",
                schema: "dbo",
                table: "TipoServicio",
                column: "ErpConexionId",
                principalSchema: "int",
                principalTable: "ErpConexion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnidadMedida_ErpConexion_ErpConexionId",
                schema: "dbo",
                table: "UnidadMedida",
                column: "ErpConexionId",
                principalSchema: "int",
                principalTable: "ErpConexion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivoAmortizable_ErpConexion_ErpConexionId",
                schema: "dbo",
                table: "ActivoAmortizable");

            migrationBuilder.DropForeignKey(
                name: "FK_CategoriaProducto_ErpConexion_ErpConexionId",
                schema: "dbo",
                table: "CategoriaProducto");

            migrationBuilder.DropForeignKey(
                name: "FK_Cliente_ErpConexion_ErpConexionId",
                schema: "dbo",
                table: "Cliente");

            migrationBuilder.DropForeignKey(
                name: "FK_Producto_ErpConexion_ErpConexionId",
                schema: "dbo",
                table: "Producto");

            migrationBuilder.DropForeignKey(
                name: "FK_TipoCambio_ErpConexion_ErpConexionId",
                schema: "dbo",
                table: "TipoCambio");

            migrationBuilder.DropForeignKey(
                name: "FK_TipoServicio_ErpConexion_ErpConexionId",
                schema: "dbo",
                table: "TipoServicio");

            migrationBuilder.DropForeignKey(
                name: "FK_UnidadMedida_ErpConexion_ErpConexionId",
                schema: "dbo",
                table: "UnidadMedida");

            migrationBuilder.DropTable(
                name: "SincronizacionEjecucion",
                schema: "int");

            migrationBuilder.DropTable(
                name: "ErpFlujo",
                schema: "int");

            migrationBuilder.DropTable(
                name: "ErpConexion",
                schema: "int");

            migrationBuilder.DropIndex(
                name: "IX_UnidadMedida_ErpConexionId",
                schema: "dbo",
                table: "UnidadMedida");

            migrationBuilder.DropIndex(
                name: "IX_TipoServicio_ErpConexionId",
                schema: "dbo",
                table: "TipoServicio");

            migrationBuilder.DropIndex(
                name: "IX_TipoCambio_ErpConexionId",
                schema: "dbo",
                table: "TipoCambio");

            migrationBuilder.DropIndex(
                name: "IX_Producto_ErpPendientes",
                schema: "dbo",
                table: "Producto");

            migrationBuilder.DropIndex(
                name: "UX_Producto_ErpCodigoExterno",
                schema: "dbo",
                table: "Producto");

            migrationBuilder.DropIndex(
                name: "IX_Cliente_ErpPendientes",
                schema: "dbo",
                table: "Cliente");

            migrationBuilder.DropIndex(
                name: "UX_Cliente_ErpCodigoExterno",
                schema: "dbo",
                table: "Cliente");

            migrationBuilder.DropIndex(
                name: "IX_CategoriaProducto_ErpConexionId",
                schema: "dbo",
                table: "CategoriaProducto");

            migrationBuilder.DropIndex(
                name: "IX_ActivoAmortizable_ErpPendientes",
                schema: "dbo",
                table: "ActivoAmortizable");

            migrationBuilder.DropIndex(
                name: "UX_ActivoAmortizable_ErpCodigoExterno",
                schema: "dbo",
                table: "ActivoAmortizable");
        }
    }
}
