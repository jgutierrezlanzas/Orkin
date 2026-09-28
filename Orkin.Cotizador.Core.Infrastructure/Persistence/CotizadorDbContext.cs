using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;

namespace Orkin.Cotizador.Core.Infrastructure.Persistence;

/// <summary>
/// Contexto único sobre los cuatro esquemas (ADR-006): <c>dbo</c> (negocio), <c>seg</c>
/// (seguridad), <c>int</c> (integración, Fase 12) y <c>aud</c> (auditoría append-only).
/// <para><b>Sin <c>EFCore.NamingConventions</c></b> (ADR-006): los nombres van explícitos en cada
/// <c>ToTable</c>, en PascalCase singular. Es más trabajo al escribir la configuración, a cambio de
/// que el esquema generado coincide exactamente con <c>docs/BASE-DE-DATOS.md</c>, que es el
/// documento que revisa el cliente.</para>
/// <para>⚠️ <b>Nada de <c>EnsureCreated</c></b>: el esquema se crea con migraciones
/// (<c>dotnet ef database update</c>), nunca por código. Ver <c>DbInitializer</c>.</para>
/// <para>⚠️ <b>ADR-018</b>: este archivo describe la <i>forma</i> de las tablas, nunca sus filas.
/// Los catálogos y los parámetros se siembran desde <c>scripts/seed/*.json</c> (§5.20).</para>
/// </summary>
public class CotizadorDbContext : DbContext
{
    /// <summary>
    /// Collation de la base. Fijado en el modelo para que la migración no dependa de la collation
    /// que tenga el servidor donde se corrió <c>dotnet ef migrations add</c>.
    /// </summary>
    public const string Collation = "SQL_Latin1_General_CP1_CI_AS";

    // ── dbo · Negocio ────────────────────────────────────────────────────────────
    public DbSet<Cliente> Cliente => Set<Cliente>();
    public DbSet<Producto> Producto => Set<Producto>();
    public DbSet<PrecioProducto> PrecioProducto => Set<PrecioProducto>();
    public DbSet<ActivoAmortizable> ActivoAmortizable => Set<ActivoAmortizable>();
    public DbSet<PrecioActivo> PrecioActivo => Set<PrecioActivo>();
    public DbSet<TipoCambio> TipoCambio => Set<TipoCambio>();
    public DbSet<Cotizacion> Cotizacion => Set<Cotizacion>();
    public DbSet<DetalleInsumo> DetalleInsumo => Set<DetalleInsumo>();
    public DbSet<DetalleAmortizable> DetalleAmortizable => Set<DetalleAmortizable>();
    public DbSet<CotizacionAprobacion> CotizacionAprobacion => Set<CotizacionAprobacion>();
    public DbSet<ParametroGeneral> ParametroGeneral => Set<ParametroGeneral>();
    public DbSet<Consecutivo> Consecutivo => Set<Consecutivo>();
    public DbSet<NotificacionEnviada> NotificacionEnviada => Set<NotificacionEnviada>();
    public DbSet<DocumentoGenerado> DocumentoGenerado => Set<DocumentoGenerado>();

    // ── dbo · Catálogos maestros (editables desde Administración) ─────────────────
    public DbSet<UnidadMedida> UnidadMedida => Set<UnidadMedida>();
    public DbSet<CategoriaProducto> CategoriaProducto => Set<CategoriaProducto>();
    public DbSet<TipoServicio> TipoServicio => Set<TipoServicio>();
    public DbSet<TipoServicioMargen> TipoServicioMargen => Set<TipoServicioMargen>();

    // ── dbo · Catálogos de dominio (solo lectura, ADR-018) ──────────────────────
    public DbSet<MetodoCosteoCatalogo> MetodoCosteoCatalogo => Set<MetodoCosteoCatalogo>();
    public DbSet<TipoClienteCatalogo> TipoClienteCatalogo => Set<TipoClienteCatalogo>();
    public DbSet<EstadoCotizacionCatalogo> EstadoCotizacionCatalogo => Set<EstadoCotizacionCatalogo>();
    public DbSet<SemestreCatalogo> SemestreCatalogo => Set<SemestreCatalogo>();
    public DbSet<ModalidadCatalogo> ModalidadCatalogo => Set<ModalidadCatalogo>();
    public DbSet<OrigenDatoCatalogo> OrigenDatoCatalogo => Set<OrigenDatoCatalogo>();
    public DbSet<OrigenPrecioCatalogo> OrigenPrecioCatalogo => Set<OrigenPrecioCatalogo>();
    public DbSet<FuenteTipoCambioCatalogo> FuenteTipoCambioCatalogo => Set<FuenteTipoCambioCatalogo>();
    public DbSet<EstadoAprobacionCatalogo> EstadoAprobacionCatalogo => Set<EstadoAprobacionCatalogo>();
    public DbSet<NivelAprobacionCatalogo> NivelAprobacionCatalogo => Set<NivelAprobacionCatalogo>();
    public DbSet<OrigenMargenCatalogo> OrigenMargenCatalogo => Set<OrigenMargenCatalogo>();
    public DbSet<TipoNotificacionCatalogo> TipoNotificacionCatalogo => Set<TipoNotificacionCatalogo>();
    public DbSet<EstadoNotificacionCatalogo> EstadoNotificacionCatalogo => Set<EstadoNotificacionCatalogo>();
    public DbSet<TipoDocumentoCatalogo> TipoDocumentoCatalogo => Set<TipoDocumentoCatalogo>();
    public DbSet<AccionAuditoriaCatalogo> AccionAuditoriaCatalogo => Set<AccionAuditoriaCatalogo>();
    public DbSet<TipoDatoParametroCatalogo> TipoDatoParametroCatalogo => Set<TipoDatoParametroCatalogo>();
    public DbSet<TipoConsecutivoCatalogo> TipoConsecutivoCatalogo => Set<TipoConsecutivoCatalogo>();
    public DbSet<ModuloPermisoCatalogo> ModuloPermisoCatalogo => Set<ModuloPermisoCatalogo>();
    public DbSet<NivelAccesoCatalogo> NivelAccesoCatalogo => Set<NivelAccesoCatalogo>();

    // ── seg · Seguridad (D-02: propia, sin plataforma externa) ──────────────────
    public DbSet<Usuario> Usuario => Set<Usuario>();
    public DbSet<Rol> Rol => Set<Rol>();
    public DbSet<Permiso> Permiso => Set<Permiso>();
    public DbSet<RolPermiso> RolPermiso => Set<RolPermiso>();
    // Estas cinco son las tablas puente de Identity. La propiedad se llama como la tabla
    // (seg.UsuarioRol, seg.UsuarioClaim...) pero el tipo es el de Identity a proposito: UserStore
    // hace Set<IdentityUserRole<Guid>>() y el modelo empareja por el tipo de C# exacto, asi que
    // una clase propia con el nombre de la tabla no sirve. Ver ConfigurarSeguridad.
    public DbSet<IdentityUserRole<Guid>> UsuarioRol => Set<IdentityUserRole<Guid>>();
    public DbSet<IdentityUserClaim<Guid>> UsuarioClaim => Set<IdentityUserClaim<Guid>>();
    public DbSet<IdentityUserLogin<Guid>> UsuarioLogin => Set<IdentityUserLogin<Guid>>();
    public DbSet<IdentityUserToken<Guid>> UsuarioToken => Set<IdentityUserToken<Guid>>();
    public DbSet<IdentityRoleClaim<Guid>> RolClaim => Set<IdentityRoleClaim<Guid>>();
    public DbSet<UsuarioPermiso> UsuarioPermiso => Set<UsuarioPermiso>();

    // ── aud · Auditoría (append-only) ───────────────────────────────────────────
    public DbSet<RegistroAuditoria> RegistroAuditoria => Set<RegistroAuditoria>();

    public CotizadorDbContext(DbContextOptions<CotizadorDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// El aviso de EF sobre las claves <c>tinyint</c> de los catálogos, con su número.
    /// <para>EF no le pone constante pública a este evento: lo construye con
    /// <c>new EventId(30001)</c> en el punto donde lo emite, así que el número es la única forma
    /// de referenciarlo. Va en su propia línea y con nombre para que no se lea como un dato.</para>
    /// <para><b>Lo que dice el aviso:</b> para seguir una entidad recién insertada, EF le asigna
    /// una clave temporal y la reemplaza cuando la base devuelve la real. En una columna
    /// <c>tinyint</c> esas temporales van de 255 hacia abajo, así que un catálogo de 255 filas
    /// tendría un 255 real y una clave temporal que se le encima.</para>
    /// <para><b>Por qué se silencia:</b> el techo es real, pero ya lo vigila
    /// <c>AplicadorSemilla.TechoFilasPorCatalogoTinyint</c>, que corta la siembra con un mensaje
    /// que dice qué cambiar. Lo que queda del aviso de EF son 19 líneas idénticas en cada arranque,
    /// y un log que siempre tiene 19 líneas de ruido es un log del que nadie lee la 20.</para>
    /// <para><b>Cómo se revierte:</b> borrar la línea de abajo. No hay nada más que deshacer: el
    /// aviso no cambia el modelo, solo silencia lo que dice de él.</para>
    /// </summary>
    private static readonly EventId AvisoClaveTinyintTemporal = new(30001, "Clave tinyint con valores temporales de EF");

    /// <summary>
    /// El aviso de EF sobre las claves <c>tinyint</c> de los catálogos se silencia acá y no en
    /// <see cref="OnModelCreating"/> porque <c>ModelBuilder</c> no tiene con qué configurarlo:
    /// <c>ConfigureWarnings</c> es de <see cref="DbContextOptionsBuilder"/>. Va en
    /// <c>OnConfiguring</c>, que es el lugar donde se toca la configuración del proveedor, y
    /// funciona aunque las opciones vengan de la inyección de dependencias.
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.ConfigureWarnings(a => a.Ignore(AvisoClaveTinyintTemporal));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation(Collation);

        ConfigurarCatalogosDominio(modelBuilder);
        ConfigurarSeguridad(modelBuilder);
        ConfigurarAuditoria(modelBuilder);
        ConfigurarNegocio(modelBuilder);
        ConfigurarCatalogosMaestros(modelBuilder);
        ConfigurarSistema(modelBuilder);
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Helpers compartidos
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Columnas base de §3.1. Se aplica a toda tabla de negocio y de seguridad, nunca a las de
    /// solo lectura (catálogos de dominio y auditoría).
    /// </summary>
    private static void ConfigurarBase<TEntity>(EntityTypeBuilder<TEntity> builder, bool conIsActive = true)
        where TEntity : BaseEntity
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("CreatedAt")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(e => e.CreatedBy)
            .HasColumnName("CreatedBy");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("UpdatedAt")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("UpdatedBy");

        // IsActive se mapea solo si la tabla lo tiene, y el opt-out existe para no hacerlo.
        //
        // La otra forma de excluirlo sería .Ignore(...) después de ConfigurarBase, y funciona, pero
        // deja a EF avisando en cada arranque: "the property 'Cotizacion.IsActive' was first
        // mapped explicitly and then ignored". Mapeada una vez e ignorada después, la propiedad
        // termina sin columna igual, pero el modelo queda diciendo las dos cosas y el aviso enseña
        // a ignorar avisos, que es justo lo que hace que un aviso sirva.
        //
        // Las cuatro tablas que lo pierden son las que no pueden tener dos verdades: una cotización
        // se anula con su Estado, una línea pertenece a su cotización y una aprobación pertenece a
        // su etapa. Un IsActive al lado de su Estado es un segundo criterio para la misma pregunta,
        // y con dos criterios siempre gana el que alguien mira último.
        if (conIsActive)
        {
            builder.Property(e => e.IsActive)
                .HasColumnName("IsActive")
                .HasDefaultValue(true);
        }

        builder.Property(e => e.RowVersion)
            .HasColumnName("RowVersion")
            .IsRowVersion();
    }

    /// <summary>
    /// Las cuatro columnas de procedencia de un dato maestro (ADR-013): con qué clave en el ERP
    /// vino, con qué huella y cuándo se confirmó. Están en <c>Cliente</c>, <c>Producto</c>,
    /// <c>ActivoAmortizable</c>, <c>TipoCambio</c> y en los catálogos maestros.
    /// <para>Se resuelven por nombre de propiedad y no con un tipo genérico porque las entidades no
    /// comparten una clase base: <c>Producto</c> usa <c>OrigenPrecioId</c> y <c>Cliente</c> usa
    /// <c>OrigenDatoId</c>, y ese matiz es justamente el que hay que hacer explícito.</para>
    /// </summary>
    private static void ConfigurarProcedencia<T>(EntityTypeBuilder<T> builder)
        where T : class
    {
        builder.Property("CodigoExterno").HasColumnName("CodigoExterno").HasMaxLength(100);
        builder.Property("ErpConexionId").HasColumnName("ErpConexionId");
        builder.Property("HashContenido").HasColumnName("HashContenido").HasColumnType("binary(32)");
        builder.Property("UltimaSyncUtc").HasColumnName("UltimaSyncUtc").HasColumnType("datetime2(3)");
    }

    /// <summary>
    /// Declara la FK de procedencia: la propiedad <c>*Id</c> y su relación con el catálogo de origen.
    /// <para><b>Por qué no alcanza con <c>HasColumnName(...).IsRequired()</c>:</b> eso describe una
    /// columna, no una relación. Una columna <c>tinyint</c> sin relación no tiene restricción de
    /// integridad —nada impide escribir un <c>OrigenDatoId</c> que no existe— y además EF no la
    /// cuenta como clave foránea, que es lo que rompe dos cosas del_seed: la siembra escribe las
    /// FK con el <c>Codigo</c> del catálogo precisamente para que nada pueda quedar desalineado
    /// (§5.20), y esa resolución se dispara solo para las propiedades que EF reconoce como
    /// foráneas. Sin relación, la siembra lee <c>"MANUAL"</c> donde espera un número y falla.</para>
    /// <para><b>Por qué sin propiedad de navegación:</b> el valor de esta FK se usa para mostrar de
    /// dónde vino el dato, y eso se resuelve con un <c>join</c> explícito contra el catálogo.
    /// Declararla como relación sin navegación deja la integridad sin agregar una propiedad pública
    /// que ningún código va a usar; cuando aparezca la primera consulta que sí la necesite, se
    /// agrega la navegación y nada más cambia.</para>
    /// <para><b>Por qué el <c>Restrict</c>:</b> igual que las demás FK a catálogos. Un catálogo se
    /// da de baja con <c>EsActivo = 0</c> y nunca se borra (ADR-018), así que el <c>Restrict</c>
    /// nunca se va a disparar en la operación normal, pero si alguien borra una fila desde la
    /// consola, la base le dice que no en vez de dejar datos apuntando al vacío.</para>
    /// <para><b>Nota de nombres:</b> el nombre de columna no se deduce de la propiedad porque en el
    /// modelo conviven dos convenciones —<c>TipoCliente</c> sin sufijo, <c>CategoriaId</c> con
    /// sufijo— y esta llamada declara el nombre que ya tiene la tabla. Unificarlo es un cambio de
    /// esquema entero, no un detalle de este método.</para>
    /// <para><b>Por qué el nombre de la propiedad va como texto y no con <c>nameof</c>:</b> el
    /// contexto declara un <c>DbSet</c> por entidad con el nombre de la entidad, y dentro de estos
    /// métodos ese nombre tiene alcance: <c>nameof(Cliente.OrigenDatoId)</c> compila como si
    /// <c>Cliente</c> fuera el conjunto y no el tipo. Es el mismo motivo por el que
    /// <see cref="ConfigurarProcedencia{T}"/> recibe sus cuatro columnas como texto.</para>
    /// </summary>
    private static void ConfigurarFkOrigen<T>(
        EntityTypeBuilder<T> builder,
        string propiedadFk,
        string columna,
        Type entidadOrigen)
        where T : class
    {
        builder.Property(propiedadFk).HasColumnName(columna).IsRequired();

        builder.HasOne(entidadOrigen)
            .WithMany()
            .HasForeignKey(propiedadFk)
            .OnDelete(DeleteBehavior.Restrict);
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Catálogos de dominio (ADR-018, §5)
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Los 19 catálogos de dominio. Todos comparten la forma de §5.0, así que se configuran con un
    /// solo método genérico.
    /// <para>Lo que <b>no</b> hay aquí son sus filas: viven en <c>scripts/seed/*.json</c> y se aplican
    /// con <c>RUN_SEED = true</c> (§5.20). Agregar un valor es agregar una línea al JSON, no
    /// compilar.</para>
    /// </summary>
    private static void ConfigurarCatalogosDominio(ModelBuilder modelBuilder)
    {
        // La lista arranca por §5.1 y no por §5.2 a propósito. ConfigurarCatalogoDominio es lo que
        // le pone a la tabla el esquema, el IDENTITY, los anchos de nvarchar, los valores por
        // defecto y el índice único sobre Codigo, y es justo el índice único lo que la siembra usa
        // como clave natural. Un catálogo que se olvide de pasar por acá no falla al compilar: se
        // crea con los anchos por defecto, sin Identity y sin índice, y el síntoma aparece en la
        // primera siembra, cuando dos filas con el mismo Codigo entran sin que nada lo impida.
        ConfigurarCatalogoDominio(modelBuilder.Entity<TipoClienteCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<MetodoCosteoCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<EstadoCotizacionCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<SemestreCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<ModalidadCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<OrigenDatoCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<OrigenPrecioCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<FuenteTipoCambioCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<EstadoAprobacionCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<NivelAprobacionCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<OrigenMargenCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<TipoNotificacionCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<EstadoNotificacionCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<TipoDocumentoCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<AccionAuditoriaCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<TipoDatoParametroCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<TipoConsecutivoCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<ModuloPermisoCatalogo>());
        ConfigurarCatalogoDominio(modelBuilder.Entity<NivelAccesoCatalogo>());

        // §5.1 · El segmento apunta al método de costeo. Dice QUÉ método, no CÓMO se calcula.
        var tipoCliente = modelBuilder.Entity<TipoClienteCatalogo>();
        tipoCliente.Property(c => c.MetodoCosteoId).HasColumnName("MetodoCosteoId").IsRequired();
        tipoCliente.Property(c => c.EsPredeterminado).HasColumnName("EsPredeterminado").HasDefaultValue(false);
        tipoCliente.HasOne(c => c.MetodoCosteo)
            .WithMany()
            .HasForeignKey(c => c.MetodoCosteoId)
            .OnDelete(DeleteBehavior.Restrict);

        // §5.7 · La bandera que permite que el CHECK de §6.11 no lleve números dentro.
        modelBuilder.Entity<EstadoAprobacionCatalogo>()
            .Property(c => c.EsEstadoFinal)
            .HasColumnName("EsEstadoFinal")
            .HasDefaultValue(false);

        // §5.15 · El prefijo y el ancho de la numeración oficial viven en la tabla, no en un
        // formateador de C#.
        var tipoConsecutivo = modelBuilder.Entity<TipoConsecutivoCatalogo>();
        tipoConsecutivo.Property(c => c.Prefijo).HasColumnName("Prefijo").HasMaxLength(10).IsRequired();
        tipoConsecutivo.Property(c => c.Digitos).HasColumnName("Digitos").HasDefaultValue((short)4);
    }

    /// <summary>Aplica §5.0 a un catálogo de dominio: forma común, sin columnas de auditoría.</summary>
    private static void ConfigurarCatalogoDominio<T>(EntityTypeBuilder<T> builder)
        where T : CatalogoDominioBase
    {
        var tabla = typeof(T).Name;

        builder.ToTable(tabla, Esquemas.Negocio);

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("Id").ValueGeneratedOnAdd();

        builder.Property(c => c.Codigo).HasColumnName("Codigo").HasMaxLength(30).IsRequired();
        builder.Property(c => c.Nombre).HasColumnName("Nombre").HasMaxLength(100).IsRequired();
        builder.Property(c => c.Descripcion).HasColumnName("Descripcion").HasMaxLength(300);
        builder.Property(c => c.Orden).HasColumnName("Orden").HasDefaultValue((byte)0);
        builder.Property(c => c.EsActivo).HasColumnName("EsActivo").HasDefaultValue(true);
        builder.Property(c => c.EsSembrado).HasColumnName("EsSembrado").HasDefaultValue(true);

        builder.HasIndex(c => c.Codigo).IsUnique().HasDatabaseName($"UX_{tabla}_Codigo");
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Seguridad (esquema seg)
    // ═════════════════════════════════════════════════════════════════════════════

    private static void ConfigurarSeguridad(ModelBuilder modelBuilder)
    {
        // ── seg.Usuario ──────────────────────────────────────────────────────────
        // Es el IdentityUser<Guid> de ASP.NET Core Identity sobre nuestras propias tablas (D-02).
        // Identity define su propio esquema de nombres, así que hay que mapear columna por columna
        // a las de §8.1.
        var usuario = modelBuilder.Entity<Usuario>();
        usuario.ToTable("Usuario", Esquemas.Seguridad);
        usuario.HasKey(u => u.Id);
        usuario.Property(u => u.Id).HasColumnName("Id").ValueGeneratedOnAdd().HasDefaultValueSql("NEWSEQUENTIALID()");
        usuario.Property(u => u.UserName).HasColumnName("UserName").HasMaxLength(256);
        usuario.Property(u => u.NormalizedUserName).HasColumnName("NormalizedUserName").HasMaxLength(256);
        usuario.Property(u => u.Email).HasColumnName("Email").HasMaxLength(256);
        usuario.Property(u => u.NormalizedEmail).HasColumnName("NormalizedEmail").HasMaxLength(256);
        usuario.Property(u => u.EmailConfirmed).HasColumnName("EmailConfirmed").HasDefaultValue(false);
        usuario.Property(u => u.PasswordHash).HasColumnName("PasswordHash").HasMaxLength(512);
        usuario.Property(u => u.SecurityStamp).HasColumnName("SecurityStamp").HasMaxLength(128);
        usuario.Property(u => u.ConcurrencyStamp).HasColumnName("ConcurrencyStamp").HasMaxLength(128);
        usuario.Property(u => u.PhoneNumber).HasColumnName("PhoneNumber").HasMaxLength(30);
        usuario.Property(u => u.PhoneNumberConfirmed).HasColumnName("PhoneNumberConfirmed").HasDefaultValue(false);
        usuario.Property(u => u.TwoFactorEnabled).HasColumnName("TwoFactorEnabled").HasDefaultValue(false);
        usuario.Property(u => u.LockoutEnabled).HasColumnName("LockoutEnabled").HasDefaultValue(true);

        // El único punto de la base donde hay que traducir zona horaria, y el motivo es que
        // Identity no dejó elegir: LockoutEnd es DateTimeOffset? y el proveedor de SQL Server exige
        // datetimeoffset para ese tipo. Como en el resto del modelo las marcas son datetime2(3) en
        // UTC (ADR-014), la columna se guarda en UTC y se convierte de ida y vuelta. El par de
        // conversores tiene que ser simétrico: Identity compara LockoutEnd contra DateTimeOffset.UtcNow, y un
        // conversor que solo funcionara al escribir devolvería un desfase de horas en cada
        // comparación y el bloqueo de cuentas no se activaría nunca.
        usuario.Property(u => u.LockoutEnd)
            .HasColumnName("LockoutEnd")
            .HasColumnType("datetime2(3)")
            .HasConversion(valor => UtcAFechaNula(valor), valor => FechaNulaAUtc(valor));
        usuario.Property(u => u.AccessFailedCount).HasColumnName("AccessFailedCount").HasDefaultValue(0);
        usuario.Property(u => u.NombreCompleto).HasColumnName("NombreCompleto").HasMaxLength(200).IsRequired();
        usuario.Property(u => u.Documento).HasColumnName("Documento").HasMaxLength(30);
        usuario.Property(u => u.Cargo).HasColumnName("Cargo").HasMaxLength(100);
        usuario.Property(u => u.AvatarUrl).HasColumnName("AvatarUrl").HasMaxLength(450);
        usuario.Property(u => u.EsActivo).HasColumnName("EsActivo").HasDefaultValue(true);
        usuario.Property(u => u.EsUsuarioSistema).HasColumnName("EsUsuarioSistema").HasDefaultValue(false);
        usuario.Property(u => u.DebeCambiarContrasena).HasColumnName("DebeCambiarContrasena").HasDefaultValue(false);
        usuario.Property(u => u.UltimoAccesoUtc).HasColumnName("UltimoAccesoUtc").HasColumnType("datetime2(3)");
        usuario.Property(u => u.UltimoAccesoIp).HasColumnName("UltimoAccesoIp").HasMaxLength(45);
        usuario.Property(u => u.RowVersion).HasColumnName("RowVersion").IsRowVersion();
        usuario.Property(u => u.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        usuario.Property(u => u.CreatedBy).HasColumnName("CreatedBy");
        usuario.Property(u => u.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        usuario.Property(u => u.UpdatedBy).HasColumnName("UpdatedBy");
        usuario.HasIndex(u => u.NormalizedUserName).IsUnique().HasDatabaseName("UX_Usuario_NormalizedUserName");
        usuario.HasIndex(u => u.NormalizedEmail).IsUnique().HasDatabaseName("UX_Usuario_NormalizedEmail");

        // ── seg.Rol ──────────────────────────────────────────────────────────────
        var rol = modelBuilder.Entity<Rol>();
        rol.ToTable("Rol", Esquemas.Seguridad);
        rol.HasKey(r => r.Id);
        rol.Property(r => r.Id).HasColumnName("Id").ValueGeneratedOnAdd().HasDefaultValueSql("NEWSEQUENTIALID()");
        rol.Property(r => r.Name).HasColumnName("Name").HasMaxLength(256);
        rol.Property(r => r.NormalizedName).HasColumnName("NormalizedName").HasMaxLength(256);
        rol.Property(r => r.ConcurrencyStamp).HasColumnName("ConcurrencyStamp").HasMaxLength(128);
        rol.Property(r => r.Description).HasColumnName("Description").HasMaxLength(300);
        rol.Property(r => r.EsSistema).HasColumnName("EsSistema").HasDefaultValue(false);
        rol.Property(r => r.EsActivo).HasColumnName("EsActivo").HasDefaultValue(true);
        rol.Property(r => r.NivelAccesoId).HasColumnName("NivelAccesoId").IsRequired();
        rol.Property(r => r.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        rol.Property(r => r.CreatedBy).HasColumnName("CreatedBy");
        rol.Property(r => r.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        rol.Property(r => r.UpdatedBy).HasColumnName("UpdatedBy");
        rol.HasIndex(r => r.NormalizedName).IsUnique().HasDatabaseName("UX_Rol_NormalizedName");
        rol.HasOne(r => r.NivelAcceso).WithMany().HasForeignKey(r => r.NivelAccesoId).OnDelete(DeleteBehavior.Restrict);

        // ── seg.Permiso ──────────────────────────────────────────────────────────
        var permiso = modelBuilder.Entity<Permiso>();
        ConfigurarBase(permiso);
        permiso.ToTable("Permiso", Esquemas.Seguridad);
        permiso.Property(p => p.Codigo).HasColumnName("Codigo").HasMaxLength(100).IsRequired();
        permiso.Property(p => p.Nombre).HasColumnName("Nombre").HasMaxLength(150).IsRequired();
        permiso.Property(p => p.ModuloId).HasColumnName("ModuloId").IsRequired();
        permiso.Property(p => p.Descripcion).HasColumnName("Descripcion").HasMaxLength(500);
        permiso.Property(p => p.Orden).HasColumnName("Orden").HasDefaultValue((short)0);
        permiso.HasIndex(p => p.Codigo).IsUnique().HasDatabaseName("UX_Permiso_Codigo");
        permiso.HasOne(p => p.Modulo).WithMany().HasForeignKey(p => p.ModuloId).OnDelete(DeleteBehavior.Restrict);

        // ── Tablas puente ────────────────────────────────────────────────────────
        var rolPermiso = modelBuilder.Entity<RolPermiso>();
        rolPermiso.ToTable("RolPermiso", Esquemas.Seguridad);
        rolPermiso.HasKey(rp => new { rp.RolId, rp.PermisoId });
        rolPermiso.Property(rp => rp.AsignadoUtc).HasColumnName("AsignadoUtc").HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        rolPermiso.Property(rp => rp.AsignadoPor).HasColumnName("AsignadoPor");
        rolPermiso.HasOne(rp => rp.Rol).WithMany(r => r.Permisos).HasForeignKey(rp => rp.RolId).OnDelete(DeleteBehavior.Cascade);
        rolPermiso.HasOne(rp => rp.Permiso).WithMany(p => p.Roles).HasForeignKey(rp => rp.PermisoId).OnDelete(DeleteBehavior.Cascade);

        // ── Tablas puente de Identity ───────────────────────────────────────────────────────────
        // Identity no permite cambiar el tipo de sus tablas puente, y no por capricho: UserStore las
        // busca por el nombre del tipo de C# y no por el de la tabla. Eso tiene dos consecuencias
        // que conviene tener presentes al tocar este bloque.
        //
        // La primera es que no se pueden agregar columnas propias a seg.UsuarioRol, y por eso no
        // lleva AsignadoUtc ni AsignadoPor. No es una perdida: aud.RegistroAuditoria ya responde
        // quien asigno cada rol y cuando, con la ventaja de que responde tambien por las
        // revocaciones, que una columna de la tabla puente no recordaria.
        //
        // Un detalle de .NET 10 que conviene no olvidar: IdentityUser<TKey> e IdentityRole<TKey>
        // ya no traen las colecciones Roles, Claims, Logins ni Tokens. Antes el modelo las
        // descubria solo por herencia; ahora hay que exponer los DbSet y declarar las relaciones a
        // mano, y por eso ninguna de las cinco usa WithMany(con navegacion). Si se cambia alguna
        // por .WithMany() sin argumento no pasa nada hoy, pero el lado inverso se pierde en
        // cuanto alguien quiera llegar desde el usuario.
        //
        // La segunda es que las cinco se configuran aunque hoy no se usen todas. UsuarioClaim se
        // usa en cada ingreso -UserClaimsPrincipalFactory arma el principal con ella-, UsuarioLogin
        // y UsuarioToken las pide el constructor de UserStore, y RolClaim solo sirve si alguien
        // cambia a RoleClaimsPrincipalFactory. Dejarlas fuera no ahorra nada: el dia que falte una,
        // el fallo aparece como "Cannot create a DbSet for 'X' because this type is not included in
        // the model for the context", en el peor momento y sin decir que falta la tabla.

        // seg.UsuarioRol ── la que lee EmisorJwt para resolver los permisos del token
        var usuarioRol = modelBuilder.Entity<IdentityUserRole<Guid>>();
        usuarioRol.ToTable("UsuarioRol", Esquemas.Seguridad);
        usuarioRol.HasKey(ur => new { ur.UserId, ur.RoleId });
        usuarioRol.HasOne<Usuario>().WithMany().HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade);
        usuarioRol.HasOne<Rol>().WithMany().HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Cascade);
        usuarioRol.HasIndex(ur => ur.RoleId).HasDatabaseName("IX_UsuarioRol_RolId");

        // seg.UsuarioClaim ── claims propios del usuario, distintos de los del token de sesión
        var usuarioClaim = modelBuilder.Entity<IdentityUserClaim<Guid>>();
        usuarioClaim.ToTable("UsuarioClaim", Esquemas.Seguridad);
        usuarioClaim.HasKey(c => c.Id);
        usuarioClaim.Property(c => c.Id).HasColumnName("Id").ValueGeneratedOnAdd();
        usuarioClaim.Property(c => c.UserId).HasColumnName("UserId");
        usuarioClaim.Property(c => c.ClaimType).HasColumnName("ClaimType").HasMaxLength(256).IsRequired();
        usuarioClaim.Property(c => c.ClaimValue).HasColumnName("ClaimValue");
        usuarioClaim.HasOne<Usuario>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        usuarioClaim.HasIndex(c => c.UserId).HasDatabaseName("IX_UsuarioClaim_UserId");

        // seg.UsuarioLogin ── para el ingreso con un proveedor externo. Todavía no se usa.
        var usuarioLogin = modelBuilder.Entity<IdentityUserLogin<Guid>>();
        usuarioLogin.ToTable("UsuarioLogin", Esquemas.Seguridad);
        usuarioLogin.HasKey(l => new { l.LoginProvider, l.ProviderKey });
        usuarioLogin.Property(l => l.LoginProvider).HasColumnName("LoginProvider").HasMaxLength(128).IsRequired();
        usuarioLogin.Property(l => l.ProviderKey).HasColumnName("ProviderKey").HasMaxLength(128).IsRequired();
        usuarioLogin.Property(l => l.ProviderDisplayName).HasColumnName("ProviderDisplayName").HasMaxLength(128);
        usuarioLogin.Property(l => l.UserId).HasColumnName("UserId");
        usuarioLogin.HasOne<Usuario>().WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
        usuarioLogin.HasIndex(l => l.UserId).HasDatabaseName("IX_UsuarioLogin_UserId");

        // seg.UsuarioToken ── tokens de dos factores y códigos de recuperación
        var usuarioToken = modelBuilder.Entity<IdentityUserToken<Guid>>();
        usuarioToken.ToTable("UsuarioToken", Esquemas.Seguridad);
        usuarioToken.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });
        usuarioToken.Property(t => t.UserId).HasColumnName("UserId");
        usuarioToken.Property(t => t.LoginProvider).HasColumnName("LoginProvider").HasMaxLength(128).IsRequired();
        usuarioToken.Property(t => t.Name).HasColumnName("Name").HasMaxLength(128).IsRequired();
        usuarioToken.Property(t => t.Value).HasColumnName("Value").HasMaxLength(5000);
        usuarioToken.HasOne<Usuario>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        usuarioToken.HasIndex(t => t.LoginProvider).HasDatabaseName("IX_UsuarioToken_LoginProvider");

        // seg.RolClaim ── claims a nivel de rol
        var rolClaim = modelBuilder.Entity<IdentityRoleClaim<Guid>>();
        rolClaim.ToTable("RolClaim", Esquemas.Seguridad);
        rolClaim.HasKey(c => c.Id);
        rolClaim.Property(c => c.Id).HasColumnName("Id").ValueGeneratedOnAdd();
        rolClaim.Property(c => c.RoleId).HasColumnName("RoleId");
        rolClaim.Property(c => c.ClaimType).HasColumnName("ClaimType").HasMaxLength(256).IsRequired();
        rolClaim.Property(c => c.ClaimValue).HasColumnName("ClaimValue");
        rolClaim.HasOne<Rol>().WithMany().HasForeignKey(c => c.RoleId).OnDelete(DeleteBehavior.Cascade);
        rolClaim.HasIndex(c => c.RoleId).HasDatabaseName("IX_RolClaim_RoleId");

        var usuarioPermiso = modelBuilder.Entity<UsuarioPermiso>();
        ConfigurarBase(usuarioPermiso);
        usuarioPermiso.ToTable("UsuarioPermiso", Esquemas.Seguridad);
        usuarioPermiso.Property(up => up.Concedido).HasColumnName("Concedido").HasDefaultValue(true);
        usuarioPermiso.Property(up => up.Motivo).HasColumnName("Motivo").HasMaxLength(500);
        usuarioPermiso.Property(up => up.VigenteHasta).HasColumnName("VigenteHasta").HasColumnType("datetime2(3)");
        usuarioPermiso.HasIndex(up => new { up.UsuarioId, up.PermisoId }).IsUnique().HasDatabaseName("UX_UsuarioPermiso_Usuario_Permiso");
        usuarioPermiso.HasOne(up => up.Usuario).WithMany(u => u.Permisos).HasForeignKey(up => up.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        usuarioPermiso.HasOne(up => up.Permiso).WithMany(p => p.Usuarios).HasForeignKey(up => up.PermisoId).OnDelete(DeleteBehavior.Cascade);
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Auditoría (esquema aud, append-only)
    // ═════════════════════════════════════════════════════════════════════════════

    private static void ConfigurarAuditoria(ModelBuilder modelBuilder)
    {
        var auditoria = modelBuilder.Entity<RegistroAuditoria>();
        auditoria.ToTable("RegistroAuditoria", Esquemas.Auditoria);

        // Sin ConfigurarBase: la bitácora no lleva IsActive ni CreatedBy (§9.1).
        auditoria.HasKey(r => r.Id);
        auditoria.Property(r => r.Id).HasColumnName("Id").ValueGeneratedOnAdd();
        auditoria.Property(r => r.Entidad).HasColumnName("Entidad").HasMaxLength(60).IsRequired();
        auditoria.Property(r => r.EntidadId).HasColumnName("EntidadId").IsRequired();
        auditoria.Property(r => r.AccionId).HasColumnName("Accion").IsRequired();
        auditoria.Property(r => r.UsuarioId).HasColumnName("UsuarioId");
        auditoria.Property(r => r.NombreUsuario).HasColumnName("NombreUsuario").HasMaxLength(150);
        auditoria.Property(r => r.FechaUtc).HasColumnName("FechaUtc").HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()");
        auditoria.Property(r => r.Ip).HasColumnName("Ip").HasMaxLength(45);
        auditoria.Property(r => r.Agente).HasColumnName("Agente").HasMaxLength(300);
        auditoria.Property(r => r.TraceId).HasColumnName("TraceId").HasMaxLength(64);
        auditoria.Property(r => r.CamposModificados).HasColumnName("CamposModificados").HasMaxLength(1000);
        auditoria.Property(r => r.ValorAnteriorJson).HasColumnName("ValorAnteriorJson");
        auditoria.Property(r => r.ValorNuevoJson).HasColumnName("ValorNuevoJson");
        auditoria.Property(r => r.Motivo).HasColumnName("Motivo").HasMaxLength(1000);

        auditoria.HasIndex(r => new { r.Entidad, r.EntidadId, r.FechaUtc })
            .HasDatabaseName("IX_Auditoria_Entidad");
        auditoria.HasIndex(r => new { r.UsuarioId, r.FechaUtc })
            .HasDatabaseName("IX_Auditoria_Usuario");
        auditoria.HasIndex(r => r.FechaUtc).HasDatabaseName("IX_Auditoria_Fecha");
        auditoria.HasIndex(r => new { r.AccionId, r.FechaUtc }).HasDatabaseName("IX_Auditoria_Accion");
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Negocio (esquema dbo)
    // ═════════════════════════════════════════════════════════════════════════════

    private static void ConfigurarNegocio(ModelBuilder modelBuilder)
    {
        // ── dbo.Cliente ──────────────────────────────────────────────────────────
        var cliente = modelBuilder.Entity<Cliente>();
        ConfigurarBase(cliente);
        ConfigurarProcedencia(cliente);
        cliente.ToTable("Cliente", Esquemas.Negocio);
        cliente.Property(c => c.Codigo).HasColumnName("Codigo").HasMaxLength(30).IsRequired();
        cliente.Property(c => c.Nombre).HasColumnName("Nombre").HasMaxLength(200).IsRequired();
        cliente.Property(c => c.RazonSocial).HasColumnName("RazonSocial").HasMaxLength(200);
        cliente.Property(c => c.TipoClienteId).HasColumnName("TipoCliente").IsRequired();
        cliente.Property(c => c.CedulaORuc).HasColumnName("CedulaORuc").HasMaxLength(30);
        cliente.Property(c => c.IdentificacionFiscal).HasColumnName("IdentificacionFiscal").HasMaxLength(30);
        cliente.Property(c => c.Direccion).HasColumnName("Direccion").HasMaxLength(300);
        cliente.Property(c => c.Ciudad).HasColumnName("Ciudad").HasMaxLength(100);
        cliente.Property(c => c.Departamento).HasColumnName("Departamento").HasMaxLength(100);
        cliente.Property(c => c.Telefono).HasColumnName("Telefono").HasMaxLength(30);
        cliente.Property(c => c.Correo).HasColumnName("Correo").HasMaxLength(256);
        cliente.Property(c => c.ContactoNombre).HasColumnName("ContactoNombre").HasMaxLength(150);
        cliente.Property(c => c.ContactoCargo).HasColumnName("ContactoCargo").HasMaxLength(100);
        cliente.Property(c => c.SitioWeb).HasColumnName("SitioWeb").HasMaxLength(256);
        cliente.Property(c => c.Notas).HasColumnName("Notas").HasMaxLength(1000);
        ConfigurarFkOrigen(cliente, "OrigenDatoId", "OrigenDatoId", typeof(OrigenDatoCatalogo));
        cliente.HasIndex(c => c.Codigo).IsUnique().HasDatabaseName("UX_Cliente_Codigo");
        cliente.HasIndex(c => c.Nombre).HasDatabaseName("IX_Cliente_Nombre");
        cliente.HasOne(c => c.TipoCliente).WithMany().HasForeignKey(c => c.TipoClienteId).OnDelete(DeleteBehavior.Restrict);

        // ── dbo.Producto ─────────────────────────────────────────────────────────
        var producto = modelBuilder.Entity<Producto>();
        ConfigurarBase(producto);
        ConfigurarProcedencia(producto);
        producto.ToTable("Producto", Esquemas.Negocio);
        producto.Property(p => p.Codigo).HasColumnName("Codigo").HasMaxLength(30).IsRequired();
        producto.Property(p => p.Nombre).HasColumnName("Nombre").HasMaxLength(250).IsRequired();
        producto.Property(p => p.Descripcion).HasColumnName("Descripcion").HasMaxLength(500);
        producto.Property(p => p.CategoriaId).HasColumnName("CategoriaId");
        producto.Property(p => p.CostoActual).HasColumnName("CostoActual").HasPrecision(18, 6).IsRequired();
        producto.Property(p => p.PrecioVentaActual).HasColumnName("PrecioVentaActual").HasPrecision(18, 6);
        producto.Property(p => p.VidaUtilMeses).HasColumnName("VidaUtilMeses");
        ConfigurarFkOrigen(producto, "OrigenPrecioId", "OrigenPrecio", typeof(OrigenPrecioCatalogo));
        producto.HasIndex(p => p.Codigo).IsUnique().HasDatabaseName("UX_Producto_Codigo");
        producto.HasIndex(p => new { p.Nombre, p.IsActive }).HasDatabaseName("IX_Producto_Nombre");
        producto.HasOne(p => p.Categoria).WithMany().HasForeignKey(p => p.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        producto.HasOne(p => p.UnidadMedida).WithMany().HasForeignKey(p => p.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
        producto.HasMany(p => p.Precios).WithOne(pe => pe.Producto).HasForeignKey(pe => pe.ProductoId).OnDelete(DeleteBehavior.Cascade);

        // ── dbo.PrecioProducto ───────────────────────────────────────────────────
        var precioProducto = modelBuilder.Entity<PrecioProducto>();
        ConfigurarBase(precioProducto);
        precioProducto.ToTable("PrecioProducto", Esquemas.Negocio);
        precioProducto.Property(p => p.VigenteDesde).HasColumnName("VigenteDesde").HasColumnType("date").IsRequired();
        precioProducto.Property(p => p.VigenteHasta).HasColumnName("VigenteHasta").HasColumnType("date");
        precioProducto.Property(p => p.Costo).HasColumnName("Costo").HasPrecision(18, 6).IsRequired();
        precioProducto.Property(p => p.PrecioVenta).HasColumnName("PrecioVenta").HasPrecision(18, 6);
        ConfigurarFkOrigen(precioProducto, "OrigenPrecioId", "OrigenPrecio", typeof(OrigenPrecioCatalogo));
        precioProducto.HasIndex(p => new { p.ProductoId, p.VigenteDesde })
            .IsUnique().HasDatabaseName("UX_PrecioProducto_Producto_Inicio");

        // ── dbo.ActivoAmortizable ────────────────────────────────────────────────
        var activo = modelBuilder.Entity<ActivoAmortizable>();
        ConfigurarBase(activo);
        ConfigurarProcedencia(activo);
        activo.ToTable("ActivoAmortizable", Esquemas.Negocio, t => t.HasCheckConstraint(
            "CK_ActivoAmortizable_MesesVidaUtil", "[MesesVidaUtil] > 0"));
        activo.Property(a => a.Codigo).HasColumnName("Codigo").HasMaxLength(30).IsRequired();
        activo.Property(a => a.Nombre).HasColumnName("Nombre").HasMaxLength(250).IsRequired();
        activo.Property(a => a.Descripcion).HasColumnName("Descripcion").HasMaxLength(500);
        activo.Property(a => a.CategoriaId).HasColumnName("CategoriaId");
        activo.Property(a => a.CostoActual).HasColumnName("CostoActual").HasPrecision(18, 6).IsRequired();
        activo.Property(a => a.ValorResidual).HasColumnName("ValorResidual").HasPrecision(18, 6);
        activo.Property(a => a.Cantidad).HasColumnName("Cantidad").HasPrecision(18, 4).HasDefaultValue(1m);
        // H-04: sin este CHECK, una vida útil en 0 dejaba el valor mensual en 0 en silencio.
        activo.Property(a => a.MesesVidaUtil).HasColumnName("MesesVidaUtil").IsRequired();
        ConfigurarFkOrigen(activo, "OrigenPrecioId", "OrigenPrecio", typeof(OrigenPrecioCatalogo));
        activo.HasIndex(a => a.Codigo).IsUnique().HasDatabaseName("UX_ActivoAmortizable_Codigo");
        activo.HasOne(a => a.Categoria).WithMany().HasForeignKey(a => a.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        activo.HasOne(a => a.UnidadMedida).WithMany().HasForeignKey(a => a.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);
        activo.HasMany(a => a.Precios).WithOne(p => p.ActivoAmortizable).HasForeignKey(p => p.ActivoAmortizableId).OnDelete(DeleteBehavior.Cascade);

        // ── dbo.PrecioActivo ─────────────────────────────────────────────────────
        var precioActivo = modelBuilder.Entity<PrecioActivo>();
        ConfigurarBase(precioActivo);
        precioActivo.ToTable("PrecioActivo", Esquemas.Negocio);
        precioActivo.Property(p => p.VigenteDesde).HasColumnName("VigenteDesde").HasColumnType("date").IsRequired();
        precioActivo.Property(p => p.VigenteHasta).HasColumnName("VigenteHasta").HasColumnType("date");
        precioActivo.Property(p => p.Costo).HasColumnName("Costo").HasPrecision(18, 6).IsRequired();
        ConfigurarFkOrigen(precioActivo, "OrigenPrecioId", "OrigenPrecio", typeof(OrigenPrecioCatalogo));
        precioActivo.HasIndex(p => new { p.ActivoAmortizableId, p.VigenteDesde })
            .IsUnique().HasDatabaseName("UX_PrecioActivo_Activo_Inicio");

        // ── dbo.TipoCambio ───────────────────────────────────────────────────────
        var tipoCambio = modelBuilder.Entity<TipoCambio>();
        ConfigurarBase(tipoCambio);
        ConfigurarProcedencia(tipoCambio);
        tipoCambio.ToTable("TipoCambio", Esquemas.Negocio);
        tipoCambio.Property(t => t.Fecha).HasColumnName("Fecha").HasColumnType("date").IsRequired();
        tipoCambio.Property(t => t.Valor).HasColumnName("Valor").HasPrecision(18, 6).IsRequired();
        tipoCambio.Property(t => t.FuenteId).HasColumnName("Fuente").IsRequired();
        ConfigurarFkOrigen(tipoCambio, "OrigenDatoId", "OrigenDatoId", typeof(OrigenDatoCatalogo));
        tipoCambio.HasIndex(t => new { t.Fecha, t.FuenteId }).IsUnique().HasDatabaseName("UX_TipoCambio_Fecha_Fuente");
        tipoCambio.HasOne(t => t.Fuente).WithMany().HasForeignKey(t => t.FuenteId).OnDelete(DeleteBehavior.Restrict);

        // ── dbo.Cotizacion ───────────────────────────────────────────────────────
        var cotizacion = modelBuilder.Entity<Cotizacion>();
        // Una cotización se anula, no se desactiva: por eso no lleva IsActive.
        ConfigurarBase(cotizacion, conIsActive: false);
        cotizacion.ToTable("Cotizacion", Esquemas.Negocio, t => t.HasCheckConstraint(
            "CK_Cotizacion_MargenAplicado", "[MargenAplicado] >= 0 AND [MargenAplicado] <= 1"));
        cotizacion.Property(c => c.Numero).HasColumnName("Numero").HasMaxLength(20).IsRequired();
        cotizacion.Property(c => c.Anio).HasColumnName("Anio").IsRequired();
        cotizacion.Property(c => c.Version).HasColumnName("Version").HasDefaultValue((short)1);
        cotizacion.Property(c => c.EsVersionActual).HasColumnName("EsVersionActual").HasDefaultValue(true);
        cotizacion.Property(c => c.TipoClienteId).HasColumnName("TipoCliente").IsRequired();
        cotizacion.Property(c => c.Fecha).HasColumnName("Fecha").HasColumnType("date").IsRequired();
        cotizacion.Property(c => c.FechaVigencia).HasColumnName("FechaVigencia").HasColumnType("date");
        cotizacion.Property(c => c.VigenciaDias).HasColumnName("VigenciaDias");
        cotizacion.Property(c => c.EstadoId).HasColumnName("Estado").IsRequired();
        cotizacion.Property(c => c.ModalidadId).HasColumnName("Modalidad");
        cotizacion.Property(c => c.FechaEstimada).HasColumnName("FechaEstimada").HasColumnType("date");
        // H-02: el margen es una fracción. Este CHECK es la razón por la que el 5.5 con formato
        // 0% del Excel no puede volver a colarse.
        cotizacion.Property(c => c.MargenAplicado).HasColumnName("MargenAplicado").HasPrecision(5, 4).IsRequired();
        cotizacion.Property(c => c.MargenOrigenId).HasColumnName("MargenOrigen").IsRequired();
        cotizacion.Property(c => c.MetodoCosteoId).HasColumnName("MetodoCosteo").IsRequired();
        cotizacion.Property(c => c.TipoCambioValor).HasColumnName("TipoCambioValor").HasPrecision(18, 6).IsRequired();
        cotizacion.Property(c => c.TipoCambioFecha).HasColumnName("TipoCambioFecha").HasColumnType("date").IsRequired();
        cotizacion.Property(c => c.TotalInsumosSemestre1).HasColumnName("TotalInsumosSemestre1").HasPrecision(18, 6);
        cotizacion.Property(c => c.TotalInsumosSemestre2).HasColumnName("TotalInsumosSemestre2").HasPrecision(18, 6);
        cotizacion.Property(c => c.TotalInsumos).HasColumnName("TotalInsumos").HasPrecision(18, 6);
        cotizacion.Property(c => c.TotalAmortizablesMensual).HasColumnName("TotalAmortizablesMensual").HasPrecision(18, 6);
        cotizacion.Property(c => c.CostoTotal).HasColumnName("CostoTotal").HasPrecision(18, 6);
        cotizacion.Property(c => c.PrecioCordobas).HasColumnName("PrecioCordobas").HasPrecision(18, 6);
        cotizacion.Property(c => c.PrecioUsd).HasColumnName("PrecioUsd").HasPrecision(18, 6);
        cotizacion.Property(c => c.PorcentajePlantilla).HasColumnName("PorcentajePlantilla").HasPrecision(9, 6);
        cotizacion.Property(c => c.HashCalculo).HasColumnName("HashCalculo").HasColumnType("binary(32)");
        cotizacion.Property(c => c.ParametrosCalculo).HasColumnName("ParametrosCalculo");
        cotizacion.Property(c => c.ClienteNombre).HasColumnName("ClienteNombre").HasMaxLength(200).IsRequired();
        cotizacion.Property(c => c.ClienteDocumento).HasColumnName("ClienteDocumento").HasMaxLength(30);
        cotizacion.Property(c => c.ContactoNombre).HasColumnName("ContactoNombre").HasMaxLength(150);
        cotizacion.Property(c => c.ContactoTelefono).HasColumnName("ContactoTelefono").HasMaxLength(30);
        cotizacion.Property(c => c.ContactoCorreo).HasColumnName("ContactoCorreo").HasMaxLength(256);
        cotizacion.Property(c => c.DireccionServicio).HasColumnName("DireccionServicio").HasMaxLength(300);
        cotizacion.Property(c => c.Condiciones).HasColumnName("Condiciones").HasMaxLength(2000);
        cotizacion.Property(c => c.Observaciones).HasColumnName("Observaciones").HasMaxLength(2000);
        cotizacion.Property(c => c.EnviadaUtc).HasColumnName("EnviadaUtc").HasColumnType("datetime2(3)");
        cotizacion.Property(c => c.AprobadaUtc).HasColumnName("AprobadaUtc").HasColumnType("datetime2(3)");
        cotizacion.Property(c => c.AnuladaUtc).HasColumnName("AnuladaUtc").HasColumnType("datetime2(3)");
        cotizacion.Property(c => c.MotivoAnulacion).HasColumnName("MotivoAnulacion").HasMaxLength(1000);
        cotizacion.Property(c => c.ConvertidaUtc).HasColumnName("ConvertidaUtc").HasColumnType("datetime2(3)");
        ConfigurarFkOrigen(cotizacion, "OrigenDatoId", "OrigenDatoId", typeof(OrigenDatoCatalogo));
        cotizacion.HasIndex(c => new { c.Numero, c.Version }).IsUnique().HasDatabaseName("UX_Cotizacion_Numero_Version");
        cotizacion.HasIndex(c => new { c.ClienteId, c.Fecha }).HasDatabaseName("IX_Cotizacion_Cliente_Fecha");
        cotizacion.HasIndex(c => new { c.EstadoId, c.Fecha }).HasDatabaseName("IX_Cotizacion_Estado_Fecha");
        cotizacion.HasOne(c => c.Cliente).WithMany().HasForeignKey(c => c.ClienteId).OnDelete(DeleteBehavior.Restrict);
        cotizacion.HasOne(c => c.TipoServicio).WithMany().HasForeignKey(c => c.TipoServicioId).OnDelete(DeleteBehavior.Restrict);
        cotizacion.HasOne(c => c.TipoCambio).WithMany().HasForeignKey(c => c.TipoCambioId).OnDelete(DeleteBehavior.Restrict);

        // ── dbo.DetalleInsumo ─────────────────────────────────────────────────────
        var detalleInsumo = modelBuilder.Entity<DetalleInsumo>();
        // Una línea no se activa ni se desactiva: existe mientras su cotización exista.
        ConfigurarBase(detalleInsumo, conIsActive: false);
        detalleInsumo.ToTable("DetalleInsumo", Esquemas.Negocio, t =>
        {
            t.HasCheckConstraint("CK_DetalleInsumo_Cantidad", "[Cantidad] > 0");
            t.HasCheckConstraint("CK_DetalleInsumo_PrecioUnitario", "[PrecioUnitario] >= 0");
            t.HasCheckConstraint("CK_DetalleInsumo_Total", "[Total] >= 0");
        });
        detalleInsumo.Property(d => d.SemestreId).HasColumnName("Semestre");
        detalleInsumo.Property(d => d.Cantidad).HasColumnName("Cantidad").HasPrecision(18, 4).IsRequired();
        detalleInsumo.Property(d => d.PrecioUnitario).HasColumnName("PrecioUnitario").HasPrecision(18, 6).IsRequired();
        detalleInsumo.Property(d => d.Total).HasColumnName("Total").HasPrecision(18, 6).IsRequired();
        detalleInsumo.Property(d => d.UnidadMedidaTexto).HasColumnName("UnidadMedidaTexto").HasMaxLength(15);
        detalleInsumo.Property(d => d.ProductoNombre).HasColumnName("ProductoNombre").HasMaxLength(250);
        detalleInsumo.Property(d => d.Orden).HasColumnName("Orden").HasDefaultValue((short)0);
        detalleInsumo.Property(d => d.Observacion).HasColumnName("Observacion").HasMaxLength(500);
        // Covering: RN-07 necesita SUM(Total) WHERE Semestre = 1 y = 2 sin leer la tabla.
        detalleInsumo.HasIndex(d => new { d.CotizacionId, d.SemestreId })
            .IncludeProperties(d => new { d.Total, d.Cantidad, d.PrecioUnitario })
            .HasDatabaseName("IX_DetalleInsumo_Cotizacion");
        detalleInsumo.HasIndex(d => d.ProductoId).HasDatabaseName("IX_DetalleInsumo_Producto");
        detalleInsumo.HasOne(d => d.Cotizacion).WithMany(c => c.DetalleInsumos).HasForeignKey(d => d.CotizacionId).OnDelete(DeleteBehavior.Cascade);
        detalleInsumo.HasOne(d => d.Producto).WithMany().HasForeignKey(d => d.ProductoId).OnDelete(DeleteBehavior.Restrict);
        detalleInsumo.HasOne(d => d.Semestre).WithMany().HasForeignKey(d => d.SemestreId).OnDelete(DeleteBehavior.Restrict);

        // ── dbo.DetalleAmortizable ───────────────────────────────────────────────
        var detalleAmortizable = modelBuilder.Entity<DetalleAmortizable>();
        // Como la línea de insumo: no tiene vida propia.
        ConfigurarBase(detalleAmortizable, conIsActive: false);
        detalleAmortizable.ToTable("DetalleAmortizable", Esquemas.Negocio, t =>
        {
            t.HasCheckConstraint("CK_DetalleAmortizable_Cantidad", "[Cantidad] > 0");
            t.HasCheckConstraint("CK_DetalleAmortizable_PrecioUnitario", "[PrecioUnitario] >= 0");
            // H-04: la razón de que este CHECK exista.
            t.HasCheckConstraint("CK_DetalleAmortizable_MesesVidaUtil", "[MesesVidaUtil] > 0");
            t.HasCheckConstraint("CK_DetalleAmortizable_ValorMensual", "[ValorMensual] >= 0");
        });
        detalleAmortizable.Property(d => d.Cantidad).HasColumnName("Cantidad").HasPrecision(18, 4).IsRequired();
        detalleAmortizable.Property(d => d.PrecioUnitario).HasColumnName("PrecioUnitario").HasPrecision(18, 6).IsRequired();
        detalleAmortizable.Property(d => d.MesesVidaUtil).HasColumnName("MesesVidaUtil").IsRequired();
        detalleAmortizable.Property(d => d.Total).HasColumnName("Total").HasPrecision(18, 6).IsRequired();
        detalleAmortizable.Property(d => d.ValorMensual).HasColumnName("ValorMensual").HasPrecision(18, 6).HasDefaultValue(0m);
        detalleAmortizable.Property(d => d.UnidadMedidaTexto).HasColumnName("UnidadMedidaTexto").HasMaxLength(15);
        detalleAmortizable.Property(d => d.ActivoNombre).HasColumnName("ActivoNombre").HasMaxLength(250);
        detalleAmortizable.Property(d => d.Orden).HasColumnName("Orden").HasDefaultValue((short)0);
        detalleAmortizable.Property(d => d.Observacion).HasColumnName("Observacion").HasMaxLength(500);
        detalleAmortizable.HasIndex(d => new { d.CotizacionId, d.Orden })
            .IncludeProperties(d => new { d.ValorMensual, d.Total, d.MesesVidaUtil })
            .HasDatabaseName("IX_DetalleAmortizable_Cotizacion");
        detalleAmortizable.HasIndex(d => d.ActivoAmortizableId).HasDatabaseName("IX_DetalleAmortizable_Activo");
        detalleAmortizable.HasOne(d => d.Cotizacion).WithMany(c => c.DetalleAmortizables).HasForeignKey(d => d.CotizacionId).OnDelete(DeleteBehavior.Cascade);
        detalleAmortizable.HasOne(d => d.ActivoAmortizable).WithMany().HasForeignKey(d => d.ActivoAmortizableId).OnDelete(DeleteBehavior.Restrict);

        // ── dbo.CotizacionAprobacion ─────────────────────────────────────────────
        var aprobacion = modelBuilder.Entity<CotizacionAprobacion>();
        // Una aprobación pertenece a una etapa del flujo; su estado está en el catálogo.
        ConfigurarBase(aprobacion, conIsActive: false);
        // El CHECK de §6.11 de "estado resuelto ⇒ fecha obligatoria" lo crea la migración con la
        // función escalar dbo.FnEsEstadoFinalAprobacion, que lee EsEstadoFinal del catálogo (§5.7).
        // Así el esquema no lleva dentro el número de "rechazada".
        aprobacion.ToTable("CotizacionAprobacion", Esquemas.Negocio, t => t.HasCheckConstraint(
            "CK_Aprobacion_Secuencia", "[Secuencia] >= 1"));
        aprobacion.Property(a => a.Secuencia).HasColumnName("Secuencia").HasDefaultValue((short)1);
        aprobacion.Property(a => a.NivelId).HasColumnName("Nivel").IsRequired();
        aprobacion.Property(a => a.EstadoId).HasColumnName("Estado").IsRequired();
        aprobacion.Property(a => a.MotivoDisparo).HasColumnName("MotivoDisparo").HasMaxLength(500);
        aprobacion.Property(a => a.MargenDisparador).HasColumnName("MargenDisparador").HasPrecision(5, 4);
        aprobacion.Property(a => a.MontoDisparador).HasColumnName("MontoDisparador").HasPrecision(18, 6);
        aprobacion.Property(a => a.SolicitadoUtc).HasColumnName("SolicitadoUtc").HasColumnType("datetime2(3)").IsRequired();
        aprobacion.Property(a => a.NombreAprobador).HasColumnName("NombreAprobador").HasMaxLength(150);
        aprobacion.Property(a => a.CorreoAprobador).HasColumnName("CorreoAprobador").HasMaxLength(256);
        aprobacion.Property(a => a.RespuestaPorToken).HasColumnName("RespuestaPorToken").HasDefaultValue(false);
        aprobacion.Property(a => a.FechaRespuesta).HasColumnName("FechaRespuesta").HasColumnType("datetime2(3)");
        aprobacion.Property(a => a.Comentario).HasColumnName("Comentario").HasMaxLength(2000);
        aprobacion.Property(a => a.TokenHash).HasColumnName("TokenHash").HasColumnType("binary(32)");
        aprobacion.Property(a => a.TokenExpira).HasColumnName("TokenExpira").HasColumnType("datetime2(3)");
        aprobacion.Property(a => a.TokenUsadoUtc).HasColumnName("TokenUsadoUtc").HasColumnType("datetime2(3)");
        aprobacion.HasIndex(a => new { a.CotizacionId, a.Secuencia }).HasDatabaseName("IX_Aprobacion_Cotizacion");
        aprobacion.HasIndex(a => new { a.EstadoId, a.SolicitadoUtc })
            .IncludeProperties(a => new { a.CotizacionId, a.NombreAprobador, a.CorreoAprobador })
            .HasDatabaseName("IX_Aprobacion_Pendientes");
        // Filtrado y único: el lookup del enlace público tiene que ser un seek.
        aprobacion.HasIndex(a => a.TokenHash).IsUnique()
            .HasDatabaseName("UX_Aprobacion_TokenHash")
            .HasFilter("[TokenHash] IS NOT NULL");
        aprobacion.HasOne(a => a.Cotizacion).WithMany(c => c.Aprobaciones).HasForeignKey(a => a.CotizacionId).OnDelete(DeleteBehavior.Cascade);
        aprobacion.HasOne(a => a.Estado).WithMany().HasForeignKey(a => a.EstadoId).OnDelete(DeleteBehavior.Restrict);
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Catálogos maestros editables
    // ═════════════════════════════════════════════════════════════════════════════

    private static void ConfigurarCatalogosMaestros(ModelBuilder modelBuilder)
    {
        // ── dbo.UnidadMedida ─────────────────────────────────────────────────────
        var unidad = modelBuilder.Entity<UnidadMedida>();
        ConfigurarBase(unidad);
        ConfigurarProcedencia(unidad);
        unidad.ToTable("UnidadMedida", Esquemas.Negocio);
        unidad.Property(u => u.Codigo).HasColumnName("Codigo").HasMaxLength(10).IsRequired();
        unidad.Property(u => u.Nombre).HasColumnName("Nombre").HasMaxLength(100).IsRequired();
        unidad.Property(u => u.Abreviatura).HasColumnName("Abreviatura").HasMaxLength(15).IsRequired();
        unidad.Property(u => u.Orden).HasColumnName("Orden").HasDefaultValue(0);
        unidad.Property(u => u.PermiteDecimales).HasColumnName("PermiteDecimales").HasDefaultValue(true);
        ConfigurarFkOrigen(unidad, "OrigenDatoId", "OrigenDatoId", typeof(OrigenDatoCatalogo));
        unidad.Property(u => u.EsSembrado).HasColumnName("EsSembrado").HasDefaultValue(true);
        unidad.HasIndex(u => u.Codigo).IsUnique().HasDatabaseName("UX_UnidadMedida_Codigo");

        // ── dbo.CategoriaProducto ────────────────────────────────────────────────
        var categoria = modelBuilder.Entity<CategoriaProducto>();
        ConfigurarBase(categoria);
        ConfigurarProcedencia(categoria);
        categoria.ToTable("CategoriaProducto", Esquemas.Negocio);
        categoria.Property(c => c.Codigo).HasColumnName("Codigo").HasMaxLength(20).IsRequired();
        categoria.Property(c => c.Nombre).HasColumnName("Nombre").HasMaxLength(120).IsRequired();
        categoria.Property(c => c.Descripcion).HasColumnName("Descripcion").HasMaxLength(300);
        categoria.Property(c => c.Orden).HasColumnName("Orden").HasDefaultValue(0);
        ConfigurarFkOrigen(categoria, "OrigenDatoId", "OrigenDatoId", typeof(OrigenDatoCatalogo));
        categoria.Property(c => c.EsSembrado).HasColumnName("EsSembrado").HasDefaultValue(true);
        categoria.HasIndex(c => c.Codigo).IsUnique().HasDatabaseName("UX_CategoriaProducto_Codigo");

        // ── dbo.TipoServicio ─────────────────────────────────────────────────────
        var tipoServicio = modelBuilder.Entity<TipoServicio>();
        ConfigurarBase(tipoServicio);
        ConfigurarProcedencia(tipoServicio);
        tipoServicio.ToTable("TipoServicio", Esquemas.Negocio);
        tipoServicio.Property(t => t.Codigo).HasColumnName("Codigo").HasMaxLength(20).IsRequired();
        tipoServicio.Property(t => t.Nombre).HasColumnName("Nombre").HasMaxLength(150).IsRequired();
        tipoServicio.Property(t => t.Descripcion).HasColumnName("Descripcion").HasMaxLength(500);
        tipoServicio.Property(t => t.UnidadMedidaId).HasColumnName("UnidadMedidaId");
        tipoServicio.Property(t => t.Orden).HasColumnName("Orden").HasDefaultValue(0);
        tipoServicio.Property(t => t.EsResidencialPorDefecto).HasColumnName("EsResidencialPorDefecto").HasDefaultValue(false);
        tipoServicio.Property(t => t.EsEmpresarialPorDefecto).HasColumnName("EsEmpresarialPorDefecto").HasDefaultValue(false);
        ConfigurarFkOrigen(tipoServicio, "OrigenDatoId", "OrigenDatoId", typeof(OrigenDatoCatalogo));
        tipoServicio.Property(t => t.EsSembrado).HasColumnName("EsSembrado").HasDefaultValue(true);
        tipoServicio.HasIndex(t => t.Codigo).IsUnique().HasDatabaseName("UX_TipoServicio_Codigo");
        tipoServicio.HasOne(t => t.UnidadMedida).WithMany().HasForeignKey(t => t.UnidadMedidaId).OnDelete(DeleteBehavior.Restrict);

        // ── dbo.TipoServicioMargen ───────────────────────────────────────────────
        // El margen vive con vigencia, y como fracción. H-02 / RN-10.
        var margen = modelBuilder.Entity<TipoServicioMargen>();
        ConfigurarBase(margen);
        margen.ToTable("TipoServicioMargen", Esquemas.Negocio, t =>
        {
            t.HasCheckConstraint("CK_TipoServicioMargen_Margen", "[Margen] >= 0 AND [Margen] <= 1");
            t.HasCheckConstraint("CK_TipoServicioMargen_Vigencia", "[VigenteHasta] IS NULL OR [VigenteHasta] >= [VigenteDesde]");
        });
        margen.Property(m => m.VigenteDesde).HasColumnName("VigenteDesde").HasColumnType("date").IsRequired();
        margen.Property(m => m.VigenteHasta).HasColumnName("VigenteHasta").HasColumnType("date");
        margen.Property(m => m.Margen).HasColumnName("Margen").HasPrecision(5, 4).IsRequired();
        ConfigurarFkOrigen(margen, "OrigenDatoId", "OrigenDatoId", typeof(OrigenDatoCatalogo));
        margen.HasIndex(m => new { m.TipoServicioId, m.VigenteDesde }).IsUnique().HasDatabaseName("UX_TipoServicioMargen_Inicio");
        margen.HasOne(m => m.TipoServicio).WithMany().HasForeignKey(m => m.TipoServicioId).OnDelete(DeleteBehavior.Cascade);
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Sistema: parámetros, consecutivos, notificaciones, documentos
    // ═════════════════════════════════════════════════════════════════════════════

    private static void ConfigurarSistema(ModelBuilder modelBuilder)
    {
        // ── dbo.ParametroGeneral ─────────────────────────────────────────────────
        // ⚠️ ADR-018: todo valor ajustable vive aquí, no compilado en C#.
        var parametro = modelBuilder.Entity<ParametroGeneral>();
        ConfigurarBase(parametro);
        parametro.ToTable("ParametroGeneral", Esquemas.Negocio);
        parametro.Property(p => p.Nombre).HasColumnName("Nombre").HasMaxLength(100).IsRequired();
        parametro.Property(p => p.Descripcion).HasColumnName("Descripcion").HasMaxLength(500);
        parametro.Property(p => p.Valor).HasColumnName("Valor");
        parametro.Property(p => p.TipoDatoId).HasColumnName("TipoDato").IsRequired();
        parametro.Property(p => p.Grupo).HasColumnName("Grupo").HasMaxLength(60);
        parametro.Property(p => p.Orden).HasColumnName("Orden").HasDefaultValue((short)0);
        parametro.Property(p => p.EsSistema).HasColumnName("EsSistema").HasDefaultValue(false);
        parametro.Property(p => p.EsSecreto).HasColumnName("EsSecreto").HasDefaultValue(false);
        parametro.HasIndex(p => p.Nombre).IsUnique().HasDatabaseName("UX_ParametroGeneral_Nombre");
        parametro.HasOne(p => p.TipoDato).WithMany().HasForeignKey(p => p.TipoDatoId).OnDelete(DeleteBehavior.Restrict);

        // ── dbo.Consecutivo ──────────────────────────────────────────────────────
        var consecutivo = modelBuilder.Entity<Consecutivo>();
        ConfigurarBase(consecutivo);
        consecutivo.ToTable("Consecutivo", Esquemas.Negocio);
        consecutivo.Property(c => c.TipoId).HasColumnName("Tipo").IsRequired();
        consecutivo.Property(c => c.Anio).HasColumnName("Anio").IsRequired();
        consecutivo.Property(c => c.Prefijo).HasColumnName("Prefijo").HasMaxLength(10);
        consecutivo.Property(c => c.UltimoNumero).HasColumnName("UltimoNumero").HasDefaultValue(0L);
        consecutivo.Property(c => c.Digitos).HasColumnName("Digitos").HasDefaultValue(4);
        consecutivo.HasIndex(c => new { c.TipoId, c.Anio }).IsUnique().HasDatabaseName("UX_Consecutivo_Tipo_Anio");
        consecutivo.HasOne(c => c.Tipo).WithMany().HasForeignKey(c => c.TipoId).OnDelete(DeleteBehavior.Restrict);

        // ── dbo.NotificacionEnviada ──────────────────────────────────────────────
        var notificacion = modelBuilder.Entity<NotificacionEnviada>();
        ConfigurarBase(notificacion);
        notificacion.ToTable("NotificacionEnviada", Esquemas.Negocio);
        notificacion.Property(n => n.TipoId).HasColumnName("Tipo").IsRequired();
        notificacion.Property(n => n.Destinatario).HasColumnName("Destinatario").HasMaxLength(256).IsRequired();
        notificacion.Property(n => n.Asunto).HasColumnName("Asunto").HasMaxLength(300);
        notificacion.Property(n => n.EstadoId).HasColumnName("Estado").IsRequired();
        notificacion.Property(n => n.Intentos).HasColumnName("Intentos").HasDefaultValue(0);
        notificacion.Property(n => n.EnviadoUtc).HasColumnName("EnviadoUtc").HasColumnType("datetime2(3)");
        notificacion.Property(n => n.IdExterno).HasColumnName("IdExterno").HasMaxLength(256);
        notificacion.Property(n => n.Error).HasColumnName("Error").HasMaxLength(2000);
        notificacion.HasIndex(n => new { n.EstadoId, n.TipoId }).HasDatabaseName("IX_Notificacion_Estado");

        // ── dbo.DocumentoGenerado ────────────────────────────────────────────────
        var documento = modelBuilder.Entity<DocumentoGenerado>();
        ConfigurarBase(documento);
        documento.ToTable("DocumentoGenerado", Esquemas.Negocio);
        documento.Property(d => d.TipoId).HasColumnName("Tipo").IsRequired();
        documento.Property(d => d.NombreArchivo).HasColumnName("NombreArchivo").HasMaxLength(300).IsRequired();
        documento.Property(d => d.Ruta).HasColumnName("Ruta").HasMaxLength(450);
        documento.Property(d => d.TamanoBytes).HasColumnName("TamanoBytes");
        documento.Property(d => d.Hash).HasColumnName("Hash").HasColumnType("binary(32)");
        documento.Property(d => d.GeneradoUtc).HasColumnName("GeneradoUtc").HasColumnType("datetime2(3)");
        documento.HasIndex(d => new { d.CotizacionId, d.TipoId }).HasDatabaseName("IX_Documento_Cotizacion");
    }

    // ── Conversores de zona horaria ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Al leer: un instante con desplazamiento a <c>DateTime</c> en UTC, que es como se guarda
    /// toda marca del modelo.
    /// <para>Se usa <see cref="DateTimeOffset.UtcDateTime"/> y no un <c>DateTime.SpecifyKind</c> con
    /// la hora sin cambios: son cosas distintas. <c>SpecifyKind</c> le pone la etiqueta "UTC" a una
    /// hora que en realidad es local, y a partir de ahí cualquier conversión posterior la mueve otra
    /// vez. El resultado visible es un bloqueo de cuenta que expira dos o cinco horas antes o
    /// después de lo debido, y que por lo tanto no expira nunca en la práctica.</para>
    /// </summary>
    private static DateTime? UtcAFechaNula(DateTimeOffset? valor) => valor?.UtcDateTime;

    /// <summary>Al escribir: un instante con desplazamiento a UTC, que es lo que espera Identity.</summary>
    private static DateTimeOffset? FechaNulaAUtc(DateTime? valor) =>
        valor.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(valor.Value, DateTimeKind.Utc)) : null;
}
