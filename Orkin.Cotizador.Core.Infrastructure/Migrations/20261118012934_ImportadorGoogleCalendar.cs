using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkin.Cotizador.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImportadorGoogleCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ErpMapeoEventoGoogle",
                schema: "int",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Campo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ValorOrigen = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ValorDestino = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpMapeoEventoGoogle", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportacionGoogleLote",
                schema: "int",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FechaImportacionUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalEventos = table.Column<int>(type: "int", nullable: false),
                    Confirmados = table.Column<int>(type: "int", nullable: false),
                    Descartados = table.Column<int>(type: "int", nullable: false),
                    ConError = table.Column<int>(type: "int", nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportacionGoogleLote", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportacionGoogleEvento",
                schema: "int",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    LoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventoId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CalendarId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Ubicacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FechaInicio = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    FechaFin = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ClienteParseado = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LocacionParseada = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TipoServicioParseado = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VehiculoParseado = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TecnicosParseados = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Nota = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ClienteCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LocacionCodigo = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    TipoServicioCodigo = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    VehiculoCodigo = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    TecnicosCodigos = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Plantilla = table.Column<long>(type: "bigint", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IdVisita = table.Column<long>(type: "bigint", nullable: true),
                    ProcesadoUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportacionGoogleEvento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportacionGoogleEvento_ImportacionGoogleLote_LoteId",
                        column: x => x.LoteId,
                        principalSchema: "int",
                        principalTable: "ImportacionGoogleLote",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ErpMapeoEventoGoogle_CampoOrigen",
                schema: "int",
                table: "ErpMapeoEventoGoogle",
                columns: new[] { "Campo", "ValorOrigen" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ImportacionGoogleEvento_LoteEvento",
                schema: "int",
                table: "ImportacionGoogleEvento",
                columns: new[] { "LoteId", "EventoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErpMapeoEventoGoogle",
                schema: "int");

            migrationBuilder.DropTable(
                name: "ImportacionGoogleEvento",
                schema: "int");

            migrationBuilder.DropTable(
                name: "ImportacionGoogleLote",
                schema: "int");
        }
    }
}
