using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;

namespace Orkin.Cotizador.Core.Infrastructure.Persistence;

/// <summary>
/// Sella las columnas de auditoría y escribe <c>aud.RegistroAuditoria</c> sin que el código de
/// negocio se acuerde de hacerlo (§9, ADR-007).
/// <para><b>Por qué se sella en <c>SavingChanges</c> y la bitácora se escribe en
/// <c>SavedChanges</c>:</b> las marcas (<c>CreatedAt</c>, <c>CreatedBy</c>…) tienen que estar antes
/// del INSERT, porque la base las usa como <c>DEFAULT</c> y no como identidad. La bitácora, en
/// cambio, solo puede insertarse <b>después</b>: si se agrega al conjunto de cambios antes, el propio
/// INSERT de la bitácora dispara otra vez el interceptor y la fila se audita a sí misma. Por eso se
/// escribe con SQL crudo, que no pasa por el <c>ChangeTracker</c> y por lo tanto no puede recursar.</para>
/// <para><b>El SQL crudo comparte la transacción</b> del <c>SaveChanges</c> que ya está abierta: si
/// el guardado falla, tampoco queda la bitácora de un cambio que no ocurrió. Una bitácora que
/// registra lo que se quiso hacer en vez de lo que se hizo no sirve para auditar.</para>
/// <para><b>Por qué la acción se resuelve por código y no hay números:</b> la fila de
/// <c>dbo.AccionAuditoriaCatalogo</c> se busca por su <c>Codigo</c> y su <c>tinyint</c> se cachea por
/// contexto. Si un día la base no tiene la fila, el arranque y cada guardado fallan con
/// <see cref="CatalogoNoResueltoException"/>: es preferible a escribir <c>Accion = 0</c> y que nadie
/// pueda decir después qué pasó.</para>
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IContextoUsuario _contextoUsuario;
    private readonly ILogger<AuditSaveChangesInterceptor> _logger;

    /// <summary>
    /// Los <c>AccionId</c> ya resueltos, por <see cref="CotizadorDbContext"/>. Se usa una
    /// <see cref="ConditionalWeakTable{TKey,TValue}"/> y no un diccionario estático normal para que
    /// la entrada muera junto con el contexto y no quede una referencia viva a una base que ya no
    /// existe.
    /// </summary>
    private static readonly ConditionalWeakTable<CotizadorDbContext, ConcurrentDictionary<string, byte>>
        AccionesPorContexto = new();

    /// <summary>
    /// Lo que se va a escribir en <c>aud</c> cuando el guardado termine bien. Vive en la instancia
    /// del interceptor, que se registra con tiempo de vida <c>scoped</c>: dos peticiones nunca
    /// comparten la lista, y un trabajo en segundo plano tampoco.
    /// </summary>
    /// <summary>
    /// Una fila de bitácora pendiente, con la entrada del <c>ChangeTracker</c> de la que salió.
    /// <para>Se guarda la entrada y no solo la fila porque la clave de una inserción generada por la
    /// base todavía no existe cuando se arma la lista. Las tablas con clave <c>Guid</c> dejan que
    /// la base la produzca con <c>NEWSEQUENTIALID()</c>, así que en <c>SavingChanges</c> -que es
    /// cuando se recoge la fila- el <c>Id</c> del objeto sigue siendo <c>00000000-...</c>. Se
    /// vuelve a leer en el momento de escribir, cuando EF ya lo devolvió.</para>
    /// <para>Sin esto, la bitácora del alta de cualquier tabla con clave generada queda apuntando al
    /// Guid cero: una fila que existe, que dice "se creó algo" y que no sirve para encontrar qué.
    /// Y no falla de forma visible, que es la peor forma de fallar.</para>
    /// </summary>
    private sealed record Pendiente(EntityEntry Entrada, RegistroAuditoria Fila);

    private readonly List<Pendiente> _pendientes = [];

    public AuditSaveChangesInterceptor(
        IContextoUsuario contextoUsuario,
        ILogger<AuditSaveChangesInterceptor> logger)
    {
        _contextoUsuario = contextoUsuario;
        _logger = logger;
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Sincrónico
    // ═════════════════════════════════════════════════════════════════════════════

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is CotizadorDbContext context)
        {
            Preparar(context);
        }

        return result;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is CotizadorDbContext context)
        {
            Grabar(context);
        }

        return result;
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Asincrónico
    // ═════════════════════════════════════════════════════════════════════════════

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is CotizadorDbContext context)
        {
            Preparar(context);
        }

        return ValueTask.FromResult(result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is CotizadorDbContext context)
        {
            await GrabarAsync(context, cancellationToken);
        }

        return result;
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  Interno
    // ═════════════════════════════════════════════════════════════════════════════

    /// <summary>Sella las marcas y arma la lista de filas de bitácora de este guardado.</summary>
    private void Preparar(CotizadorDbContext context)
    {
        _pendientes.Clear();

        var ahora = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            var tipo = entry.Entity.GetType();

            // La bitácora no se audita a sí misma. Además, sus filas se insertan por SQL crudo, así
            // que nunca pasan por acá; la exclusión es solo una red de seguridad explícita.
            if (tipo == typeof(RegistroAuditoria))
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    Sellar(entry, ahora, esNuevo: true);
                    break;

                case EntityState.Modified:
                    Sellar(entry, ahora, esNuevo: false);
                    break;
            }

            Recolectar(context, entry, ahora);
        }
    }

    /// <summary>
    /// Escribe las cuatro columnas de marca de §3.1. Se resuelven por nombre de propiedad del
    /// modelo, no por tipo, porque <c>seg.Usuario</c> y <c>seg.Rol</c> las llevan sin heredar de
    /// <see cref="BaseEntity"/>: son tablas de Identity con su propio conjunto de columnas.
    /// </summary>
    private void Sellar(EntityEntry entry, DateTime ahora, bool esNuevo)
    {
        Asignar(entry, "CreatedAt", ahora, siempre: esNuevo);
        Asignar(entry, "UpdatedAt", ahora, siempre: true);
        Asignar(entry, "CreatedBy", _contextoUsuario.UsuarioId, siempre: esNuevo);
        Asignar(entry, "UpdatedBy", _contextoUsuario.UsuarioId, siempre: true);
    }

    /// <param name="siempre">
    /// <c>true</c> en una inserción, donde la marca se escribe aunque nadie la haya puesto.
    /// <c>false</c> en una modificación, donde solo se toca si el código ya movió la columna: pisar
    /// <c>CreatedAt</c> cada vez que alguien actualiza una fila haría que la bitácora perdiera cuándo
    /// se creó la cosa.
    /// </param>
    private static void Asignar(EntityEntry entry, string nombrePropiedad, object? valor, bool siempre)
    {
        if (entry.Metadata.FindProperty(nombrePropiedad) is null)
        {
            return;
        }

        if (!siempre && !entry.Property(nombrePropiedad).IsModified)
        {
            return;
        }

        entry.CurrentValues[nombrePropiedad] = valor;
    }

    /// <summary>Agrega una fila a <c>aud</c> por cada entrada que se creó, cambió o borró.</summary>
    private void Recolectar(CotizadorDbContext context, EntityEntry entry, DateTime ahora)
    {
        if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            return;
        }

        var entidad = entry.Metadata;
        var clave = LeerClave(entidad, entry.Entity);

        if (clave is null)
        {
            AvisarSiNoAuditable(entidad);

            return;
        }

        var accion = ResolverAccion(entry);

        if (accion is null)
        {
            return;
        }

        var (campos, anterior, nuevo) = DescribirCambios(entry);

        // EntidadId se deja en cero a propósito: se rellena al escribir, no acá. Ver Pendiente.
        _pendientes.Add(new Pendiente(entry, new RegistroAuditoria
        {
            Entidad = entidad.ClrType.Name,
            AccionId = ResolverAccionId(context, accion),
            UsuarioId = _contextoUsuario.UsuarioId,
            NombreUsuario = _contextoUsuario.NombreUsuario,
            FechaUtc = ahora,
            Ip = _contextoUsuario.Ip,
            Agente = Truncar(_contextoUsuario.Agente, 300),
            TraceId = Truncar(Activity.Current?.TraceId.ToString() ?? _contextoUsuario.TraceId, 64),
            CamposModificados = campos is null ? null : Truncar(campos, 1000),
            ValorAnteriorJson = anterior,
            ValorNuevoJson = nuevo
        }));
    }

    /// <summary>
    /// Qué acción se registra. Sale del <see cref="EntityState"/> y no de un parámetro del código de
    /// negocio, porque insertar o modificar es un hecho observable por sí mismo; las acciones que
    /// dependen de una <i>intención</i> (anular, aprobar) las escriben los servicios a mano.
    /// </summary>
    private string? ResolverAccion(EntityEntry entry)
    {
        if (entry.State == EntityState.Added)
        {
            return AccionesAuditoria.Creado;
        }

        if (entry.State == EntityState.Deleted)
        {
            return AccionesAuditoria.Eliminado;
        }

        // Un Modified que revierte IsActive de false a true es una restauración, no una edición.
        // Es la única señal que el estado de la entidad da por sí solo sobre por qué cambió.
        var isActive = entry.Metadata.FindProperty(nameof(BaseEntity.IsActive));
        var baseEntity = entry.Entity as BaseEntity;

        if (isActive is not null
            && baseEntity is not null
            && entry.OriginalValues[nameof(BaseEntity.IsActive)] is bool antes
            && antes == false
            && baseEntity.IsActive)
        {
            return AccionesAuditoria.Restaurado;
        }

        return AccionesAuditoria.Actualizado;
    }

    /// <summary>
    /// Los valores anteriores y nuevos, solo de lo que cambió. Sin esto, la bitácora guardaría la
    /// fila entera en cada edición y un cambio de precio de un producto llenaría la tabla de
    /// documentos que una cotización con veinte líneas produce.
    /// </summary>
    private static (string? Campos, string? Anterior, string? Nuevo) DescribirCambios(EntityEntry entry)
    {
        if (entry.State != EntityState.Modified)
        {
            return (null, null, null);
        }

        var modificados = entry.Properties
            .Where(p => p.IsModified)
            .Select(p => p.Metadata.Name)
            .ToArray();

        if (modificados.Length == 0)
        {
            return (null, null, null);
        }

        var anterior = new Dictionary<string, object?>(StringComparer.Ordinal);
        var nuevo = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var nombre in modificados)
        {
            anterior[nombre] = entry.OriginalValues[nombre];
            nuevo[nombre] = entry.CurrentValues[nombre];
        }

        var opciones = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        return (
            JsonSerializer.Serialize(modificados, opciones),
            JsonSerializer.Serialize(anterior, opciones),
            JsonSerializer.Serialize(nuevo, opciones));
    }

    /// <summary>
    /// Avisa, o no, de una entrada que quedó fuera de la bitácora por no tener clave <c>Guid</c>.
    /// <para>Hay dos motivos distintos y confundirlos convierte uncomportamiento esperado en un aviso
    /// por corrida, que es como se aprende a no leer los avisos.</para>
    /// <para><b>Clave compuesta</b>: son las tablas puente de <c>seg</c> y las de detalle del
    /// cotizador. No caben en <c>EntidadId</c> y dejarlas fuera es lo previsto, así que pasan
    /// inadvertidas a propósito. Su historia no se pierde: la fila que las une a su padre sí se
    /// audita, y para el caso de <c>seg.UsuarioRol</c> el cambio de rol de un usuario además queda
    /// en el <c>SecurityStamp</c> del usuario, que se audita como modificación.</para>
    /// <para><b>Clave de un solo valor que no es <c>Guid</c></b>: son los catálogos de dominio, con
    /// <c>Id tinyint</c>. Son de solo lectura en runtime y los crea la semilla, que corre en su
    /// propio contexto sin este interceptor. Que uno cambie por otro camino sí es una anomalía, y
    /// por eso este caso avisa.</para>
    /// </summary>
    private void AvisarSiNoAuditable(IReadOnlyEntityType entidad)
    {
        var clave = entidad.FindPrimaryKey();

        if (clave is null || clave.Properties.Count == 1)
        {
            _logger.LogWarning(
                "No se registró en la bitácora un cambio de {Entidad}: su clave no es un Guid. " +
                "Los catálogos de dominio (Id tinyint) solo cambian por la semilla.",
                entidad.ClrType.Name);
        }
    }

    private static Guid? LeerClave(IReadOnlyEntityType entidad, object valor)
    {
        var clave = entidad.FindPrimaryKey();

        if (clave is null)
        {
            return null;
        }

        var valorClave = clave.Properties
            .Select(p => p.PropertyInfo?.GetValue(valor))
            .ToArray();

        // Solo se auditan tablas con clave Guid. Un Id tinyint (catálogos) o una clave compuesta
        // (las tablas puente de seg) no cabe en EntidadId, y prefiero dejarlos fuera con un aviso
        // explícito que inventar un Guid que no existe.
        return valorClave.Length == 1 && valorClave[0] is Guid g ? g : null;
    }

    /// <summary>Traduce el <c>Codigo</c> de la acción al <c>tinyint</c> de la fila, cacheado.</summary>
    private byte ResolverAccionId(CotizadorDbContext context, string codigo)
    {
        var cache = AccionesPorContexto.GetValue(context, _ => new ConcurrentDictionary<string, byte>());

        if (cache.TryGetValue(codigo, out var id))
        {
            return id;
        }

        // La consulta se hace en SavingChanges, donde el contexto todavía está disponible para LINQ.
        // Es una sola vez por código y por contexto.
        var fila = context.AccionAuditoriaCatalogo
            .AsNoTracking()
            .FirstOrDefault(a => a.Codigo == codigo);

        if (fila is null)
        {
            throw new CatalogoNoResueltoException(nameof(AccionAuditoriaCatalogo), codigo);
        }

        cache[codigo] = fila.Id;
        return fila.Id;
    }

    private static string? Truncar(string? texto, int maximo)
        => texto is not null && texto.Length > maximo ? texto[..maximo] : texto;

    // ── Escritura ────────────────────────────────────────────────────────────────

    private void Grabar(CotizadorDbContext context)
    {
        if (_pendientes.Count == 0)
        {
            return;
        }

        var sql = ConstruirInsert();
        var conexion = context.Database.GetDbConnection();
        var abiertaPorAqui = AbrirSiHaceFalta(conexion);

        try
        {
            foreach (var pendiente in _pendientes)
            {
                using var comando = CrearComando(context, sql);
                Adjuntar(comando, Completar(pendiente));
                comando.ExecuteNonQuery();
            }

            AdvertirClavesVacias();
        }
        finally
        {
            if (abiertaPorAqui)
            {
                conexion.Close();
            }
        }

        _logger.LogDebug("Escritas {Cantidad} filas en aud.RegistroAuditoria.", _pendientes.Count);
        _pendientes.Clear();
    }

    private async Task GrabarAsync(CotizadorDbContext context, CancellationToken cancellationToken)
    {
        if (_pendientes.Count == 0)
        {
            return;
        }

        var sql = ConstruirInsert();
        var conexion = context.Database.GetDbConnection();
        var abiertaPorAqui = conexion.State != ConnectionState.Open;

        if (abiertaPorAqui)
        {
            await conexion.OpenAsync(cancellationToken);
        }

        try
        {
            foreach (var pendiente in _pendientes)
            {
                await using var comando = CrearComando(context, sql);
                Adjuntar(comando, Completar(pendiente));
                await comando.ExecuteNonQueryAsync(cancellationToken);
            }

            AdvertirClavesVacias();
        }
        finally
        {
            if (abiertaPorAqui)
            {
                conexion.Close();
            }
        }

        _logger.LogDebug("Escritas {Cantidad} filas en aud.RegistroAuditoria.", _pendientes.Count);
        _pendientes.Clear();
    }

    /// <summary>
    /// Abre la conexión solo si estaba cerrada, y avisa si la abrió para poder cerrarla después.
    /// <para>Hay que hacerlo porque a este punto la conexión del contexto ya está cerrada: EF
    /// confirmó su transacción y cerró antes de disparar <c>SavedChanges</c>. Y hay que cerrarla
    /// solo si se abrió acá, porque si ya venía abierta es que hay una transacción en curso y
    /// cerrarla le dejaría a EF con una conexión muerta.</para>
    /// </summary>
    private static bool AbrirSiHaceFalta(DbConnection conexion)
    {
        if (conexion.State == ConnectionState.Open)
        {
            return false;
        }

        conexion.Open();
        return true;
    }

    /// <summary>
    /// El <see cref="DbCommand"/> sobre la misma conexión del contexto, para que herede su
    /// transacción.
    /// <para><b>Por qué no <c>ExecuteSqlRaw</c>:</b> se empezó con él y tiene dos Entradas que
    /// hacen que no sirva acá. La primera es el tipo de los parámetros: con valores sueltos, EF
    /// los tipa con <c>GetMapping(valor.GetType())</c>, y para un nulo no hay tipo que mirar. Se
    /// puede probar con <c>?? DBNull.Value</c>, pero <c>DBNull</c> no es un tipo que el proveedor
    /// sepa mapear y el error que sale no menciona ni la columna ni la fila. La segunda es
    /// <b>exclusive</b>: al pasar parámetros ya construidos, EF los renombra pasando el SQL por
    /// <c>string.Format</c>, y <c>string.Format</c> no entiende los marcadores <c>{p0}</c> de EF
    /// —da <c>FormatException: Expected an ASCII digit</c>—. Con <see cref="DbCommand"/> no hay
    /// marcadores, no hay inferencia de tipos y no hay sorpresas: los tipos se declaran.</para>
    /// <para><b>Por qué la conexión del contexto y no una nueva:</b> una conexión nueva sería otra
    /// transacción, y la fila de bitácora sobreviviría a un guardado que falló. Además, mientras
    /// haya una transacción vigente, se ata al <see cref="DbCommand"/>: si no, SqlClient se
    /// niega a ejecutar y el error habla de <c>Command.Transaction</c>, que no dice nada de
    /// bitácora ni de auditoría.</para>
    /// <para><b>Y si no hay transacción vigente:</b> entonces el <c>SaveChanges</c> ya se
    /// confirmó, porque EF confirma la suya antes de disparar este evento. La fila de bitácora
    /// entra sola. No es la atomicidad ideal y es la única alcanzable desde acá; el motivo está
    /// anotado en <c>docs/BASE-DE-DATOS.md</c> §9.4 y en el comentario de
    /// <see cref="Preparar"/>.</para>
    /// </summary>
    private static DbCommand CrearComando(CotizadorDbContext context, string sql)
    {
        var comando = context.Database.GetDbConnection().CreateCommand();
        comando.CommandText = sql;

        if (context.Database.CurrentTransaction?.GetDbTransaction() is { } transaccion)
        {
            comando.Transaction = transaccion;
        }

        return comando;
    }

    /// <summary>
    /// Rellena <see cref="RegistroAuditoria.EntidadId"/> con el valor que la entidad tenga en este
    /// momento y devuelve la fila.
    /// <para>Se vuelve a leer la clave y no se confía en la que se copió al recoger, porque en una
    /// inserción la clave la produce la base: cuando se recogió la fila todavía era el Guid cero, y
    /// si se escribiera esa copia la bitácora del alta apuntaría a la nada. Para una modificación o
    /// un borrado el valor es el mismo que ya se tenía, y no hay diferencia.</para>
    /// </summary>
    private static RegistroAuditoria Completar(Pendiente pendiente)
    {
        if (LeerClave(pendiente.Entrada.Metadata, pendiente.Entrada.Entity) is { } clave)
        {
            pendiente.Fila.EntidadId = clave;
        }

        return pendiente.Fila;
    }

    private void AdvertirClavesVacias()
    {
        var sinClave = _pendientes
            .Where(p => p.Fila.EntidadId == Guid.Empty)
            .Select(p => p.Fila.Entidad)
            .Distinct()
            .ToArray();

        if (sinClave.Length > 0)
        {
            _logger.LogWarning(
                "Se escribieron {Cantidad} filas en aud.RegistroAuditoria sin clave de entidad para: " +
                "{Entidades}. La fila queda apuntando al Guid cero, así que no sirve para encontrar " +
                "qué se cambió. Suele significar que la clave la genera la base y EF todavía no la " +
                "devolvió al momento de escribir.",
                _pendientes.Count(p => p.Fila.EntidadId == Guid.Empty),
                string.Join(", ", sinClave));
        }
    }

    private static void Adjuntar(DbCommand comando, RegistroAuditoria r)
    {
        var parametros = Argumentos(r);

        for (var i = 0; i < parametros.Length; i++)
        {
            comando.Parameters.Add(parametros[i]);
        }
    }

    /// <summary>
    /// Un único <c>INSERT</c> con los parámetros de una fila, y se ejecuta una vez por fila. Se
    /// podría mandar todo en un lote, y con <c>MARS</c> o con un procedure sí se podría, pero la
    /// diferencia de rendimiento es irrelevante frente a la de las consultas que originan estas
    /// filas, y un lote obligaría a construir el texto del <c>VALUES</c> con la cantidad de
    /// parameters variable.
    /// <para>El <c>FechaUtc</c> se calcula en la base con <c>SYSUTCDATETIME()</c> y no se manda
    /// como parámetro, para que la marca sea la del servidor de datos y no la del reloj de la
    /// máquina donde corre la API. Es la misma marca que pone <c>ConfigurarAuditoria</c> por
    /// defecto, y se escribe explícita solo para que la bitácora no dependa de que ese
    /// <c>HasDefaultValueSql</c> siga puesto.</para>
    /// <para>La numeración <c>{p0}..{p10}</c> es de <c>ExecuteSqlRaw</c>, que ya no se usa: hoy los
    /// parámetros se agregan al <see cref="DbCommand"/> por posición, con los nombres
    /// <c>@p0</c>..<c>@p10</c> que produce <see cref="Argumentos"/>. Si algún día se vuelve a
    /// <c>ExecuteSqlRaw</c>, esta cadena tiene que volver a ser la que EF sepa interpretar.</para>
    /// </summary>
    private static string ConstruirInsert() =>
        """
        INSERT INTO [aud].[RegistroAuditoria]
            ([Entidad], [EntidadId], [Accion], [UsuarioId], [NombreUsuario], [FechaUtc],
             [Ip], [Agente], [TraceId], [CamposModificados], [ValorAnteriorJson], [ValorNuevoJson])
        VALUES
            (@p0, @p1, @p2, @p3, @p4, SYSUTCDATETIME(),
             @p5, @p6, @p7, @p8, @p9, @p10)
        """;

    /// <summary>
    /// Los once parámetros del <c>INSERT</c>, como <see cref="SqlParameter"/> y no como valores
    /// sueltos.
    /// <para><b>Por qué no vale la pena la forma corta:</b> <c>ExecuteSqlRaw</c> deduce el tipo de
    /// cada parámetro con <c>GetMapping(valor.GetType())</c>, y eso obliga a que el valor ya traiga
    /// su tipo resuelto. Para un <c>string</c> o un <c>Guid</c> funciona, pero para un nulo no hay
    /// tipo que mirar: el补救zo habitual es <c>?? DBNull.Value</c>, y <c>DBNull</c> no es un tipo
    /// que el proveedor sepa mapear. El error que sale es "The current provider doesn't have a
    /// store type mapping for properties of type 'DBNull'", que no dice qué columna ni qué fila, y
    /// aparece en el <c>SaveChanges</c> más inocente del sistema —crear el primer usuario—.</para>
    /// <para><b>Por qué <c>SqlParameter</c> con <c>SqlDbType</c> explícito y no un
    /// <see cref="DbParameter"/> con el valor solo:</b> un parámetro sin tipo declarado lleva el
    /// nulo al proveedor, y el proveedor tiene que adivinarlo. Declarar el tipo en el código hace
    /// que la longitud del <c>nvarchar</c> y el tipo de la fecha no dependan de lo que SqlClient
    /// haya decidido, y hace que un cambio de tipo en la entidad se note como un error de
    /// compilación y no como un truncado silencioso.</para>
    /// <para>Los anchos copian los de <see cref="CotizadorDbContext.ConfigurarAuditoria"/>: no es
    /// que el parámetro los imponga —la columna manda— sino que declararlos evita que SqlClient
    /// mande un <c>nvarchar</c> de longitud desconocida y que el optimizador del plan trabaje a
    /// ciegas.</para>
    /// </summary>
    private static DbParameter[] Argumentos(RegistroAuditoria r) =>
    [
        Texto("@p0", r.Entidad, 60),
        Identificador("@p1", r.EntidadId),
        Numero("@p2", r.AccionId),
        Identificador("@p3", r.UsuarioId),
        Texto("@p4", r.NombreUsuario, 150),
        Texto("@p5", r.Ip, 45),
        Texto("@p6", r.Agente, 300),
        Texto("@p7", r.TraceId, 64),
        Texto("@p8", r.CamposModificados, 1000),
        Texto("@p9", r.ValorAnteriorJson, SinLongitud),
        Texto("@p10", r.ValorNuevoJson, SinLongitud)
    ];

    /// <summary>Longitud -1: <c>nvarchar(max)</c>, que es lo que son los dos JSON.</summary>
    private const int SinLongitud = -1;

    private static SqlParameter Texto(string nombre, string? valor, int longitud) =>
        new(nombre, SqlDbType.NVarChar, longitud) { Value = (object?)valor ?? DBNull.Value };

    private static SqlParameter Identificador(string nombre, Guid? valor) =>
        new(nombre, SqlDbType.UniqueIdentifier) { Value = (object?)valor ?? DBNull.Value };

    private static SqlParameter Numero(string nombre, byte valor) =>
        new(nombre, SqlDbType.TinyInt) { Value = valor };
}

/// <summary>
/// Los <c>Codigo</c> de <c>dbo.AccionAuditoriaCatalogo</c> que el interceptor deduce solo del
/// <c>EntityState</c>. Mismo criterio que <c>CodigosMetodoCosteo</c>: no es un catálogo quemado, es el
/// contrato entre la tabla y el punto del código que tiene que nombrarla.
/// <para>Las acciones que dependen de una <b>intención</b> de negocio —anular, aprobar, rechazar,
/// exportar, sincronizar— <b>no</b> están aquí. El estado de la entidad no dice que una cotización se
/// anuló: dice que se actualizó. Esas las escriben los servicios, y esa es la diferencia entre una
/// bitácora que registra hechos y una que registra intenciones.</para>
/// </summary>
internal static class AccionesAuditoria
{
    public const string Creado = "CREADO";

    public const string Actualizado = "ACTUALIZADO";

    public const string Eliminado = "ELIMINADO";

    public const string Restaurado = "RESTAURADO";
}
