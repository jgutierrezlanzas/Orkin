using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkin.Cotizador.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProgramacionFlujos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "DesfaseSegundos",
                schema: "int",
                table: "ErpFlujo",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<bool>(
                name: "EsPausado",
                schema: "int",
                table: "ErpFlujo",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<short>(
                name: "MaximoFallosConsecutivos",
                schema: "int",
                table: "ErpFlujo",
                type: "smallint",
                nullable: false,
                defaultValue: (short)5);

            migrationBuilder.AddColumn<int>(
                name: "MaximoRegistrosPorCorrida",
                schema: "int",
                table: "ErpFlujo",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoPausa",
                schema: "int",
                table: "ErpFlujo",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProximaEjecucionUtc",
                schema: "int",
                table: "ErpFlujo",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimoIntentoUtc",
                schema: "int",
                table: "ErpFlujo",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErpFlujo_Programados",
                schema: "int",
                table: "ErpFlujo",
                column: "ProximaEjecucionUtc",
                filter: "[EsPausado] = 0 AND [IntervaloSegundos] IS NOT NULL AND [Estado] <> 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ErpFlujo_Desfase",
                schema: "int",
                table: "ErpFlujo",
                sql: "[DesfaseSegundos] BETWEEN 0 AND 30");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ErpFlujo_Fallos",
                schema: "int",
                table: "ErpFlujo",
                sql: "[MaximoFallosConsecutivos] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ErpFlujo_IntervaloMinimo",
                schema: "int",
                table: "ErpFlujo",
                sql: "[IntervaloSegundos] IS NULL OR [IntervaloSegundos] >= 10");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ErpFlujo_Programados",
                schema: "int",
                table: "ErpFlujo");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ErpFlujo_Desfase",
                schema: "int",
                table: "ErpFlujo");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ErpFlujo_Fallos",
                schema: "int",
                table: "ErpFlujo");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ErpFlujo_IntervaloMinimo",
                schema: "int",
                table: "ErpFlujo");

            migrationBuilder.DropColumn(
                name: "DesfaseSegundos",
                schema: "int",
                table: "ErpFlujo");

            migrationBuilder.DropColumn(
                name: "EsPausado",
                schema: "int",
                table: "ErpFlujo");

            migrationBuilder.DropColumn(
                name: "MaximoFallosConsecutivos",
                schema: "int",
                table: "ErpFlujo");

            migrationBuilder.DropColumn(
                name: "MaximoRegistrosPorCorrida",
                schema: "int",
                table: "ErpFlujo");

            migrationBuilder.DropColumn(
                name: "MotivoPausa",
                schema: "int",
                table: "ErpFlujo");

            migrationBuilder.DropColumn(
                name: "ProximaEjecucionUtc",
                schema: "int",
                table: "ErpFlujo");

            migrationBuilder.DropColumn(
                name: "UltimoIntentoUtc",
                schema: "int",
                table: "ErpFlujo");
        }
    }
}
