using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkin.Cotizador.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CotizacionCodigosErpSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cotizacion_Cliente_ClienteId",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropForeignKey(
                name: "FK_Cotizacion_TipoServicio_TipoServicioId",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropForeignKey(
                name: "FK_DetalleAmortizable_ActivoAmortizable_ActivoAmortizableId",
                schema: "dbo",
                table: "DetalleAmortizable");

            migrationBuilder.DropForeignKey(
                name: "FK_DetalleInsumo_Producto_ProductoId",
                schema: "dbo",
                table: "DetalleInsumo");

            migrationBuilder.DropIndex(
                name: "IX_DetalleInsumo_Producto",
                schema: "dbo",
                table: "DetalleInsumo");

            migrationBuilder.DropIndex(
                name: "IX_DetalleAmortizable_Activo",
                schema: "dbo",
                table: "DetalleAmortizable");

            migrationBuilder.DropIndex(
                name: "IX_Cotizacion_Cliente_Fecha",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropIndex(
                name: "IX_Cotizacion_TipoServicioId",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "ProductoId",
                schema: "dbo",
                table: "DetalleInsumo");

            migrationBuilder.DropColumn(
                name: "UnidadMedidaId",
                schema: "dbo",
                table: "DetalleInsumo");

            migrationBuilder.DropColumn(
                name: "ActivoAmortizableId",
                schema: "dbo",
                table: "DetalleAmortizable");

            migrationBuilder.DropColumn(
                name: "ClienteId",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "TecnicoAsignadoId",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "TipoServicioId",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.AddColumn<string>(
                name: "ProductoCodigo",
                schema: "dbo",
                table: "DetalleInsumo",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ActivoCodigo",
                schema: "dbo",
                table: "DetalleAmortizable",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClienteCodigo",
                schema: "dbo",
                table: "Cotizacion",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoCodigo",
                schema: "dbo",
                table: "Cotizacion",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EstadoNombre",
                schema: "dbo",
                table: "Cotizacion",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModalidadCodigo",
                schema: "dbo",
                table: "Cotizacion",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModalidadNombre",
                schema: "dbo",
                table: "Cotizacion",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TecnicoCodigo",
                schema: "dbo",
                table: "Cotizacion",
                type: "varchar(5)",
                unicode: false,
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TecnicoNombre",
                schema: "dbo",
                table: "Cotizacion",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoServicioCodigo",
                schema: "dbo",
                table: "Cotizacion",
                type: "varchar(4)",
                unicode: false,
                maxLength: 4,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TipoServicioNombre",
                schema: "dbo",
                table: "Cotizacion",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_DetalleInsumo_ProductoCodigo",
                schema: "dbo",
                table: "DetalleInsumo",
                column: "ProductoCodigo");

            migrationBuilder.CreateIndex(
                name: "IX_DetalleAmortizable_ActivoCodigo",
                schema: "dbo",
                table: "DetalleAmortizable",
                column: "ActivoCodigo");

            migrationBuilder.CreateIndex(
                name: "IX_Cotizacion_ClienteCodigo_Fecha",
                schema: "dbo",
                table: "Cotizacion",
                columns: new[] { "ClienteCodigo", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DetalleInsumo_ProductoCodigo",
                schema: "dbo",
                table: "DetalleInsumo");

            migrationBuilder.DropIndex(
                name: "IX_DetalleAmortizable_ActivoCodigo",
                schema: "dbo",
                table: "DetalleAmortizable");

            migrationBuilder.DropIndex(
                name: "IX_Cotizacion_ClienteCodigo_Fecha",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "ProductoCodigo",
                schema: "dbo",
                table: "DetalleInsumo");

            migrationBuilder.DropColumn(
                name: "ActivoCodigo",
                schema: "dbo",
                table: "DetalleAmortizable");

            migrationBuilder.DropColumn(
                name: "ClienteCodigo",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "EstadoCodigo",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "EstadoNombre",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "ModalidadCodigo",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "ModalidadNombre",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "TecnicoCodigo",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "TecnicoNombre",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "TipoServicioCodigo",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "TipoServicioNombre",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductoId",
                schema: "dbo",
                table: "DetalleInsumo",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UnidadMedidaId",
                schema: "dbo",
                table: "DetalleInsumo",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActivoAmortizableId",
                schema: "dbo",
                table: "DetalleAmortizable",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ClienteId",
                schema: "dbo",
                table: "Cotizacion",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TecnicoAsignadoId",
                schema: "dbo",
                table: "Cotizacion",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TipoServicioId",
                schema: "dbo",
                table: "Cotizacion",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_DetalleInsumo_Producto",
                schema: "dbo",
                table: "DetalleInsumo",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_DetalleAmortizable_Activo",
                schema: "dbo",
                table: "DetalleAmortizable",
                column: "ActivoAmortizableId");

            migrationBuilder.CreateIndex(
                name: "IX_Cotizacion_Cliente_Fecha",
                schema: "dbo",
                table: "Cotizacion",
                columns: new[] { "ClienteId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Cotizacion_TipoServicioId",
                schema: "dbo",
                table: "Cotizacion",
                column: "TipoServicioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cotizacion_Cliente_ClienteId",
                schema: "dbo",
                table: "Cotizacion",
                column: "ClienteId",
                principalSchema: "dbo",
                principalTable: "Cliente",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cotizacion_TipoServicio_TipoServicioId",
                schema: "dbo",
                table: "Cotizacion",
                column: "TipoServicioId",
                principalSchema: "dbo",
                principalTable: "TipoServicio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DetalleAmortizable_ActivoAmortizable_ActivoAmortizableId",
                schema: "dbo",
                table: "DetalleAmortizable",
                column: "ActivoAmortizableId",
                principalSchema: "dbo",
                principalTable: "ActivoAmortizable",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DetalleInsumo_Producto_ProductoId",
                schema: "dbo",
                table: "DetalleInsumo",
                column: "ProductoId",
                principalSchema: "dbo",
                principalTable: "Producto",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
