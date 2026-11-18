using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkin.Cotizador.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NotificacionesFase8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClaveNegocio",
                schema: "dbo",
                table: "NotificacionEnviada",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CuerpoHtml",
                schema: "dbo",
                table: "NotificacionEnviada",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeidoUtc",
                schema: "dbo",
                table: "NotificacionEnviada",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProximoIntentoUtc",
                schema: "dbo",
                table: "NotificacionEnviada",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JobEjecucion",
                schema: "int",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Operacion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IniciadoUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    FinalizadoUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Encolados = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Enviados = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Fallidos = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Mensaje = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Usuario = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobEjecucion", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notificacion_ClaveNegocio",
                schema: "dbo",
                table: "NotificacionEnviada",
                column: "ClaveNegocio");

            migrationBuilder.CreateIndex(
                name: "IX_Notificacion_Cola",
                schema: "dbo",
                table: "NotificacionEnviada",
                columns: new[] { "Estado", "ProximoIntentoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_JobEjecucion_NombreInicio",
                schema: "int",
                table: "JobEjecucion",
                columns: new[] { "Nombre", "IniciadoUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobEjecucion",
                schema: "int");

            migrationBuilder.DropIndex(
                name: "IX_Notificacion_ClaveNegocio",
                schema: "dbo",
                table: "NotificacionEnviada");

            migrationBuilder.DropIndex(
                name: "IX_Notificacion_Cola",
                schema: "dbo",
                table: "NotificacionEnviada");

            migrationBuilder.DropColumn(
                name: "ClaveNegocio",
                schema: "dbo",
                table: "NotificacionEnviada");

            migrationBuilder.DropColumn(
                name: "CuerpoHtml",
                schema: "dbo",
                table: "NotificacionEnviada");

            migrationBuilder.DropColumn(
                name: "LeidoUtc",
                schema: "dbo",
                table: "NotificacionEnviada");

            migrationBuilder.DropColumn(
                name: "ProximoIntentoUtc",
                schema: "dbo",
                table: "NotificacionEnviada");
        }
    }
}
