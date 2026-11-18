using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Plantillas;

/// <summary>Una plantilla para la grilla: cabecera legacy + conteos + nombre de cliente.</summary>
public sealed record PlantillaResumen(
    PlantillaOrdenTrabajo Cabecera,
    int Detalles,
    int Servicios,
    int Equipos,
    string? ClienteNombre);

/// <summary>Una plantilla completa: cabecera legacy y sus tres colecciones en las tablas originales.</summary>
public sealed record PlantillaCompleta(
    PlantillaOrdenTrabajo Cabecera,
    IReadOnlyList<PlantillaOrdenTrabajoDetalle> Detalles,
    IReadOnlyList<PlantillaOrdenTrabajoServicio> Servicios,
    IReadOnlyList<PlantillaOrdenTrabajoEquipos> Equipos,
    string? ClienteNombre);

/// <summary>Datos editables de una plantilla. Las claves (<c>PLANTILLA</c>, <c>CONSECUTIVO</c>) las
/// asigna el servicio, igual que hacía <c>PlantillasRepository</c> con <c>MAX + 1</c>.</summary>
public sealed record PlantillaGuardar(
    string? Cliente,
    string? Locacion,
    string? Direccion,
    string? Tecnico,
    string? Contacto,
    string? Telefono,
    string? Vehiculo,
    string? DescripTrabajo,
    string? Notas,
    string? Activo,
    IReadOnlyList<PlantillaOrdenTrabajoDetalle> Detalles,
    IReadOnlyList<PlantillaOrdenTrabajoServicio> Servicios,
    IReadOnlyList<PlantillaOrdenTrabajoEquipos> Equipos);

/// <summary>
/// Gestión de las plantillas del WinForms sobre <b>sus propias tablas</b>:
/// <c>Plantilla_Orden_Trabajo</c>, <c>_Detalle</c>, <c>_Servicio</c> y <c>_Equipos</c> (D-22).
/// <para>
/// No hay tablas paralelas ni migración de datos: lo que se migró es esta lógica (antes en
/// <c>PlantillasRepository</c> y <c>FrmCrudPlantillas</c>), respetando claves, códigos ERP,
/// <c>ACTIVO = SI/NO</c> y los consecutivos <c>MAX + 1</c> del legacy.
/// </para>
/// </summary>
public sealed class PlantillaService
{
    private const string ActivoSi = "SI";
    private const string ActivoNo = "NO";

    private readonly CotizadorDbContext _contexto;
    private readonly IContextoUsuario _usuario;

    public PlantillaService(CotizadorDbContext contexto, IContextoUsuario? usuario = null)
    {
        _contexto = contexto;
        _usuario = usuario ?? new ContextoUsuarioSistema();
    }

    // ── Lectura ───────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PlantillaResumen>> ListarAsync(
        string? texto = null,
        string? cliente = null,
        bool soloActivas = true,
        CancellationToken cancellationToken = default)
    {
        var consulta = _contexto.PlantillaOrdenTrabajo.AsNoTracking();

        if (soloActivas)
        {
            consulta = consulta.Where(p => p.Activo == ActivoSi);
        }

        if (!string.IsNullOrWhiteSpace(cliente))
        {
            consulta = consulta.Where(p => p.Cliente == cliente.Trim());
        }

        var filtro = texto?.Trim();
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            // El legacy buscaba por nombre de cliente y descripción. Acá además se busca por el
            // número de plantilla cuando el filtro es numérico (la grilla nueva lo muestra).
            var esNumero = long.TryParse(filtro, out var numero);
            consulta = consulta.Where(p =>
                (p.DescripTrabajo ?? string.Empty).Contains(filtro) ||
                (p.Cliente ?? string.Empty).Contains(filtro) ||
                (p.Locacion ?? string.Empty).Contains(filtro) ||
                (esNumero && p.Plantilla == numero));
        }

        var cabeceras = await consulta
            .OrderBy(p => p.Plantilla)
            .ToListAsync(cancellationToken);

        if (cabeceras.Count == 0)
        {
            return [];
        }

        // Las hijas se cuentan por rango de PLANTILLA en vez de por IN (...): hay más de 3.500
        // plantillas y un IN con todos los ids supera el límite de parámetros de SQL Server.
        var desde = LimiteInt(cabeceras.Min(p => p.Plantilla));
        var hasta = LimiteInt(cabeceras.Max(p => p.Plantilla));

        var detalles = await ConteoDetallesAsync(desde, hasta, cancellationToken);
        var servicios = await ConteoServiciosAsync(desde, hasta, cancellationToken);
        var equipos = await ConteoEquiposAsync(desde, hasta, cancellationToken);

        var nombresCliente = await NombresClienteAsync(
            cabeceras.Select(p => p.Cliente).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(),
            cancellationToken);

        return cabeceras.Select(p => new PlantillaResumen(
            p,
            detalles.GetValueOrDefault(p.Plantilla),
            servicios.GetValueOrDefault(p.Plantilla),
            equipos.GetValueOrDefault(p.Plantilla),
            p.Cliente is null ? null : nombresCliente.GetValueOrDefault(p.Cliente))).ToList();
    }

    public async Task<PlantillaCompleta?> ObtenerAsync(
        long plantilla,
        CancellationToken cancellationToken = default)
    {
        var cabecera = await _contexto.PlantillaOrdenTrabajo
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Plantilla == plantilla, cancellationToken);

        if (cabecera is null)
        {
            return null;
        }

        return await CompletaAsync(cabecera, cancellationToken);
    }

    // ── Escritura ─────────────────────────────────────────────────────────────────

    public async Task<PlantillaCompleta> CrearAsync(
        PlantillaGuardar datos,
        CancellationToken cancellationToken = default)
    {
        Validar(datos);
        await ValidarAsync(datos, null, cancellationToken);

        var numero = await SiguientePlantillaAsync(cancellationToken);
        var cabecera = new PlantillaOrdenTrabajo
        {
            Plantilla = numero,
            Cliente = Limite(datos.Cliente, 20),
            Locacion = Limite(datos.Locacion, 5),
            Direccion = Limite(datos.Direccion, 100),
            // El WinForms dejaba TECNICO y VEHICULO vacíos en el alta (FrmCrudPlantillas).
            Tecnico = Limite(datos.Tecnico, 4) ?? string.Empty,
            Contacto = Limite(datos.Contacto, 35),
            Telefono = Limite(datos.Telefono, 10),
            Vehiculo = Limite(datos.Vehiculo, 4) ?? string.Empty,
            DescripTrabajo = Limite(datos.DescripTrabajo, 100),
            Notas = Limite(datos.Notas, 100),
            Activo = ActivoSi,
            FechaCreacion = DateOnly.FromDateTime(DateTime.Today),
            UsuarioCreacion = UsuarioCreacion()
        };

        _contexto.PlantillaOrdenTrabajo.Add(cabecera);
        await GuardarHijosAsync(numero, datos, cancellationToken);
        await _contexto.SaveChangesAsync(cancellationToken);

        return (await ObtenerAsync(numero, cancellationToken))!;
    }

    public async Task<PlantillaCompleta> ActualizarAsync(
        long plantilla,
        PlantillaGuardar datos,
        bool activo,
        CancellationToken cancellationToken = default)
    {
        var cabecera = await _contexto.PlantillaOrdenTrabajo
            .FirstOrDefaultAsync(p => p.Plantilla == plantilla, cancellationToken)
            ?? throw new InvalidOperationException($"No existe la plantilla {plantilla}.");

        await ValidarAsync(datos, await CompletaAsync(cabecera, cancellationToken), cancellationToken);

        cabecera.Cliente = Limite(datos.Cliente, 20);
        cabecera.Locacion = Limite(datos.Locacion, 5);
        cabecera.Direccion = Limite(datos.Direccion, 100);
        cabecera.Tecnico = Limite(datos.Tecnico, 4);
        cabecera.Contacto = Limite(datos.Contacto, 35);
        cabecera.Telefono = Limite(datos.Telefono, 10);
        cabecera.Vehiculo = Limite(datos.Vehiculo, 4);
        cabecera.DescripTrabajo = Limite(datos.DescripTrabajo, 100);
        cabecera.Notas = Limite(datos.Notas, 100);
        cabecera.Activo = activo ? ActivoSi : ActivoNo;
        // FECHA_CREACION y USUARIO_CREACION son auditoría del alta: no se tocan.

        await GuardarHijosAsync(plantilla, datos, cancellationToken);
        await _contexto.SaveChangesAsync(cancellationToken);

        return (await ObtenerAsync(plantilla, cancellationToken))!;
    }

    /// <summary>
    /// Copia la cabecera y las tres colecciones con un número nuevo. A diferencia del copiador
    /// del WinForms, <c>MAXIMO_PERMITIDO</c> se conserva.
    /// </summary>
    public async Task<PlantillaCompleta> CopiarAsync(
        long plantilla,
        string? descripcionTrabajo = null,
        CancellationToken cancellationToken = default)
    {
        var original = await ObtenerAsync(plantilla, cancellationToken)
            ?? throw new InvalidOperationException($"No existe la plantilla {plantilla}.");

        var datos = new PlantillaGuardar(
            original.Cabecera.Cliente,
            original.Cabecera.Locacion,
            original.Cabecera.Direccion,
            original.Cabecera.Tecnico,
            original.Cabecera.Contacto,
            original.Cabecera.Telefono,
            original.Cabecera.Vehiculo,
            descripcionTrabajo ?? original.Cabecera.DescripTrabajo,
            original.Cabecera.Notas,
            ActivoSi,
            original.Detalles.Select(d => new PlantillaOrdenTrabajoDetalle
            {
                CodigoArticulo = d.CodigoArticulo,
                DescripArticulo = d.DescripArticulo,
                TipoPlaga = d.TipoPlaga,
                LugarTratar = d.LugarTratar,
                Area = d.Area,
                Concentracion = d.Concentracion,
                CodigoDosificacion = d.CodigoDosificacion,
                MetodoAplicacion = d.MetodoAplicacion,
                Cantidad = d.Cantidad,
                Umedida = d.Umedida,
                CostoUnitario = d.CostoUnitario,
                CostoTotal = d.CostoTotal,
                MaximoPermitido = d.MaximoPermitido
            }).ToList(),
            original.Servicios.Select(s => new PlantillaOrdenTrabajoServicio
            {
                CodigoServicio = s.CodigoServicio,
                CodigoFrecuencia = s.CodigoFrecuencia
            }).ToList(),
            original.Equipos.Select(e => new PlantillaOrdenTrabajoEquipos
            {
                CodigoEquipo = e.CodigoEquipo,
                Utilizado = e.Utilizado
            }).ToList());

        return await CrearAsync(datos, cancellationToken);
    }

    /// <summary>
    /// Baja lógica: <c>ACTIVO = NO</c>, el mismo código que usa el WinForms. No borra la fila ni
    /// sus líneas, para no destruir datos reales del ERP ni romper <c>Plantilla_Calendario</c>.
    /// </summary>
    public async Task DarDeBajaAsync(
        long plantilla,
        CancellationToken cancellationToken = default)
    {
        var cabecera = await _contexto.PlantillaOrdenTrabajo
            .FirstOrDefaultAsync(p => p.Plantilla == plantilla, cancellationToken)
            ?? throw new InvalidOperationException($"No existe la plantilla {plantilla}.");

        cabecera.Activo = ActivoNo;
        await _contexto.SaveChangesAsync(cancellationToken);
    }

    // ── Internos ──────────────────────────────────────────────────────────────────

    private async Task<PlantillaCompleta> CompletaAsync(
        PlantillaOrdenTrabajo cabecera,
        CancellationToken cancellationToken)
    {
        var numero = LimiteInt(cabecera.Plantilla);

        var detalles = await _contexto.PlantillaOrdenTrabajoDetalle
            .AsNoTracking()
            .Where(d => d.Plantilla == numero)
            .OrderBy(d => d.Consecutivo)
            .ToListAsync(cancellationToken);
        var servicios = await _contexto.PlantillaOrdenTrabajoServicio
            .AsNoTracking()
            .Where(s => s.Plantilla == numero)
            .OrderBy(s => s.Consecutivo)
            .ToListAsync(cancellationToken);
        var equipos = await _contexto.PlantillaOrdenTrabajoEquipos
            .AsNoTracking()
            .Where(e => e.Plantilla == numero)
            .OrderBy(e => e.Consecutivo)
            .ToListAsync(cancellationToken);

        string? nombreCliente = null;
        if (!string.IsNullOrWhiteSpace(cabecera.Cliente))
        {
            var nombres = await NombresClienteAsync([cabecera.Cliente], cancellationToken);
            nombreCliente = nombres.GetValueOrDefault(cabecera.Cliente);
        }

        return new PlantillaCompleta(cabecera, detalles, servicios, equipos, nombreCliente);
    }

    private async Task<int> GuardarHijosAsync(
        long plantilla,
        PlantillaGuardar datos,
        CancellationToken cancellationToken)
    {
        var numero = LimiteInt(plantilla);

        var detallesActuales = await _contexto.PlantillaOrdenTrabajoDetalle
            .Where(d => d.Plantilla == numero).OrderBy(d => d.Consecutivo).ToListAsync(cancellationToken);
        var serviciosActuales = await _contexto.PlantillaOrdenTrabajoServicio
            .Where(s => s.Plantilla == numero).OrderBy(s => s.Consecutivo).ToListAsync(cancellationToken);
        var equiposActuales = await _contexto.PlantillaOrdenTrabajoEquipos
            .Where(e => e.Plantilla == numero).OrderBy(e => e.Consecutivo).ToListAsync(cancellationToken);

        var detallesNuevos = datos.Detalles.OrderBy(d => d.Consecutivo).ToList();
        for (var i = 0; i < detallesNuevos.Count; i++)
        {
            var origen = detallesNuevos[i];
            var consecutivo = i + 1;
            var destino = detallesActuales.FirstOrDefault(d => d.Consecutivo == consecutivo);
            if (destino is null)
            {
                destino = new PlantillaOrdenTrabajoDetalle { Plantilla = numero, Consecutivo = consecutivo };
                _contexto.PlantillaOrdenTrabajoDetalle.Add(destino);
            }

            destino.CodigoArticulo = Limite(origen.CodigoArticulo, 10);
            destino.DescripArticulo = Limite(origen.DescripArticulo, 50) ?? string.Empty;
            destino.TipoPlaga = Limite(origen.TipoPlaga, 4);
            destino.LugarTratar = Limite(origen.LugarTratar, 100);
            destino.Area = origen.Area is { } area ? Math.Round(area, 2) : null;
            destino.Concentracion = Limite(origen.Concentracion, 20);
            destino.CodigoDosificacion = Limite(origen.CodigoDosificacion, 4);
            destino.MetodoAplicacion = Limite(origen.MetodoAplicacion, 4);
            destino.Cantidad = origen.Cantidad is { } cantidad ? Math.Round(cantidad, 0) : null;
            destino.Umedida = Limite(origen.Umedida, 6);
            destino.CostoUnitario = origen.CostoUnitario is { } unitario ? Math.Round(unitario, 6) : null;
            // El legacy calculaba el total en el formulario: CANTIDAD × COSTO_UNITARIO.
            destino.CostoTotal = Math.Round((origen.Cantidad ?? 0) * (origen.CostoUnitario ?? 0), 2);
            destino.MaximoPermitido = origen.MaximoPermitido is { } maximo ? Math.Round(maximo, 0) : null;
        }

        var serviciosNuevos = datos.Servicios.OrderBy(s => s.Consecutivo).ToList();
        for (var i = 0; i < serviciosNuevos.Count; i++)
        {
            var origen = serviciosNuevos[i];
            var consecutivo = i + 1;
            var codigoServicio = Limite(origen.CodigoServicio, 4) ?? string.Empty;
            var codigoFrecuencia = Limite(origen.CodigoFrecuencia, 4) ?? string.Empty;
            var destino = serviciosActuales.ElementAtOrDefault(i);

            // CODIGO_SERVICIO y CODIGO_FRECUENCIA son parte de la PK legacy: si cambian, la fila
            // se sustituye en lugar de intentar modificar la clave de una entidad rastreada.
            if (destino is not null &&
                (destino.CodigoServicio != codigoServicio || destino.CodigoFrecuencia != codigoFrecuencia))
            {
                _contexto.PlantillaOrdenTrabajoServicio.Remove(destino);
                destino = null;
            }

            if (destino is null)
            {
                _contexto.PlantillaOrdenTrabajoServicio.Add(new PlantillaOrdenTrabajoServicio
                {
                    Plantilla = numero,
                    Consecutivo = consecutivo,
                    CodigoServicio = codigoServicio,
                    CodigoFrecuencia = codigoFrecuencia
                });
                continue;
            }

            destino.CodigoServicio = codigoServicio;
            destino.CodigoFrecuencia = codigoFrecuencia;
        }

        var equiposNuevos = datos.Equipos.OrderBy(e => e.Consecutivo).ToList();
        for (var i = 0; i < equiposNuevos.Count; i++)
        {
            var origen = equiposNuevos[i];
            var consecutivo = i + 1;
            var codigoEquipo = Limite(origen.CodigoEquipo, 4);
            var utilizado = string.IsNullOrWhiteSpace(origen.Utilizado)
                ? ActivoSi
                : CodigoSiNo(origen.Utilizado);
            var destino = equiposActuales.ElementAtOrDefault(i);
            if (destino is null)
            {
                _contexto.PlantillaOrdenTrabajoEquipos.Add(new PlantillaOrdenTrabajoEquipos
                {
                    Plantilla = numero,
                    Consecutivo = consecutivo,
                    CodigoEquipo = codigoEquipo,
                    Utilizado = utilizado
                });
                continue;
            }

            destino.CodigoEquipo = codigoEquipo;
            destino.Utilizado = utilizado;
        }

        _contexto.PlantillaOrdenTrabajoDetalle.RemoveRange(
            detallesActuales.Where(d => d.Consecutivo > detallesNuevos.Count));
        _contexto.PlantillaOrdenTrabajoServicio.RemoveRange(
            serviciosActuales.Skip(serviciosNuevos.Count));
        _contexto.PlantillaOrdenTrabajoEquipos.RemoveRange(
            equiposActuales.Skip(equiposNuevos.Count));

        return numero;
    }

    private async Task<long> SiguientePlantillaAsync(CancellationToken cancellationToken)
    {
        // MAX(PLANTILLA) + 1, igual que getPlantillaOrdenTrabajoId() del legacy. Max sobre un
        // nullable devuelve null con la tabla vacía, así que no hace falta DefaultIfEmpty (que
        // no se traduce en todos los providers).
        var maximo = await _contexto.PlantillaOrdenTrabajo
            .MaxAsync(p => (long?)p.Plantilla, cancellationToken);
        return (maximo ?? 0) + 1;
    }

    private async Task<IReadOnlyDictionary<long, int>> ConteoDetallesAsync(
        int desde, int hasta, CancellationToken cancellationToken)
    {
        var filas = await _contexto.PlantillaOrdenTrabajoDetalle.AsNoTracking()
            .Where(d => d.Plantilla >= desde && d.Plantilla <= hasta)
            .GroupBy(d => d.Plantilla)
            .Select(g => new { Plantilla = g.Key, Total = g.Count() })
            .ToListAsync(cancellationToken);
        return filas.ToDictionary(f => (long)f.Plantilla, f => f.Total);
    }

    private async Task<IReadOnlyDictionary<long, int>> ConteoServiciosAsync(
        int desde, int hasta, CancellationToken cancellationToken)
    {
        var filas = await _contexto.PlantillaOrdenTrabajoServicio.AsNoTracking()
            .Where(s => s.Plantilla >= desde && s.Plantilla <= hasta)
            .GroupBy(s => s.Plantilla)
            .Select(g => new { Plantilla = g.Key, Total = g.Count() })
            .ToListAsync(cancellationToken);
        return filas.ToDictionary(f => (long)f.Plantilla, f => f.Total);
    }

    private async Task<IReadOnlyDictionary<long, int>> ConteoEquiposAsync(
        int desde, int hasta, CancellationToken cancellationToken)
    {
        var filas = await _contexto.PlantillaOrdenTrabajoEquipos.AsNoTracking()
            .Where(e => e.Plantilla >= desde && e.Plantilla <= hasta)
            .GroupBy(e => e.Plantilla)
            .Select(g => new { Plantilla = g.Key, Total = g.Count() })
            .ToListAsync(cancellationToken);
        return filas.ToDictionary(f => (long)f.Plantilla, f => f.Total);
    }

    private async Task<IReadOnlyDictionary<string, string>> NombresClienteAsync(
        IEnumerable<string?> codigos,
        CancellationToken cancellationToken)
    {
        var lista = codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct().ToList();
        if (lista.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        return await _contexto.Cliente
            .AsNoTracking()
            .Where(c => lista.Contains(c.Codigo))
            .Select(c => new { c.Codigo, c.Nombre })
            .ToDictionaryAsync(c => c.Codigo, c => c.Nombre, cancellationToken);
    }

    private string UsuarioCreacion()
    {
        var nombre = string.IsNullOrWhiteSpace(_usuario.NombreUsuario)
            ? "SISTEMA"
            : _usuario.NombreUsuario.Trim();
        return Limite(nombre, 25)!;
    }

    /// <summary>
    /// Valida los códigos contra las vistas <c>vv*</c> de origen (D-20). Un código que ya estaba
    /// grabado se acepta aunque hoy no esté activo: así se puede editar una plantilla legacy sin
    /// quedar atrapada por un catálogo que cambió después. Los códigos nuevos sí deben existir.
    /// La validación estructural (hijos obligatorios) sigue siendo solo del alta, como en el WinForms.
    /// </summary>
    private async Task ValidarAsync(
        PlantillaGuardar datos,
        PlantillaCompleta? actual,
        CancellationToken cancellationToken)
    {
        var locaciones = await ConjuntoAsync(
            _contexto.VistaLocacionErp.AsNoTracking().Where(v => v.Activo == ActivoSi).Select(v => v.Codigo),
            cancellationToken);
        var vehiculos = await ConjuntoAsync(
            _contexto.VistaVehiculoErp.AsNoTracking().Where(v => v.Activo == ActivoSi).Select(v => v.Codigo),
            cancellationToken);
        var plagas = await ConjuntoAsync(
            _contexto.VistaPlagaErp.AsNoTracking().Where(v => v.Activo == ActivoSi).Select(v => v.Codigo),
            cancellationToken);
        var ubicaciones = await ConjuntoAsync(
            _contexto.VistaUbicacionErp.AsNoTracking().Where(v => v.Activo == ActivoSi).Select(v => v.Codigo),
            cancellationToken);
        var dosificaciones = await ConjuntoAsync(
            _contexto.VistaDosificacionErp.AsNoTracking().Where(v => v.Activo == ActivoSi).Select(v => v.Codigo),
            cancellationToken);
        var metodos = await ConjuntoAsync(
            _contexto.VistaMetodoAplicacionErp.AsNoTracking().Where(v => v.Activo == ActivoSi).Select(v => v.Codigo),
            cancellationToken);
        var frecuencias = await ConjuntoAsync(
            _contexto.VistaFrecuenciaErp.AsNoTracking().Where(v => v.Activo == ActivoSi).Select(v => v.Codigo),
            cancellationToken);
        var equipos = await ConjuntoAsync(
            _contexto.VistaEquipoErp.AsNoTracking().Where(v => v.Activo == ActivoSi).Select(v => v.Codigo),
            cancellationToken);

        var anterioresPlagas = Conjunto(actual?.Detalles.Select(d => d.TipoPlaga));
        var anterioresUbicaciones = Conjunto(actual?.Detalles.Select(d => d.LugarTratar));
        var anterioresDosificaciones = Conjunto(actual?.Detalles.Select(d => d.CodigoDosificacion));
        var anterioresMetodos = Conjunto(actual?.Detalles.Select(d => d.MetodoAplicacion));
        var anterioresFrecuencias = Conjunto(actual?.Servicios.Select(s => s.CodigoFrecuencia));
        var anterioresEquipos = Conjunto(actual?.Equipos.Select(e => e.CodigoEquipo));

        RequerirCodigo("Locación", datos.Locacion, locaciones, Conjunto(new[] { actual?.Cabecera.Locacion }));
        RequerirCodigo("Vehículo", datos.Vehiculo, vehiculos, Conjunto(new[] { actual?.Cabecera.Vehiculo }));

        foreach (var detalle in datos.Detalles)
        {
            RequerirCodigo("Tipo de plaga", detalle.TipoPlaga, plagas, anterioresPlagas);
            RequerirCodigo("Lugar a tratar", detalle.LugarTratar, ubicaciones, anterioresUbicaciones);
            RequerirCodigo("Dosificación", detalle.CodigoDosificacion, dosificaciones, anterioresDosificaciones);
            RequerirCodigo("Método de aplicación", detalle.MetodoAplicacion, metodos, anterioresMetodos);
        }

        foreach (var servicio in datos.Servicios)
        {
            RequerirCodigo("Frecuencia", servicio.CodigoFrecuencia, frecuencias, anterioresFrecuencias);
        }

        foreach (var equipo in datos.Equipos)
        {
            RequerirCodigo("Equipo", equipo.CodigoEquipo, equipos, anterioresEquipos);
        }
    }

    private static async Task<HashSet<string>> ConjuntoAsync(
        IQueryable<string> codigos,
        CancellationToken cancellationToken)
        => Conjunto(await codigos.ToListAsync(cancellationToken));

    private static HashSet<string> Conjunto(IEnumerable<string?>? valores)
        => new(valores?.Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase) ?? [], StringComparer.OrdinalIgnoreCase);

    private static void RequerirCodigo(
        string campo,
        string? codigo,
        HashSet<string> validos,
        HashSet<string> anteriores)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return;
        }

        var texto = codigo.Trim();
        if (validos.Contains(texto) || anteriores.Contains(texto))
        {
            return;
        }

        throw new InvalidOperationException(
            $"{campo}: el código '{texto}' no existe o está inactivo en el catálogo ERP.");
    }

    private static void Validar(PlantillaGuardar datos)
    {
        if (string.IsNullOrWhiteSpace(datos.Cliente))
        {
            throw new InvalidOperationException("La plantilla requiere un cliente.");
        }

        if (string.IsNullOrWhiteSpace(datos.Locacion))
        {
            throw new InvalidOperationException("La plantilla requiere una locación.");
        }

        if (datos.Detalles.Count == 0 || datos.Servicios.Count == 0 || datos.Equipos.Count == 0)
        {
            throw new InvalidOperationException(
                "La plantilla requiere al menos un producto, un servicio y un equipo.");
        }
    }

    private static string CodigoSiNo(string valor)
        => valor.Trim().Equals("SI", StringComparison.OrdinalIgnoreCase) ||
           valor.Trim().Equals("S", StringComparison.OrdinalIgnoreCase) ||
           valor.Trim().Equals("TRUE", StringComparison.OrdinalIgnoreCase)
            ? ActivoSi
            : ActivoNo;

    private static string? Limite(string? valor, int longitud)
        => valor is null ? null : valor.Trim().Length <= longitud ? valor.Trim() : valor.Trim()[..longitud];

    private static int LimiteInt(long valor)
        => (int)Math.Min(valor, int.MaxValue);
}
