using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkin.Cotizador.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EsquemaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.EnsureSchema(
                name: "seg");

            migrationBuilder.EnsureSchema(
                name: "aud");

            migrationBuilder.CreateTable(
                name: "AccionAuditoriaCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccionAuditoriaCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentoGenerado",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CotizacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    NombreArchivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Ruta = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    Hash = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    GeneradoUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    GeneradoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentoGenerado", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EstadoAprobacionCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsEstadoFinal = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadoAprobacionCatalogo", x => x.Id);
                });

            // ── A mano: la UDF del §5.7 y el CHECK del §6.11 que la usa ─────────────────────────────
            // Estas dos piezas no salen del modelo, y es a propósito. Un CHECK de SQL Server no
            // admite subconsultas, así que "el estado está resuelto" no se puede preguntar con un
            // IN contra el catálogo. La salida es partirlo en dos: el catálogo dice QUÉ estados son
            // finales —una columna de bandera, que es dato— y el CHECK dice QUÉ tiene que ser
            // cierto si el estado lo es —que es la regla de negocio—.
            //
            // Por qué escribirlas acá y no en OnModelCreating: HasCheckConstraint necesita el texto
            // del CHECK, y ese texto tiene que traer la llamada a la función. Si la regla viviera
            // en el modelo, el modelo tendría que saber que existe una función, y una migración
            // generada después intentaría volver a crearla.
            //
            // Va justo después de EstadoAprobacionCatalogo y no al final de Up() porque SQL Server
            // resuelve los nombres del cuerpo de una función escalar al crearla: si la función se
            // creara antes de existir la tabla, el CREATE FUNCTION fallaría.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION dbo.FnEsEstadoFinalAprobacion (@Estado tinyint)
                RETURNS bit
                WITH SCHEMABINDING
                AS
                BEGIN
                    -- SCHEMABINDING obliga a que la función quede atada a la tabla y a la columna.
                    -- Sin eso, alguien podría cambiar el nombre de EsEstadoFinal y dejarían de
                    -- funcionar todos los CHECK que la usa, sin que ninguna migración lo dijera.
                    -- El precio es que esta función no se puede reemplazar con ALTER, hay que
                    -- quitarla y volver a crearla; es un intercambio que sale a cuenta.
                    DECLARE @EsFinal bit = 0;

                    SELECT @EsFinal = EsEstadoFinal
                    FROM dbo.EstadoAprobacionCatalogo
                    WHERE Id = @Estado;

                    RETURN ISNULL(@EsFinal, 0);
                END
                """);

            // Un @Estado que no está en el catálogo devuelve 0 y no un error. La razón es que este
            // CHECK se evalúa en cada INSERT y en cada UPDATE de la tabla, y un estado desconocido
            // tiene que ser un "todavía no es final", que es lo que permite insertar la fila y
            // corregirla después, y no un error que preclude cualquier arreglo. La FK es la que
            // garantiza que el estado exista; esta función solo responde por la bandera.

            migrationBuilder.CreateTable(
                name: "EstadoCotizacionCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadoCotizacionCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EstadoNotificacionCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadoNotificacionCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FuenteTipoCambioCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuenteTipoCambioCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetodoCosteoCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetodoCosteoCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModalidadCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EsPredeterminado = table.Column<bool>(type: "bit", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModalidadCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModuloPermisoCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuloPermisoCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NivelAccesoCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NivelAccesoCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NivelAprobacionCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NivelAprobacionCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificacionEnviada",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    CotizacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Destinatario = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Asunto = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    Intentos = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    EnviadoUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    IdExterno = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificacionEnviada", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrigenDatoCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrigenDatoCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrigenMargenCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrigenMargenCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrigenPrecioCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrigenPrecioCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistroAuditoria",
                schema: "aud",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Entidad = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    EntidadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Accion = table.Column<byte>(type: "tinyint", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NombreUsuario = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FechaUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Ip = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Agente = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CamposModificados = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ValorAnteriorJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValorNuevoJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistroAuditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SemestreCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SemestreCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TipoConsecutivoCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Prefijo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Digitos = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)4),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoConsecutivoCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TipoDatoParametroCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoDatoParametroCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TipoDocumentoCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoDocumentoCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TipoNotificacionCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoNotificacionCatalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuario",
                schema: "seg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    NombreCompleto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Documento = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Cargo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsUsuarioSistema = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DebeCambiarContrasena = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UltimoAccesoUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UltimoAccesoIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LockoutEnd = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TipoClienteCatalogo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MetodoCosteoId = table.Column<byte>(type: "tinyint", nullable: false),
                    EsPredeterminado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoClienteCatalogo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TipoClienteCatalogo_MetodoCosteoCatalogo_MetodoCosteoId",
                        column: x => x.MetodoCosteoId,
                        principalSchema: "dbo",
                        principalTable: "MetodoCosteoCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Permiso",
                schema: "seg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Codigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ModuloId = table.Column<byte>(type: "tinyint", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Orden = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permiso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Permiso_ModuloPermisoCatalogo_ModuloId",
                        column: x => x.ModuloId,
                        principalSchema: "dbo",
                        principalTable: "ModuloPermisoCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rol",
                schema: "seg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EsSistema = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EsActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    NivelAccesoId = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rol", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rol_NivelAccesoCatalogo_NivelAccesoId",
                        column: x => x.NivelAccesoId,
                        principalSchema: "dbo",
                        principalTable: "NivelAccesoCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CategoriaProducto",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    OrigenDatoId = table.Column<byte>(type: "tinyint", nullable: false),
                    CodigoExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HashContenido = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    UltimaSyncUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriaProducto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoriaProducto_OrigenDatoCatalogo_OrigenDatoId",
                        column: x => x.OrigenDatoId,
                        principalSchema: "dbo",
                        principalTable: "OrigenDatoCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TipoCambio",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Fuente = table.Column<byte>(type: "tinyint", nullable: false),
                    OrigenDatoId = table.Column<byte>(type: "tinyint", nullable: false),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CodigoExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HashContenido = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    UltimaSyncUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoCambio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TipoCambio_FuenteTipoCambioCatalogo_Fuente",
                        column: x => x.Fuente,
                        principalSchema: "dbo",
                        principalTable: "FuenteTipoCambioCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TipoCambio_OrigenDatoCatalogo_OrigenDatoId",
                        column: x => x.OrigenDatoId,
                        principalSchema: "dbo",
                        principalTable: "OrigenDatoCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnidadMedida",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Abreviatura = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    PermiteDecimales = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    OrigenDatoId = table.Column<byte>(type: "tinyint", nullable: false),
                    CodigoExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HashContenido = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    UltimaSyncUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnidadMedida", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnidadMedida_OrigenDatoCatalogo_OrigenDatoId",
                        column: x => x.OrigenDatoId,
                        principalSchema: "dbo",
                        principalTable: "OrigenDatoCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Consecutivo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    Anio = table.Column<short>(type: "smallint", nullable: false),
                    Prefijo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UltimoNumero = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    Digitos = table.Column<int>(type: "int", nullable: false, defaultValue: 4),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consecutivo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Consecutivo_TipoConsecutivoCatalogo_Tipo",
                        column: x => x.Tipo,
                        principalSchema: "dbo",
                        principalTable: "TipoConsecutivoCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParametroGeneral",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Valor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TipoDato = table.Column<byte>(type: "tinyint", nullable: false),
                    Grupo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Orden = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    EsSistema = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EsSecreto = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametroGeneral", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParametroGeneral_TipoDatoParametroCatalogo_TipoDato",
                        column: x => x.TipoDato,
                        principalSchema: "dbo",
                        principalTable: "TipoDatoParametroCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UsuarioClaim",
                schema: "seg",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioClaim", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsuarioClaim_Usuario_UserId",
                        column: x => x.UserId,
                        principalSchema: "seg",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuarioLogin",
                schema: "seg",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioLogin", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UsuarioLogin_Usuario_UserId",
                        column: x => x.UserId,
                        principalSchema: "seg",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuarioToken",
                schema: "seg",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioToken", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UsuarioToken_Usuario_UserId",
                        column: x => x.UserId,
                        principalSchema: "seg",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cliente",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RazonSocial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TipoCliente = table.Column<byte>(type: "tinyint", nullable: false),
                    CedulaORuc = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IdentificacionFiscal = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Ciudad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Departamento = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ContactoNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ContactoCargo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SitioWeb = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OrigenDatoId = table.Column<byte>(type: "tinyint", nullable: false),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CodigoExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HashContenido = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    UltimaSyncUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cliente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cliente_OrigenDatoCatalogo_OrigenDatoId",
                        column: x => x.OrigenDatoId,
                        principalSchema: "dbo",
                        principalTable: "OrigenDatoCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cliente_TipoClienteCatalogo_TipoCliente",
                        column: x => x.TipoCliente,
                        principalSchema: "dbo",
                        principalTable: "TipoClienteCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UsuarioPermiso",
                schema: "seg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermisoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Concedido = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VigenteHasta = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioPermiso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsuarioPermiso_Permiso_PermisoId",
                        column: x => x.PermisoId,
                        principalSchema: "seg",
                        principalTable: "Permiso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsuarioPermiso_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalSchema: "seg",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolClaim",
                schema: "seg",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolClaim", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolClaim_Rol_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "seg",
                        principalTable: "Rol",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolPermiso",
                schema: "seg",
                columns: table => new
                {
                    RolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermisoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsignadoUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    AsignadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolPermiso", x => new { x.RolId, x.PermisoId });
                    table.ForeignKey(
                        name: "FK_RolPermiso_Permiso_PermisoId",
                        column: x => x.PermisoId,
                        principalSchema: "seg",
                        principalTable: "Permiso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolPermiso_Rol_RolId",
                        column: x => x.RolId,
                        principalSchema: "seg",
                        principalTable: "Rol",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuarioRol",
                schema: "seg",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioRol", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UsuarioRol_Rol_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "seg",
                        principalTable: "Rol",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsuarioRol_Usuario_UserId",
                        column: x => x.UserId,
                        principalSchema: "seg",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActivoAmortizable",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CategoriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnidadMedidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CostoActual = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    MesesVidaUtil = table.Column<int>(type: "int", nullable: false),
                    ValorResidual = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 1m),
                    OrigenPrecio = table.Column<byte>(type: "tinyint", nullable: false),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CodigoExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HashContenido = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    UltimaSyncUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivoAmortizable", x => x.Id);
                    table.CheckConstraint("CK_ActivoAmortizable_MesesVidaUtil", "[MesesVidaUtil] > 0");
                    table.ForeignKey(
                        name: "FK_ActivoAmortizable_CategoriaProducto_CategoriaId",
                        column: x => x.CategoriaId,
                        principalSchema: "dbo",
                        principalTable: "CategoriaProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivoAmortizable_OrigenPrecioCatalogo_OrigenPrecio",
                        column: x => x.OrigenPrecio,
                        principalSchema: "dbo",
                        principalTable: "OrigenPrecioCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivoAmortizable_UnidadMedida_UnidadMedidaId",
                        column: x => x.UnidadMedidaId,
                        principalSchema: "dbo",
                        principalTable: "UnidadMedida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Producto",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CategoriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnidadMedidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CostoActual = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PrecioVentaActual = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    VidaUtilMeses = table.Column<int>(type: "int", nullable: true),
                    OrigenPrecio = table.Column<byte>(type: "tinyint", nullable: false),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CodigoExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HashContenido = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    UltimaSyncUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Producto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Producto_CategoriaProducto_CategoriaId",
                        column: x => x.CategoriaId,
                        principalSchema: "dbo",
                        principalTable: "CategoriaProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Producto_OrigenPrecioCatalogo_OrigenPrecio",
                        column: x => x.OrigenPrecio,
                        principalSchema: "dbo",
                        principalTable: "OrigenPrecioCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Producto_UnidadMedida_UnidadMedidaId",
                        column: x => x.UnidadMedidaId,
                        principalSchema: "dbo",
                        principalTable: "UnidadMedida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TipoServicio",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UnidadMedidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EsResidencialPorDefecto = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EsEmpresarialPorDefecto = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    OrigenDatoId = table.Column<byte>(type: "tinyint", nullable: false),
                    CodigoExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HashContenido = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    UltimaSyncUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoServicio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TipoServicio_OrigenDatoCatalogo_OrigenDatoId",
                        column: x => x.OrigenDatoId,
                        principalSchema: "dbo",
                        principalTable: "OrigenDatoCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TipoServicio_UnidadMedida_UnidadMedidaId",
                        column: x => x.UnidadMedidaId,
                        principalSchema: "dbo",
                        principalTable: "UnidadMedida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrecioActivo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ActivoAmortizableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VigenteDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenteHasta = table.Column<DateOnly>(type: "date", nullable: true),
                    Costo = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    OrigenPrecio = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrecioActivo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrecioActivo_ActivoAmortizable_ActivoAmortizableId",
                        column: x => x.ActivoAmortizableId,
                        principalSchema: "dbo",
                        principalTable: "ActivoAmortizable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PrecioActivo_OrigenPrecioCatalogo_OrigenPrecio",
                        column: x => x.OrigenPrecio,
                        principalSchema: "dbo",
                        principalTable: "OrigenPrecioCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrecioProducto",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ProductoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VigenteDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenteHasta = table.Column<DateOnly>(type: "date", nullable: true),
                    Costo = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PrecioVenta = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    OrigenPrecio = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrecioProducto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrecioProducto_OrigenPrecioCatalogo_OrigenPrecio",
                        column: x => x.OrigenPrecio,
                        principalSchema: "dbo",
                        principalTable: "OrigenPrecioCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrecioProducto_Producto_ProductoId",
                        column: x => x.ProductoId,
                        principalSchema: "dbo",
                        principalTable: "Producto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cotizacion",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Anio = table.Column<short>(type: "smallint", nullable: false),
                    Version = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    VersionAnteriorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EsVersionActual = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoServicioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoCliente = table.Column<byte>(type: "tinyint", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    VigenciaDias = table.Column<short>(type: "smallint", nullable: true),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    Modalidad = table.Column<byte>(type: "tinyint", nullable: true),
                    TecnicoAsignadoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FechaEstimada = table.Column<DateOnly>(type: "date", nullable: true),
                    MargenAplicado = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    MargenOrigen = table.Column<byte>(type: "tinyint", nullable: false),
                    MetodoCosteo = table.Column<byte>(type: "tinyint", nullable: false),
                    TipoCambioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TipoCambioValor = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    TipoCambioFecha = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalInsumosSemestre1 = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    TotalInsumosSemestre2 = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    TotalInsumos = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    TotalAmortizablesMensual = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CostoTotal = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PrecioCordobas = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PrecioUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PorcentajePlantilla = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    HashCalculo = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    ParametrosCalculo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClienteNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClienteDocumento = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ContactoNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ContactoTelefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ContactoCorreo = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DireccionServicio = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Condiciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EnviadaUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    AprobadaUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    AprobadaPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AnuladaUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    AnuladaPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConvertidaUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    OrigenDatoId = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cotizacion", x => x.Id);
                    table.CheckConstraint("CK_Cotizacion_MargenAplicado", "[MargenAplicado] >= 0 AND [MargenAplicado] <= 1");
                    table.ForeignKey(
                        name: "FK_Cotizacion_Cliente_ClienteId",
                        column: x => x.ClienteId,
                        principalSchema: "dbo",
                        principalTable: "Cliente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cotizacion_Cotizacion_VersionAnteriorId",
                        column: x => x.VersionAnteriorId,
                        principalSchema: "dbo",
                        principalTable: "Cotizacion",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Cotizacion_EstadoCotizacionCatalogo_Estado",
                        column: x => x.Estado,
                        principalSchema: "dbo",
                        principalTable: "EstadoCotizacionCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Cotizacion_ModalidadCatalogo_Modalidad",
                        column: x => x.Modalidad,
                        principalSchema: "dbo",
                        principalTable: "ModalidadCatalogo",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Cotizacion_OrigenDatoCatalogo_OrigenDatoId",
                        column: x => x.OrigenDatoId,
                        principalSchema: "dbo",
                        principalTable: "OrigenDatoCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cotizacion_TipoCambio_TipoCambioId",
                        column: x => x.TipoCambioId,
                        principalSchema: "dbo",
                        principalTable: "TipoCambio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cotizacion_TipoServicio_TipoServicioId",
                        column: x => x.TipoServicioId,
                        principalSchema: "dbo",
                        principalTable: "TipoServicio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TipoServicioMargen",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TipoServicioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VigenteDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenteHasta = table.Column<DateOnly>(type: "date", nullable: true),
                    Margen = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    OrigenDatoId = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoServicioMargen", x => x.Id);
                    table.CheckConstraint("CK_TipoServicioMargen_Margen", "[Margen] >= 0 AND [Margen] <= 1");
                    table.CheckConstraint("CK_TipoServicioMargen_Vigencia", "[VigenteHasta] IS NULL OR [VigenteHasta] >= [VigenteDesde]");
                    table.ForeignKey(
                        name: "FK_TipoServicioMargen_OrigenDatoCatalogo_OrigenDatoId",
                        column: x => x.OrigenDatoId,
                        principalSchema: "dbo",
                        principalTable: "OrigenDatoCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TipoServicioMargen_TipoServicio_TipoServicioId",
                        column: x => x.TipoServicioId,
                        principalSchema: "dbo",
                        principalTable: "TipoServicio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CotizacionAprobacion",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CotizacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Secuencia = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    Nivel = table.Column<byte>(type: "tinyint", nullable: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    MotivoDisparo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MargenDisparador = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    MontoDisparador = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    SolicitadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitadoUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Aprobador = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NombreAprobador = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CorreoAprobador = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RespuestaPorToken = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    FechaRespuesta = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Comentario = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    TokenExpira = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    TokenUsadoUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CotizacionAprobacion", x => x.Id);
                    table.CheckConstraint("CK_Aprobacion_Secuencia", "[Secuencia] >= 1");
                    table.ForeignKey(
                        name: "FK_CotizacionAprobacion_Cotizacion_CotizacionId",
                        column: x => x.CotizacionId,
                        principalSchema: "dbo",
                        principalTable: "Cotizacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CotizacionAprobacion_EstadoAprobacionCatalogo_Estado",
                        column: x => x.Estado,
                        principalSchema: "dbo",
                        principalTable: "EstadoAprobacionCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);

                    // §6.11, la otra mitad de la UDF: la regla de que un estado resuelto trae
                    // fecha. Va como CHECK y no como validación de la aplicación porque tiene que
                    // cumplirse también para lo que escriba un script, una migración o el propio
                    // ERP de solo lectura. Del lado de la aplicación, además, el estado llega como
                    // un byte y no como un código legible, así que no podría decidir sin ir a
                    // consultar el catálogo en cada respuesta de aprobación.
                    table.CheckConstraint(
                        "CK_Aprobacion_EstadoResueltoTieneFecha",
                        "dbo.FnEsEstadoFinalAprobacion([Estado]) = 0 OR [FechaRespuesta] IS NOT NULL");
                });

            migrationBuilder.CreateTable(
                name: "DetalleAmortizable",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CotizacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivoAmortizableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    MesesVidaUtil = table.Column<int>(type: "int", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    ValorMensual = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false, defaultValue: 0m),
                    UnidadMedidaTexto = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    ActivoNombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Orden = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Observacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetalleAmortizable", x => x.Id);
                    table.CheckConstraint("CK_DetalleAmortizable_Cantidad", "[Cantidad] > 0");
                    table.CheckConstraint("CK_DetalleAmortizable_MesesVidaUtil", "[MesesVidaUtil] > 0");
                    table.CheckConstraint("CK_DetalleAmortizable_PrecioUnitario", "[PrecioUnitario] >= 0");
                    table.CheckConstraint("CK_DetalleAmortizable_ValorMensual", "[ValorMensual] >= 0");
                    table.ForeignKey(
                        name: "FK_DetalleAmortizable_ActivoAmortizable_ActivoAmortizableId",
                        column: x => x.ActivoAmortizableId,
                        principalSchema: "dbo",
                        principalTable: "ActivoAmortizable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DetalleAmortizable_Cotizacion_CotizacionId",
                        column: x => x.CotizacionId,
                        principalSchema: "dbo",
                        principalTable: "Cotizacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DetalleInsumo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CotizacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Semestre = table.Column<byte>(type: "tinyint", nullable: true),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    UnidadMedidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnidadMedidaTexto = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    ProductoNombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Orden = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Observacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetalleInsumo", x => x.Id);
                    table.CheckConstraint("CK_DetalleInsumo_Cantidad", "[Cantidad] > 0");
                    table.CheckConstraint("CK_DetalleInsumo_PrecioUnitario", "[PrecioUnitario] >= 0");
                    table.CheckConstraint("CK_DetalleInsumo_Total", "[Total] >= 0");
                    table.ForeignKey(
                        name: "FK_DetalleInsumo_Cotizacion_CotizacionId",
                        column: x => x.CotizacionId,
                        principalSchema: "dbo",
                        principalTable: "Cotizacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetalleInsumo_Producto_ProductoId",
                        column: x => x.ProductoId,
                        principalSchema: "dbo",
                        principalTable: "Producto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DetalleInsumo_SemestreCatalogo_Semestre",
                        column: x => x.Semestre,
                        principalSchema: "dbo",
                        principalTable: "SemestreCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_AccionAuditoriaCatalogo_Codigo",
                schema: "dbo",
                table: "AccionAuditoriaCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivoAmortizable_CategoriaId",
                schema: "dbo",
                table: "ActivoAmortizable",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivoAmortizable_OrigenPrecio",
                schema: "dbo",
                table: "ActivoAmortizable",
                column: "OrigenPrecio");

            migrationBuilder.CreateIndex(
                name: "IX_ActivoAmortizable_UnidadMedidaId",
                schema: "dbo",
                table: "ActivoAmortizable",
                column: "UnidadMedidaId");

            migrationBuilder.CreateIndex(
                name: "UX_ActivoAmortizable_Codigo",
                schema: "dbo",
                table: "ActivoAmortizable",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoriaProducto_OrigenDatoId",
                schema: "dbo",
                table: "CategoriaProducto",
                column: "OrigenDatoId");

            migrationBuilder.CreateIndex(
                name: "UX_CategoriaProducto_Codigo",
                schema: "dbo",
                table: "CategoriaProducto",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cliente_Nombre",
                schema: "dbo",
                table: "Cliente",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_Cliente_OrigenDatoId",
                schema: "dbo",
                table: "Cliente",
                column: "OrigenDatoId");

            migrationBuilder.CreateIndex(
                name: "IX_Cliente_TipoCliente",
                schema: "dbo",
                table: "Cliente",
                column: "TipoCliente");

            migrationBuilder.CreateIndex(
                name: "UX_Cliente_Codigo",
                schema: "dbo",
                table: "Cliente",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Consecutivo_Tipo_Anio",
                schema: "dbo",
                table: "Consecutivo",
                columns: new[] { "Tipo", "Anio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cotizacion_Cliente_Fecha",
                schema: "dbo",
                table: "Cotizacion",
                columns: new[] { "ClienteId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Cotizacion_Estado_Fecha",
                schema: "dbo",
                table: "Cotizacion",
                columns: new[] { "Estado", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Cotizacion_Modalidad",
                schema: "dbo",
                table: "Cotizacion",
                column: "Modalidad");

            migrationBuilder.CreateIndex(
                name: "IX_Cotizacion_OrigenDatoId",
                schema: "dbo",
                table: "Cotizacion",
                column: "OrigenDatoId");

            migrationBuilder.CreateIndex(
                name: "IX_Cotizacion_TipoCambioId",
                schema: "dbo",
                table: "Cotizacion",
                column: "TipoCambioId");

            migrationBuilder.CreateIndex(
                name: "IX_Cotizacion_TipoServicioId",
                schema: "dbo",
                table: "Cotizacion",
                column: "TipoServicioId");

            migrationBuilder.CreateIndex(
                name: "IX_Cotizacion_VersionAnteriorId",
                schema: "dbo",
                table: "Cotizacion",
                column: "VersionAnteriorId");

            migrationBuilder.CreateIndex(
                name: "UX_Cotizacion_Numero_Version",
                schema: "dbo",
                table: "Cotizacion",
                columns: new[] { "Numero", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Aprobacion_Cotizacion",
                schema: "dbo",
                table: "CotizacionAprobacion",
                columns: new[] { "CotizacionId", "Secuencia" });

            migrationBuilder.CreateIndex(
                name: "IX_Aprobacion_Pendientes",
                schema: "dbo",
                table: "CotizacionAprobacion",
                columns: new[] { "Estado", "SolicitadoUtc" })
                .Annotation("SqlServer:Include", new[] { "CotizacionId", "NombreAprobador", "CorreoAprobador" });

            migrationBuilder.CreateIndex(
                name: "UX_Aprobacion_TokenHash",
                schema: "dbo",
                table: "CotizacionAprobacion",
                column: "TokenHash",
                unique: true,
                filter: "[TokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DetalleAmortizable_Activo",
                schema: "dbo",
                table: "DetalleAmortizable",
                column: "ActivoAmortizableId");

            migrationBuilder.CreateIndex(
                name: "IX_DetalleAmortizable_Cotizacion",
                schema: "dbo",
                table: "DetalleAmortizable",
                columns: new[] { "CotizacionId", "Orden" })
                .Annotation("SqlServer:Include", new[] { "ValorMensual", "Total", "MesesVidaUtil" });

            migrationBuilder.CreateIndex(
                name: "IX_DetalleInsumo_Cotizacion",
                schema: "dbo",
                table: "DetalleInsumo",
                columns: new[] { "CotizacionId", "Semestre" })
                .Annotation("SqlServer:Include", new[] { "Total", "Cantidad", "PrecioUnitario" });

            migrationBuilder.CreateIndex(
                name: "IX_DetalleInsumo_Producto",
                schema: "dbo",
                table: "DetalleInsumo",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_DetalleInsumo_Semestre",
                schema: "dbo",
                table: "DetalleInsumo",
                column: "Semestre");

            migrationBuilder.CreateIndex(
                name: "IX_Documento_Cotizacion",
                schema: "dbo",
                table: "DocumentoGenerado",
                columns: new[] { "CotizacionId", "Tipo" });

            migrationBuilder.CreateIndex(
                name: "UX_EstadoAprobacionCatalogo_Codigo",
                schema: "dbo",
                table: "EstadoAprobacionCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_EstadoCotizacionCatalogo_Codigo",
                schema: "dbo",
                table: "EstadoCotizacionCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_EstadoNotificacionCatalogo_Codigo",
                schema: "dbo",
                table: "EstadoNotificacionCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_FuenteTipoCambioCatalogo_Codigo",
                schema: "dbo",
                table: "FuenteTipoCambioCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_MetodoCosteoCatalogo_Codigo",
                schema: "dbo",
                table: "MetodoCosteoCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ModalidadCatalogo_Codigo",
                schema: "dbo",
                table: "ModalidadCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ModuloPermisoCatalogo_Codigo",
                schema: "dbo",
                table: "ModuloPermisoCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_NivelAccesoCatalogo_Codigo",
                schema: "dbo",
                table: "NivelAccesoCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_NivelAprobacionCatalogo_Codigo",
                schema: "dbo",
                table: "NivelAprobacionCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notificacion_Estado",
                schema: "dbo",
                table: "NotificacionEnviada",
                columns: new[] { "Estado", "Tipo" });

            migrationBuilder.CreateIndex(
                name: "UX_OrigenDatoCatalogo_Codigo",
                schema: "dbo",
                table: "OrigenDatoCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_OrigenMargenCatalogo_Codigo",
                schema: "dbo",
                table: "OrigenMargenCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_OrigenPrecioCatalogo_Codigo",
                schema: "dbo",
                table: "OrigenPrecioCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParametroGeneral_TipoDato",
                schema: "dbo",
                table: "ParametroGeneral",
                column: "TipoDato");

            migrationBuilder.CreateIndex(
                name: "UX_ParametroGeneral_Nombre",
                schema: "dbo",
                table: "ParametroGeneral",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permiso_ModuloId",
                schema: "seg",
                table: "Permiso",
                column: "ModuloId");

            migrationBuilder.CreateIndex(
                name: "UX_Permiso_Codigo",
                schema: "seg",
                table: "Permiso",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrecioActivo_OrigenPrecio",
                schema: "dbo",
                table: "PrecioActivo",
                column: "OrigenPrecio");

            migrationBuilder.CreateIndex(
                name: "UX_PrecioActivo_Activo_Inicio",
                schema: "dbo",
                table: "PrecioActivo",
                columns: new[] { "ActivoAmortizableId", "VigenteDesde" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrecioProducto_OrigenPrecio",
                schema: "dbo",
                table: "PrecioProducto",
                column: "OrigenPrecio");

            migrationBuilder.CreateIndex(
                name: "UX_PrecioProducto_Producto_Inicio",
                schema: "dbo",
                table: "PrecioProducto",
                columns: new[] { "ProductoId", "VigenteDesde" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Producto_CategoriaId",
                schema: "dbo",
                table: "Producto",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Producto_Nombre",
                schema: "dbo",
                table: "Producto",
                columns: new[] { "Nombre", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Producto_OrigenPrecio",
                schema: "dbo",
                table: "Producto",
                column: "OrigenPrecio");

            migrationBuilder.CreateIndex(
                name: "IX_Producto_UnidadMedidaId",
                schema: "dbo",
                table: "Producto",
                column: "UnidadMedidaId");

            migrationBuilder.CreateIndex(
                name: "UX_Producto_Codigo",
                schema: "dbo",
                table: "Producto",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Accion",
                schema: "aud",
                table: "RegistroAuditoria",
                columns: new[] { "Accion", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Entidad",
                schema: "aud",
                table: "RegistroAuditoria",
                columns: new[] { "Entidad", "EntidadId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Fecha",
                schema: "aud",
                table: "RegistroAuditoria",
                column: "FechaUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Usuario",
                schema: "aud",
                table: "RegistroAuditoria",
                columns: new[] { "UsuarioId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Rol_NivelAccesoId",
                schema: "seg",
                table: "Rol",
                column: "NivelAccesoId");

            migrationBuilder.CreateIndex(
                name: "UX_Rol_NormalizedName",
                schema: "seg",
                table: "Rol",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RolClaim_RoleId",
                schema: "seg",
                table: "RolClaim",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RolPermiso_PermisoId",
                schema: "seg",
                table: "RolPermiso",
                column: "PermisoId");

            migrationBuilder.CreateIndex(
                name: "UX_SemestreCatalogo_Codigo",
                schema: "dbo",
                table: "SemestreCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TipoCambio_Fuente",
                schema: "dbo",
                table: "TipoCambio",
                column: "Fuente");

            migrationBuilder.CreateIndex(
                name: "IX_TipoCambio_OrigenDatoId",
                schema: "dbo",
                table: "TipoCambio",
                column: "OrigenDatoId");

            migrationBuilder.CreateIndex(
                name: "UX_TipoCambio_Fecha_Fuente",
                schema: "dbo",
                table: "TipoCambio",
                columns: new[] { "Fecha", "Fuente" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TipoClienteCatalogo_MetodoCosteoId",
                schema: "dbo",
                table: "TipoClienteCatalogo",
                column: "MetodoCosteoId");

            migrationBuilder.CreateIndex(
                name: "UX_TipoClienteCatalogo_Codigo",
                schema: "dbo",
                table: "TipoClienteCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_TipoConsecutivoCatalogo_Codigo",
                schema: "dbo",
                table: "TipoConsecutivoCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_TipoDatoParametroCatalogo_Codigo",
                schema: "dbo",
                table: "TipoDatoParametroCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_TipoDocumentoCatalogo_Codigo",
                schema: "dbo",
                table: "TipoDocumentoCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_TipoNotificacionCatalogo_Codigo",
                schema: "dbo",
                table: "TipoNotificacionCatalogo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TipoServicio_OrigenDatoId",
                schema: "dbo",
                table: "TipoServicio",
                column: "OrigenDatoId");

            migrationBuilder.CreateIndex(
                name: "IX_TipoServicio_UnidadMedidaId",
                schema: "dbo",
                table: "TipoServicio",
                column: "UnidadMedidaId");

            migrationBuilder.CreateIndex(
                name: "UX_TipoServicio_Codigo",
                schema: "dbo",
                table: "TipoServicio",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TipoServicioMargen_OrigenDatoId",
                schema: "dbo",
                table: "TipoServicioMargen",
                column: "OrigenDatoId");

            migrationBuilder.CreateIndex(
                name: "UX_TipoServicioMargen_Inicio",
                schema: "dbo",
                table: "TipoServicioMargen",
                columns: new[] { "TipoServicioId", "VigenteDesde" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnidadMedida_OrigenDatoId",
                schema: "dbo",
                table: "UnidadMedida",
                column: "OrigenDatoId");

            migrationBuilder.CreateIndex(
                name: "UX_UnidadMedida_Codigo",
                schema: "dbo",
                table: "UnidadMedida",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Usuario_NormalizedEmail",
                schema: "seg",
                table: "Usuario",
                column: "NormalizedEmail",
                unique: true,
                filter: "[NormalizedEmail] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Usuario_NormalizedUserName",
                schema: "seg",
                table: "Usuario",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioClaim_UserId",
                schema: "seg",
                table: "UsuarioClaim",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioLogin_UserId",
                schema: "seg",
                table: "UsuarioLogin",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioPermiso_PermisoId",
                schema: "seg",
                table: "UsuarioPermiso",
                column: "PermisoId");

            migrationBuilder.CreateIndex(
                name: "UX_UsuarioPermiso_Usuario_Permiso",
                schema: "seg",
                table: "UsuarioPermiso",
                columns: new[] { "UsuarioId", "PermisoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioRol_RolId",
                schema: "seg",
                table: "UsuarioRol",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioToken_LoginProvider",
                schema: "seg",
                table: "UsuarioToken",
                column: "LoginProvider");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El orden de estas dos líneas no es estética, es una restricción de SQL Server, y las
            // dos jalan en direcciones opuestas:
            //
            //   · La función es SCHEMABINDING: no se puede quitar dbo.EstadoAprobacionCatalogo
            //     mientras exista una función atada a ella. Así que la función va ANTES de los
            //     DropTable.
            //   · El CHECK llama a la función: no se puede quitar la función mientras exista el
            //     CHECK que la referencia. Así que el CHECK va ANTES de la función.
            //
            // Encadenadas dan: CHECK, luego función, luego tablas. Al revés de cualquiera de los
            // otros dos ordenes, la reversa falla y deja la base a medias, en un estado que no
            // corresponde a ninguna migración y que solo se arregla a mano.
            migrationBuilder.Sql(
                "ALTER TABLE dbo.CotizacionAprobacion DROP CONSTRAINT IF EXISTS " +
                "CK_Aprobacion_EstadoResueltoTieneFecha;");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS dbo.FnEsEstadoFinalAprobacion;");

            migrationBuilder.DropTable(
                name: "AccionAuditoriaCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Consecutivo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CotizacionAprobacion",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DetalleAmortizable",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DetalleInsumo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "DocumentoGenerado",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EstadoNotificacionCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "NivelAprobacionCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "NotificacionEnviada",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OrigenMargenCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ParametroGeneral",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PrecioActivo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PrecioProducto",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RegistroAuditoria",
                schema: "aud");

            migrationBuilder.DropTable(
                name: "RolClaim",
                schema: "seg");

            migrationBuilder.DropTable(
                name: "RolPermiso",
                schema: "seg");

            migrationBuilder.DropTable(
                name: "TipoDocumentoCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TipoNotificacionCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TipoServicioMargen",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UsuarioClaim",
                schema: "seg");

            migrationBuilder.DropTable(
                name: "UsuarioLogin",
                schema: "seg");

            migrationBuilder.DropTable(
                name: "UsuarioPermiso",
                schema: "seg");

            migrationBuilder.DropTable(
                name: "UsuarioRol",
                schema: "seg");

            migrationBuilder.DropTable(
                name: "UsuarioToken",
                schema: "seg");

            migrationBuilder.DropTable(
                name: "TipoConsecutivoCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EstadoAprobacionCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Cotizacion",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SemestreCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TipoDatoParametroCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ActivoAmortizable",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Producto",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Permiso",
                schema: "seg");

            migrationBuilder.DropTable(
                name: "Rol",
                schema: "seg");

            migrationBuilder.DropTable(
                name: "Usuario",
                schema: "seg");

            migrationBuilder.DropTable(
                name: "Cliente",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EstadoCotizacionCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ModalidadCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TipoCambio",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TipoServicio",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CategoriaProducto",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OrigenPrecioCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ModuloPermisoCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "NivelAccesoCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TipoClienteCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "FuenteTipoCambioCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UnidadMedida",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MetodoCosteoCatalogo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OrigenDatoCatalogo",
                schema: "dbo");
        }
    }
}
