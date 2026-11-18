using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orkin.Cotizador.Core.Application.Catalogos;
using Orkin.Cotizador.Core.Application.Cotizaciones;
using Orkin.Cotizador.Core.Application.Notificaciones;
using Orkin.Cotizador.Core.Application.Sistema;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.DependencyInjection;

/// <summary>
/// Registra lo que la capa de aplicación ofrece.
/// <para><b>Por qué una capa que por ahora solo registra dos servicios:</b> los servicios de esta
/// capa no se pueden enumerar antes de que existan los casos de uso que los necesitan. Registrarlos
/// cuando exista el caso de uso, y no antes, evita tener aquí veinte líneas que nadie usa y nadie
/// sabe si se pueden borrar.</para>
/// <para><b>Por qué se registra aquí y no en la API:</b> el orden importa para las pruebas. Un
/// proyecto de pruebas llama a <c>AddApplicationServices()</c> y no tiene que saber qué hay detrás.
/// Y si mañana la API arranca con un <c>IHostedService</c> en vez de con Kestrel, quien arma la
/// composición no cambia.</para>
/// <para><b>Por qué <see cref="EjecutorSql"/> es scoped y no singleton:</b> depende del
/// <c>CotizadorDbContext</c>, que también es scoped. Un singleton que dependiera de él se
/// comprobaría al registrar los servicios, y no la primera vez que se usara, que es la peor hora
/// posible para descubrir que un <c>DbContext</c> no se puede compartir entre hilos.</para>
/// <para><b>Por qué no recibe <c>IConfiguration</c>:</b> las opciones de esta capa
/// (<see cref="CorreoOpciones"/> y <see cref="NotificacionesOpciones"/>) se enlazan y validan en el
/// composition root de la API, igual que las de seguridad. Esta capa solo las consume con
/// <c>IOptions&lt;T&gt;</c>, de modo que una prueba puede pasar las suyas sin levantar configuración.</para>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra los servicios de la capa de aplicación. Se llama desde <c>Program.cs</c> después de
    /// <c>AddInfrastructure</c>, porque los servicios de aplicación usan tipos de infraestructura.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // El correlativo comparte el contexto con el caso de uso que guarda la cotización, para que
        // la transacción sea una sola cuando el que llama abrió una.
        services.AddScoped<ConsecutivoService>();
        services.AddScoped<EjecutorSql>();
        services.AddScoped<ILectorSql>(proveedor => proveedor.GetRequiredService<EjecutorSql>());
        services.AddScoped<TipoCambioService>();
        services.AddScoped<CatalogoErpService>();
        services.AddScoped<CotizacionService>();
        services.AddScoped<Plantillas.PlantillaService>();
        services.AddScoped<Ordenes.OrdenTrabajoService>();
        services.AddScoped<Requisas.RequisaService>();
        services.AddScoped<Inventario.IExactusService, Inventario.ExactusService>();
        services.AddScoped<Inventario.InventarioService>();
        services.AddScoped<Reportes.IReporteService, Reportes.ServicioReportes>();
        services.AddScoped<Visitas.IGeneradorOrdenTrabajo, Visitas.GeneradorOrdenTrabajoService>();
        services.AddScoped<Visitas.VisitaService>();
        services.AddScoped<Visitas.ImportadorGoogleService>();

        // ── Fase 8: notificaciones y cola de correo ──────────────────────────────────
        // AddOptions sin parámetros da el IOptions<T> vacío que necesitan los Servicios cuando
        // quien arma el contenedor no enlazó la sección (pruebas, hosts que no son la API). La API
        // llama después a AddOptions<...>().Bind(...).ValidateOnStart(), y el último registro gana.
        services.AddOptions();
        services.AddScoped<ValidadorSpNotificacion>();
        services.AddScoped<IProveedorCorreo, ProveedorCorreoSmtp>();
        services.AddScoped<INotificacionService, NotificacionService>();
        services.AddHostedService<NotificacionBackgroundService>();

        // D-24: el único catálogo que se sigue cargando es el tipo de cambio. Los demás se leen
        // directamente de las vistas vv* del ERP: no hay tablas espejo ni motor ETL local.
        services.AddScoped<Domain.Carga.ICargadorCatalogo, Carga.CargadorTiposCambio>();

        // D-20: no se registran cargadores para catálogos que ya viven en SOFTLAND/FUMIPLAGA
        // o en las vistas vv* de SINCOERPORKIN (frecuencias, locaciones, métodos, plagas,
        // ubicaciones, vehículos). Se consumen desde su origen cuando un módulo los necesite.

        // El reloj, por inyección, para que una prueba pueda fijar el tiempo cuando lo necesite.
        // TryAdd porque es un servicio del host y el host puede haber puesto el suyo.
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
