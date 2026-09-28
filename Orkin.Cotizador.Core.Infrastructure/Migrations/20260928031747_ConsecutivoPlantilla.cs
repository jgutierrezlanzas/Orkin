using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkin.Cotizador.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConsecutivoPlantilla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── A mano: limpieza de la semilla vieja ─────────────────────────────────────────────
            // TipoConsecutivoCatalogo sembró cuatro filas: COTIZACION, CLIENTE, PRODUCTO y ACTIVO.
            // Solo COTIZACION se numera con un consecutivo; los códigos de cliente, producto y
            // activo los trae la transformación desde el ERP y no le pertenecen a este catalogo.
            //
            // La fila COTIZACION también se borra: el seeder no toca filas con EsSembrado=1 ni
            // siquiera para actualizarlas (§5.20), así que la única forma de que la semilla nueva —
            // que ahora trae Plantilla, Sufijo y ReiniciaPorAnio— sea la fuente de verdad es quitar
            // la fila y dejar que el seeder la vuelva a insertar en el próximo arranque.
            //
            // dbo.Consecutivo está vacía (nadie emitió todavía), así que el borrado no choca con la
            // FK Restrict. La FK de Cotizacion → TipoConsecutivoCatalogo se crea más abajo, en esta
            // misma migración, y por lo tanto no está presente cuando corre el DELETE.
            migrationBuilder.Sql(
                "DELETE FROM dbo.TipoConsecutivoCatalogo " +
                "WHERE Codigo IN ('COTIZACION', 'CLIENTE', 'PRODUCTO', 'ACTIVO');");

            migrationBuilder.AlterColumn<string>(
                name: "Prefijo",
                schema: "dbo",
                table: "TipoConsecutivoCatalogo",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<int>(
                name: "Digitos",
                schema: "dbo",
                table: "TipoConsecutivoCatalogo",
                type: "int",
                nullable: false,
                defaultValue: 4,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldDefaultValue: (short)4);

            // Sin defaultValue: la tabla quedó vacía con el DELETE de arriba, y un valor por
            // defecto compilado aquí contradiría a la semilla, que es la que debe decir cuál es la
            // plantilla de cada tipo. Si el DELETE no hubiera corrido, la columna NOT NULL sin
            // default no se podría agregar; el orden de las operaciones es parte de la migración.
            migrationBuilder.AddColumn<string>(
                name: "Plantilla",
                schema: "dbo",
                table: "TipoConsecutivoCatalogo",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReiniciaPorAnio",
                schema: "dbo",
                table: "TipoConsecutivoCatalogo",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Sufijo",
                schema: "dbo",
                table: "TipoConsecutivoCatalogo",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false);

            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                schema: "dbo",
                table: "Producto",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "Numero",
                schema: "dbo",
                table: "Cotizacion",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<byte>(
                name: "TipoConsecutivoId",
                schema: "dbo",
                table: "Cotizacion",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Prefijo",
                schema: "dbo",
                table: "Consecutivo",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Plantilla",
                schema: "dbo",
                table: "Consecutivo",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sufijo",
                schema: "dbo",
                table: "Consecutivo",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                schema: "dbo",
                table: "Cliente",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                schema: "dbo",
                table: "ActivoAmortizable",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.CreateIndex(
                name: "IX_Cotizacion_TipoConsecutivoId",
                schema: "dbo",
                table: "Cotizacion",
                column: "TipoConsecutivoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cotizacion_TipoConsecutivoCatalogo_TipoConsecutivoId",
                schema: "dbo",
                table: "Cotizacion",
                column: "TipoConsecutivoId",
                principalSchema: "dbo",
                principalTable: "TipoConsecutivoCatalogo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ── A mano: la UDF del §5.15 y el CHECK que la usa ──────────────────────────────────
            // Lo mismo que dbo.FnEsEstadoFinalAprobacion: son dos piezas que no salen del modelo,
            // y es a propósito. El CHECK necesita llamar a la función, y si la función viviera en
            // OnModelCreating, el modelo tendría que saber que existe —y una migración generada
            // después intentaría volver a crearla—.
            //
            // Las reglas que valida, en orden:
            //   1. La plantilla no está vacía.
            //   2. Digitos entra en 1..19, que es lo que aguanta un bigint (UltimoNumero).
            //   3. {NUM} existe y aparece exactamente una vez.
            //   4. Cada '{...}' es una de PRE, SUF, ANIO o NUM, y ninguna se repite.
            //   5. Si la plantilla lleva {ANIO}, el contador tiene que reiniciar cada año:
            //      un número con año en medio que no se reinicia mezclaría años y contadores, y
            //      el lector no podría saber si 2026-41 va antes o después de 2026-5.
            // La función no toca tablas, así que no hace falta SCHEMABINDING.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION dbo.FnPlantillaConsecutivoValida (
                    @Plantilla nvarchar(80),
                    @ReiniciaPorAnio bit,
                    @Digitos int
                )
                RETURNS bit
                AS
                BEGIN
                    IF @Plantilla IS NULL OR LTRIM(RTRIM(@Plantilla)) = N''
                        RETURN 0;

                    IF @Digitos < 1 OR @Digitos > 19
                        RETURN 0;

                    DECLARE @Largo int = LEN(@Plantilla);
                    DECLARE @I int = 1;
                    DECLARE @Pre int = 0, @Suf int = 0, @Anio int = 0, @Num int = 0;

                    WHILE @I <= @Largo
                    BEGIN
                        IF SUBSTRING(@Plantilla, @I, 1) = N'{'
                        BEGIN
                            DECLARE @Cierre int = CHARINDEX(N'}', @Plantilla, @I);
                            IF @Cierre = 0
                                RETURN 0;

                            DECLARE @Seccion nvarchar(10) =
                                SUBSTRING(@Plantilla, @I, @Cierre - @I + 1);

                            IF @Seccion = N'{PRE}' SET @Pre = @Pre + 1;
                            ELSE IF @Seccion = N'{SUF}' SET @Suf = @Suf + 1;
                            ELSE IF @Seccion = N'{ANIO}' SET @Anio = @Anio + 1;
                            ELSE IF @Seccion = N'{NUM}' SET @Num = @Num + 1;
                            ELSE RETURN 0;

                            SET @I = @Cierre + 1;
                        END
                        ELSE
                            SET @I = @I + 1;
                    END

                    IF @Num <> 1
                        RETURN 0;
                    IF @Pre > 1 OR @Suf > 1 OR @Anio > 1
                        RETURN 0;
                    IF @Anio > 0 AND @ReiniciaPorAnio = 0
                        RETURN 0;

                    RETURN 1;
                END
                """);

            // El CHECK va después de la función y de las columnas nuevas: SQL Server lo valida
            // contra las filas existentes al crearlo, y la tabla quedó vacía con el DELETE de
            // arriba. A partir del próximo INSERT (el del seeder, que trae la plantilla de
            // COTIZACION), cada fila que entre con una plantilla inválida se cae acá.
            migrationBuilder.Sql(
                "ALTER TABLE dbo.TipoConsecutivoCatalogo " +
                "ADD CONSTRAINT CK_TipoConsecutivoCatalogo_PlantillaValida " +
                "CHECK (dbo.FnPlantillaConsecutivoValida(Plantilla, ReiniciaPorAnio, Digitos) = 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El orden es CHECK → función → columnas, por las mismas razones que en la migración
            // inicial: el CHECK no se puede quitar antes de que la columna desaparezca (la función
            // responde por valores de esa columna) ni la función antes que el CHECK que la llama.
            migrationBuilder.Sql(
                "ALTER TABLE dbo.TipoConsecutivoCatalogo " +
                "DROP CONSTRAINT IF EXISTS CK_TipoConsecutivoCatalogo_PlantillaValida;");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.FnPlantillaConsecutivoValida;");

            migrationBuilder.DropForeignKey(
                name: "FK_Cotizacion_TipoConsecutivoCatalogo_TipoConsecutivoId",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropIndex(
                name: "IX_Cotizacion_TipoConsecutivoId",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "Plantilla",
                schema: "dbo",
                table: "TipoConsecutivoCatalogo");

            migrationBuilder.DropColumn(
                name: "ReiniciaPorAnio",
                schema: "dbo",
                table: "TipoConsecutivoCatalogo");

            migrationBuilder.DropColumn(
                name: "Sufijo",
                schema: "dbo",
                table: "TipoConsecutivoCatalogo");

            migrationBuilder.DropColumn(
                name: "TipoConsecutivoId",
                schema: "dbo",
                table: "Cotizacion");

            migrationBuilder.DropColumn(
                name: "Plantilla",
                schema: "dbo",
                table: "Consecutivo");

            migrationBuilder.DropColumn(
                name: "Sufijo",
                schema: "dbo",
                table: "Consecutivo");

            migrationBuilder.AlterColumn<string>(
                name: "Prefijo",
                schema: "dbo",
                table: "TipoConsecutivoCatalogo",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<short>(
                name: "Digitos",
                schema: "dbo",
                table: "TipoConsecutivoCatalogo",
                type: "smallint",
                nullable: false,
                defaultValue: (short)4,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 4);

            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                schema: "dbo",
                table: "Producto",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Numero",
                schema: "dbo",
                table: "Cotizacion",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Prefijo",
                schema: "dbo",
                table: "Consecutivo",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                schema: "dbo",
                table: "Cliente",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                schema: "dbo",
                table: "ActivoAmortizable",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);
        }
    }
}