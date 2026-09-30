using System.Collections.Concurrent;

using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Carga;

/// <summary>
/// Carga el catálogo de clientes desde un origen externo. Hace upsert idempotente por
/// <c>CodigoExterno</c> con la misma mecánica que <see cref="CargadorProductos"/>: inserta lo
/// nuevo, actualiza lo que cambió, omite lo que tiene el mismo hash y adopta lo que se capturó a
/// mano con el mismo código.
/// <para>El cliente de ORKIN también vive en su propio ERP, y cotizarle a alguien que ya es
/// cliente exige su ficha —no solo el nombre—, así que el flujo es por columna y no por
/// catálogo fijo: la vista del cliente decide qué traer.</para>
/// </summary>
public sealed class CargadorClientes : ICargadorCatalogo
{
    private readonly CotizadorDbContext _contexto;

    public CargadorClientes(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    public string Catalogo => "CLIENTES";

    public IReadOnlyList<CampoEsperado> CamposEsperados =>
    [
        new("Codigo", "texto", true, "El código del cliente en el ERP del cliente"),
        new("Nombre", "texto", true, "El nombre del cliente"),
        new("TipoClienteCodigo", "texto", true, "EMPRESARIAL o RESIDENCIAL. Decide el método de costeo de sus cotizaciones."),
        new("RazonSocial", "texto", false, "Solo en clientes empresariales."),
        new("CedulaORuc", "texto", false, "Cédula o RUC del cliente."),
        new("IdentificacionFiscal", "texto", false, "RT/NIT si el ERP lo trae."),
        new("Direccion", "texto", false, "Dirección del cliente."),
        new("Ciudad", "texto", false, "Ciudad."),
        new("Departamento", "texto", false, "Departamento."),
        new("Telefono", "texto", false, "Teléfono de contacto."),
        new("Correo", "texto", false, "Correo para enviar la cotización."),
        new("ContactoNombre", "texto", false, "Nombre de la persona de contacto."),
        new("ContactoCargo", "texto", false, "Cargo de la persona de contacto."),
        new("SitioWeb", "texto", false, "Sitio web."),
        new("Notas", "texto", false, "Notas internas."),
    ];

    // Caché de nombres de tipo de cliente para el diff de la simulación. Vive en el cargador y
    // no en el bucle por la misma razón que en CargadorProductos: su vida útil es la de la
    // carga, y el cargador es scoped.
    private readonly ConcurrentDictionary<byte, string> _nombresTipo = new();

    public async Task<ResultadoCarga> CargarAsync(
        IReadOnlyList<Dictionary<string, object?>> filas,
        ConfiguracionCarga configuracion,
        CancellationToken cancellationToken = default)
    {
        var insertados = 0;
        var actualizados = 0;
        var omitidos = 0;
        var rechazados = new List<RechazoCarga>();

        // Índice de tipos de cliente por código, para resolver la FK sin consultar por fila.
        var tipos = await _contexto.TipoClienteCatalogo
            .AsNoTracking()
            .Where(t => t.EsActivo)
            .ToDictionaryAsync(t => t.Codigo, t => t.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // El origen del dato se resuelve por código y no por id fijo, como el origen del precio
        // en productos: el id depende del orden de la semilla.
        var origenDatoId = await _contexto.OrigenDatoCatalogo
            .AsNoTracking()
            .Where(o => o.Codigo == "ERP")
            .Select(o => o.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (origenDatoId == 0)
        {
            throw new InvalidOperationException(
                "No está sembrado el catálogo OrigenDato (falta ERP). Corra la semilla.");
        }

        // Filas existentes por CodigoExterno, para decidir insert vs update vs omit. Acotado
        // por conexión por la misma razón que en productos: dos ERPs distintos con el mismo
        // "CLI-0001" son dos clientes distintos y no se pisan entre sí.
        var codigosExternos = new List<string>();
        foreach (var fila in filas)
        {
            var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);
            var codigo = Normalizacion.AString(mapeada.GetValueOrDefault("Codigo"));
            if (codigo is not null) codigosExternos.Add(codigo);
        }

        var existentes = await _contexto.Cliente
            .Where(c => c.ErpConexionId == configuracion.ErpConexionId
                     && c.CodigoExterno != null
                     && codigosExternos.Contains(c.CodigoExterno))
            .ToDictionaryAsync(c => c.CodigoExterno!, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // La adopción: la ficha se capturó a mano antes de que existiera la conexión y el ERP
        // trae al mismo cliente con el mismo código. Sin esto, `Codigo` único rechazaría el
        // insert con UX_Cliente_Codigo. Solo se adoptan filas sin conexión.
        var huerfanas = await _contexto.Cliente
            .Where(c => c.ErpConexionId == null && codigosExternos.Contains(c.Codigo))
            .ToDictionaryAsync(c => c.Codigo, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var numeroFila = 1;
        var simulados = new List<CambioSimulado>();

        // La misma memoria de hashes que en productos: para que un código repetido en el origen
        // se comporte en simulación igual que en la carga real.
        var hashesSimulados = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fila in filas)
        {
            try
            {
                var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);

                var codigo = Normalizacion.AString(mapeada.GetValueOrDefault("Codigo"))
                    ?? throw new FormatException("El campo 'Codigo' es obligatorio.");

                var nombre = Normalizacion.AString(mapeada.GetValueOrDefault("Nombre"))
                    ?? throw new FormatException("El campo 'Nombre' es obligatorio.");

                // El tipo de cliente decide el método de costeo (RN-04 / RN-07). Si la vista no
                // lo trae, quedarse callado y poner uno por defecto es cotizarle a alguien con
                // el margen de otro tipo de cliente: mejor que la fila se rechace y se vea.
                var tipoCodigo = Normalizacion.AString(mapeada.GetValueOrDefault("TipoClienteCodigo"))
                    ?? throw new FormatException("El campo 'TipoClienteCodigo' es obligatorio (EMPRESARIAL o RESIDENCIAL).");

                if (!tipos.TryGetValue(tipoCodigo, out var tipoId))
                {
                    throw new FormatException($"El tipo de cliente '{tipoCodigo}' no existe. Debe ser EMPRESARIAL o RESIDENCIAL.");
                }

                var razonSocial = Normalizacion.AString(mapeada.GetValueOrDefault("RazonSocial"));
                var cedulaRuc = Normalizacion.AString(mapeada.GetValueOrDefault("CedulaORuc"));
                var identFiscal = Normalizacion.AString(mapeada.GetValueOrDefault("IdentificacionFiscal"));
                var direccion = Normalizacion.AString(mapeada.GetValueOrDefault("Direccion"));
                var ciudad = Normalizacion.AString(mapeada.GetValueOrDefault("Ciudad"));
                var departamento = Normalizacion.AString(mapeada.GetValueOrDefault("Departamento"));
                var telefono = Normalizacion.AString(mapeada.GetValueOrDefault("Telefono"));
                var correo = Normalizacion.AString(mapeada.GetValueOrDefault("Correo"));
                var contactoNombre = Normalizacion.AString(mapeada.GetValueOrDefault("ContactoNombre"));
                var contactoCargo = Normalizacion.AString(mapeada.GetValueOrDefault("ContactoCargo"));
                var sitioWeb = Normalizacion.AString(mapeada.GetValueOrDefault("SitioWeb"));
                var notas = Normalizacion.AString(mapeada.GetValueOrDefault("Notas"));

                var hash = Normalizacion.HashContenido(mapeada);

                // La decisión se toma SIEMPRE igual y lo único que se bifurca es si se aplica:
                // la simulación es la misma carga con el último paso cortado.
                if (existentes.TryGetValue(codigo, out var existente))
                {
                    var hashExistente = hashesSimulados.TryGetValue(codigo, out var yaSimulado)
                        ? yaSimulado
                        : existente?.HashContenido is { Length: > 0 }
                            ? Convert.ToHexString(existente.HashContenido)
                            : null;

                    if (hashExistente == hash)
                    {
                        omitidos++;

                        if (configuracion.EsSimulacion)
                        {
                            simulados.Add(new CambioSimulado(numeroFila, codigo, AccionSimulada.Omitir, []));
                        }
                    }
                    else
                    {
                        var cambios = new List<CampoSimulado>();

                        if (existente.Nombre != nombre)
                            cambios.Add(new CampoSimulado("Nombre", existente.Nombre, nombre));
                        if (existente.RazonSocial != razonSocial)
                            cambios.Add(new CampoSimulado("Razón social", existente.RazonSocial, razonSocial));
                        if (existente.TipoClienteId != tipoId)
                            cambios.Add(new CampoSimulado("Tipo de cliente", await NombreTipoAsync(tipoId, cancellationToken), await NombreTipoAsync(existente.TipoClienteId, cancellationToken)));
                        if (existente.CedulaORuc != cedulaRuc)
                            cambios.Add(new CampoSimulado("Cédula/RUC", existente.CedulaORuc, cedulaRuc));
                        if (existente.IdentificacionFiscal != identFiscal)
                            cambios.Add(new CampoSimulado("Identificación fiscal", existente.IdentificacionFiscal, identFiscal));
                        if (existente.Direccion != direccion)
                            cambios.Add(new CampoSimulado("Dirección", existente.Direccion, direccion));
                        if (existente.Ciudad != ciudad)
                            cambios.Add(new CampoSimulado("Ciudad", existente.Ciudad, ciudad));
                        if (existente.Departamento != departamento)
                            cambios.Add(new CampoSimulado("Departamento", existente.Departamento, departamento));
                        if (existente.Telefono != telefono)
                            cambios.Add(new CampoSimulado("Teléfono", existente.Telefono, telefono));
                        if (existente.Correo != correo)
                            cambios.Add(new CampoSimulado("Correo", existente.Correo, correo));

                        if (configuracion.EsSimulacion)
                        {
                            simulados.Add(new CambioSimulado(numeroFila, codigo, AccionSimulada.Actualizar, cambios));
                            hashesSimulados[codigo] = hash;
                        }
                        else
                        {
                            existente.Nombre = nombre;
                            existente.RazonSocial = razonSocial;
                            existente.TipoClienteId = tipoId;
                            existente.CedulaORuc = cedulaRuc;
                            existente.IdentificacionFiscal = identFiscal;
                            existente.Direccion = direccion;
                            existente.Ciudad = ciudad;
                            existente.Departamento = departamento;
                            existente.Telefono = telefono;
                            existente.Correo = correo;
                            existente.ContactoNombre = contactoNombre;
                            existente.ContactoCargo = contactoCargo;
                            existente.SitioWeb = sitioWeb;
                            existente.Notas = notas;
                            existente.HashContenido = Convert.FromHexString(hash);
                            existente.UltimaSyncUtc = DateTime.UtcNow;
                            existente.UpdatedAt = DateTime.UtcNow;
                        }

                        actualizados++;
                    }
                }
                else if (huerfanas.Remove(codigo, out var huerfana))
                {
                    // La ficha ya estaba (capturada a mano) y el ERP trae al mismo cliente. Se
                    // reescribe con los datos del ERP y pasa a ser propiedad de la conexión. Se
                    // escribe siempre sin mirar el hash, por la misma razón que en productos:
                    // adoptar ya es un cambio, y omitir por hash dejaría la fila sin reclamar
                    // para siempre.
                    if (configuracion.EsSimulacion)
                    {
                        var cambiosAdopcion = new List<CampoSimulado>
                        {
                            new("Procedencia", "Ninguna (capturada a mano)",
                                $"La conexión «{configuracion.ErpConexionId}»")
                        };

                        if (huerfana.Nombre != nombre)
                            cambiosAdopcion.Add(new CampoSimulado("Nombre", huerfana.Nombre, nombre));
                        if (huerfana.TipoClienteId != tipoId)
                            cambiosAdopcion.Add(new CampoSimulado("Tipo de cliente", await NombreTipoAsync(tipoId, cancellationToken), await NombreTipoAsync(huerfana.TipoClienteId, cancellationToken)));
                        if (huerfana.Correo != correo)
                            cambiosAdopcion.Add(new CampoSimulado("Correo", huerfana.Correo, correo));

                        simulados.Add(new CambioSimulado(numeroFila, codigo, AccionSimulada.Adoptar, cambiosAdopcion));
                        hashesSimulados[codigo] = hash;
                    }
                    else
                    {
                        huerfana.CodigoExterno = codigo;
                        huerfana.ErpConexionId = configuracion.ErpConexionId;
                        huerfana.OrigenDatoId = origenDatoId;
                        huerfana.Nombre = nombre;
                        huerfana.RazonSocial = razonSocial;
                        huerfana.TipoClienteId = tipoId;
                        huerfana.CedulaORuc = cedulaRuc;
                        huerfana.IdentificacionFiscal = identFiscal;
                        huerfana.Direccion = direccion;
                        huerfana.Ciudad = ciudad;
                        huerfana.Departamento = departamento;
                        huerfana.Telefono = telefono;
                        huerfana.Correo = correo;
                        huerfana.ContactoNombre = contactoNombre;
                        huerfana.ContactoCargo = contactoCargo;
                        huerfana.SitioWeb = sitioWeb;
                        huerfana.Notas = notas;
                        huerfana.HashContenido = Convert.FromHexString(hash);
                        huerfana.UltimaSyncUtc = DateTime.UtcNow;
                        huerfana.UpdatedAt = DateTime.UtcNow;
                    }

                    existentes[codigo] = huerfana;
                    actualizados++;
                }
                else
                {
                    // La entidad se construye igual en los dos modos; solo cambia si se adjunta.
                    var nuevo = new Cliente
                    {
                        Id = Guid.NewGuid(),
                        Codigo = codigo,
                        Nombre = nombre,
                        RazonSocial = razonSocial,
                        TipoClienteId = tipoId,
                        CedulaORuc = cedulaRuc,
                        IdentificacionFiscal = identFiscal,
                        Direccion = direccion,
                        Ciudad = ciudad,
                        Departamento = departamento,
                        Telefono = telefono,
                        Correo = correo,
                        ContactoNombre = contactoNombre,
                        ContactoCargo = contactoCargo,
                        SitioWeb = sitioWeb,
                        Notas = notas,
                        OrigenDatoId = origenDatoId,
                        ErpConexionId = configuracion.ErpConexionId,
                        CodigoExterno = codigo,
                        HashContenido = Convert.FromHexString(hash),
                        UltimaSyncUtc = DateTime.UtcNow,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    if (configuracion.EsSimulacion)
                    {
                        simulados.Add(new CambioSimulado(numeroFila, codigo, AccionSimulada.Insertar, []));
                    }
                    else
                    {
                        _contexto.Cliente.Add(nuevo);
                    }

                    existentes[codigo] = nuevo;
                    insertados++;
                }
            }
            catch (Exception ex)
            {
                var mapeada = Normalizacion.AplicarMapeo(fila, configuracion.MapeoColumnas);
                var codigoExterno = Normalizacion.AString(mapeada.GetValueOrDefault("Codigo"));
                rechazados.Add(new RechazoCarga(
                    numeroFila,
                    codigoExterno,
                    ex.Message,
                    null));
            }

            numeroFila++;
        }

        // La red de la simulación: un cargador que escriba olvidándose de la bandera tiene que
        // fallar acá, ruidosamente, y no cargar medio catálogo sin que nadie lo pidiera.
        if (configuracion.EsSimulacion)
        {
            var pendientes = _contexto.ChangeTracker.Entries().Count(e => e.State != EntityState.Unchanged && e.State != EntityState.Detached);

            if (pendientes > 0)
            {
                throw new InvalidOperationException(
                    $"El cargador de {Catalogo} dejó {pendientes} cambios sin guardar en una simulación. " +
                    "Un cargador en modo simulación no debe modificar entidades: debe reportar el cambio, no aplicarlo.");
            }
        }
        else
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }

        return new ResultadoCarga
        {
            Catalogo = Catalogo,
            TipoOrigen = configuracion.TipoOrigen,
            Insertados = insertados,
            Actualizados = actualizados,
            Omitidos = omitidos,
            Rechazados = rechazados.Count,
            Rechazos = rechazados,
            Cambios = simulados
        };
    }

    /// <summary>
    /// Nombre de un tipo de cliente, para que el diff diga "cambió de Empresarial a Residencial"
    /// y no "cambió de 1 a 2". Se cachea por la misma razón que las categorías en productos.
    /// </summary>
    private async Task<string?> NombreTipoAsync(byte id, CancellationToken ca)
    {
        if (_nombresTipo.TryGetValue(id, out var ya))
        {
            return ya;
        }

        var nombre = await _contexto.TipoClienteCatalogo
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => t.Nombre)
            .FirstOrDefaultAsync(ca);

        var resultado = nombre ?? "(no encontrado)";
        _nombresTipo[id] = resultado;

        return resultado;
    }
}
