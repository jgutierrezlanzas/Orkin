using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orkin.Cotizador.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RetiroEspejosErp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cliente",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PrecioActivo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PrecioProducto",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SincronizacionEjecucion",
                schema: "int");

            migrationBuilder.DropTable(
                name: "Tecnico");

            migrationBuilder.DropTable(
                name: "TipoServicioMargen",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ActivoAmortizable",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Producto",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ErpFlujo",
                schema: "int");

            migrationBuilder.DropTable(
                name: "TipoServicio",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CategoriaProducto",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UnidadMedida",
                schema: "dbo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoriaProducto",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CodigoExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    HashContenido = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    OrigenDatoId = table.Column<byte>(type: "tinyint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UltimaSyncUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriaProducto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoriaProducto_ErpConexion_ErpConexionId",
                        column: x => x.ErpConexionId,
                        principalSchema: "int",
                        principalTable: "ErpConexion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CategoriaProducto_OrigenDatoCatalogo_OrigenDatoId",
                        column: x => x.OrigenDatoId,
                        principalSchema: "dbo",
                        principalTable: "OrigenDatoCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cliente",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TipoCliente = table.Column<byte>(type: "tinyint", nullable: false),
                    CedulaORuc = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Ciudad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Codigo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CodigoExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContactoCargo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContactoNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Departamento = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HashContenido = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    IdentificacionFiscal = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OrigenDatoId = table.Column<byte>(type: "tinyint", nullable: false),
                    RazonSocial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SitioWeb = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    UltimaSyncUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cliente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cliente_ErpConexion_ErpConexionId",
                        column: x => x.ErpConexionId,
                        principalSchema: "int",
                        principalTable: "ErpConexion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "ErpFlujo",
                schema: "int",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Catalogo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CorridaEnCursoUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DesfaseSegundos = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    EsPausado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    FallosConsecutivos = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    IntervaloSegundos = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MapeoJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaximoFallosConsecutivos = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)5),
                    MaximoRegistrosPorCorrida = table.Column<int>(type: "int", nullable: true),
                    ModoExtraccion = table.Column<byte>(type: "tinyint", nullable: false),
                    MotivoPausa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ProximaEjecucionUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TipoDisparador = table.Column<byte>(type: "tinyint", nullable: false),
                    UltimaEjecucionUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UltimoError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UltimoEstado = table.Column<byte>(type: "tinyint", nullable: true),
                    UltimoIntentoUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UltimosActualizados = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UltimosInsertados = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UltimosOmitidos = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UltimosRechazados = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpFlujo", x => x.Id);
                    table.CheckConstraint("CK_ErpFlujo_Desfase", "[DesfaseSegundos] BETWEEN 0 AND 30");
                    table.CheckConstraint("CK_ErpFlujo_Fallos", "[MaximoFallosConsecutivos] > 0");
                    table.CheckConstraint("CK_ErpFlujo_IntervaloMinimo", "[IntervaloSegundos] IS NULL OR [IntervaloSegundos] >= 10");
                    table.ForeignKey(
                        name: "FK_ErpFlujo_ErpConexion_ErpConexionId",
                        column: x => x.ErpConexionId,
                        principalSchema: "int",
                        principalTable: "ErpConexion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tecnico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cargo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Codigo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CodigoExterno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ColorCalendario = table.Column<string>(type: "varchar(7)", unicode: false, maxLength: 7, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false),
                    HashContenido = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    OrigenDatoId = table.Column<byte>(type: "tinyint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UltimaSyncUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tecnico", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnidadMedida",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Abreviatura = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CodigoExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    HashContenido = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    OrigenDatoId = table.Column<byte>(type: "tinyint", nullable: false),
                    PermiteDecimales = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UltimaSyncUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnidadMedida", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnidadMedida_ErpConexion_ErpConexionId",
                        column: x => x.ErpConexionId,
                        principalSchema: "int",
                        principalTable: "ErpConexion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnidadMedida_OrigenDatoCatalogo_OrigenDatoId",
                        column: x => x.OrigenDatoId,
                        principalSchema: "dbo",
                        principalTable: "OrigenDatoCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SincronizacionEjecucion",
                schema: "int",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ErpFlujoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DuracionMs = table.Column<int>(type: "int", nullable: true),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    FinUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    InicioUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    MensajeError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RegistrosActualizados = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RegistrosCreados = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RegistrosDesactivados = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RegistrosLeidos = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RegistrosRechazados = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RegistrosSinCambio = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SolicitadoPor = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TipoDisparador = table.Column<byte>(type: "tinyint", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "ActivoAmortizable",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CategoriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnidadMedidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 1m),
                    Codigo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CodigoExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CostoActual = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HashContenido = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MesesVidaUtil = table.Column<int>(type: "int", nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    OrigenPrecio = table.Column<byte>(type: "tinyint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UltimaSyncUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValorResidual = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
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
                        name: "FK_ActivoAmortizable_ErpConexion_ErpConexionId",
                        column: x => x.ErpConexionId,
                        principalSchema: "int",
                        principalTable: "ErpConexion",
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
                    CategoriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnidadMedidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CodigoExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CostoActual = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HashContenido = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Nombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    OrigenPrecio = table.Column<byte>(type: "tinyint", nullable: false),
                    PrecioVentaActual = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UltimaSyncUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VidaUtilMeses = table.Column<int>(type: "int", nullable: true)
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
                        name: "FK_Producto_ErpConexion_ErpConexionId",
                        column: x => x.ErpConexionId,
                        principalSchema: "int",
                        principalTable: "ErpConexion",
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
                    UnidadMedidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CodigoExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ErpConexionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EsEmpresarialPorDefecto = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EsResidencialPorDefecto = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EsSembrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    HashContenido = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    OrigenDatoId = table.Column<byte>(type: "tinyint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UltimaSyncUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoServicio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TipoServicio_ErpConexion_ErpConexionId",
                        column: x => x.ErpConexionId,
                        principalSchema: "int",
                        principalTable: "ErpConexion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    Costo = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    OrigenPrecio = table.Column<byte>(type: "tinyint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VigenteDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenteHasta = table.Column<DateOnly>(type: "date", nullable: true)
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
                    Costo = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    OrigenPrecio = table.Column<byte>(type: "tinyint", nullable: false),
                    PrecioVenta = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VigenteDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenteHasta = table.Column<DateOnly>(type: "date", nullable: true)
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
                name: "TipoServicioMargen",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TipoServicioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Margen = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    OrigenDatoId = table.Column<byte>(type: "tinyint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VigenteDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenteHasta = table.Column<DateOnly>(type: "date", nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_ActivoAmortizable_CategoriaId",
                schema: "dbo",
                table: "ActivoAmortizable",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivoAmortizable_ErpPendientes",
                schema: "dbo",
                table: "ActivoAmortizable",
                columns: new[] { "ErpConexionId", "HashContenido" });

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
                name: "UX_ActivoAmortizable_ErpCodigoExterno",
                schema: "dbo",
                table: "ActivoAmortizable",
                columns: new[] { "ErpConexionId", "CodigoExterno" },
                unique: true,
                filter: "[ErpConexionId] IS NOT NULL AND [CodigoExterno] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriaProducto_ErpConexionId",
                schema: "dbo",
                table: "CategoriaProducto",
                column: "ErpConexionId");

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
                name: "IX_Cliente_ErpPendientes",
                schema: "dbo",
                table: "Cliente",
                columns: new[] { "ErpConexionId", "HashContenido" });

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
                name: "UX_Cliente_ErpCodigoExterno",
                schema: "dbo",
                table: "Cliente",
                columns: new[] { "ErpConexionId", "CodigoExterno" },
                unique: true,
                filter: "[ErpConexionId] IS NOT NULL AND [CodigoExterno] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ErpFlujo_Programados",
                schema: "int",
                table: "ErpFlujo",
                column: "ProximaEjecucionUtc",
                filter: "[EsPausado] = 0 AND [IntervaloSegundos] IS NOT NULL AND [Estado] <> 1");

            migrationBuilder.CreateIndex(
                name: "UX_ErpFlujo_ConexionNombre",
                schema: "int",
                table: "ErpFlujo",
                columns: new[] { "ErpConexionId", "Nombre" },
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
                name: "IX_Producto_ErpPendientes",
                schema: "dbo",
                table: "Producto",
                columns: new[] { "ErpConexionId", "HashContenido" });

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
                name: "UX_Producto_ErpCodigoExterno",
                schema: "dbo",
                table: "Producto",
                columns: new[] { "ErpConexionId", "CodigoExterno" },
                unique: true,
                filter: "[ErpConexionId] IS NOT NULL AND [CodigoExterno] IS NOT NULL");

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

            migrationBuilder.CreateIndex(
                name: "IX_TipoServicio_ErpConexionId",
                schema: "dbo",
                table: "TipoServicio",
                column: "ErpConexionId");

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
                name: "IX_UnidadMedida_ErpConexionId",
                schema: "dbo",
                table: "UnidadMedida",
                column: "ErpConexionId");

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
        }
    }
}
