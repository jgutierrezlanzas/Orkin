using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orkin.Cotizador.Core.Application.Catalogos;
using Orkin.Cotizador.Core.Application.Cotizaciones;
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
/// <para><b>Por qué no recibe <c>IConfiguration</c>:</b> esta capa todavía no tiene opciones propias.
/// Agregar el parámetro "por si acaso" es agregar un parámetro que nadie pasa valor y que hay que
/// mantener. Cuando exista el primer parámetro de esta capa —el umbral de aprobación, el tamaño de
/// página— se agrega y se enlaza aquí.</para>
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
        services.AddScoped<UnidadMedidaService>();
        services.AddScoped<CategoriaProductoService>();
        services.AddScoped<TipoServicioService>();
        services.AddScoped<ProductoService>();
        services.AddScoped<ClienteService>();
        services.AddScoped<TipoCambioService>();
        services.AddScoped<CotizacionService>();

        // Proceso de carga desde origen externo (Fase 2.5): un cargador por catálogo. El de
        // clientes va con el de productos porque el cliente de ORKIN también vive en su ERP, y
        // cotizarle a alguien que ya es cliente exige su ficha, no solo el nombre. Los maestros
        // (unidades, categorías, tipos de servicio) y el tipo de cambio completan la fila: todo
        // catálogo que el cliente tenga en su ERP se carga desde ahí.
        services.AddScoped<Domain.Carga.ICargadorCatalogo, Carga.CargadorProductos>();
        services.AddScoped<Domain.Carga.ICargadorCatalogo, Carga.CargadorClientes>();
        services.AddScoped<Domain.Carga.ICargadorCatalogo, Carga.CargadorUnidadesMedida>();
        services.AddScoped<Domain.Carga.ICargadorCatalogo, Carga.CargadorCategoriasProducto>();
        services.AddScoped<Domain.Carga.ICargadorCatalogo, Carga.CargadorTiposServicio>();
        services.AddScoped<Domain.Carga.ICargadorCatalogo, Carga.CargadorTiposCambio>();
        services.AddScoped<Domain.Carga.ICargadorCatalogo, Carga.CargadorActivosAmortizables>();
        services.AddScoped<Domain.Carga.ICargadorCatalogo, Carga.CargadorTecnicos>();

        // Integración con el ERP del cliente (Fase 12): conexiones, flujos y sus corridas.
        services.AddScoped<Integracion.IntegracionService>();

        // El reloj, por inyección, para que una prueba pueda mover el tiempo en vez de esperar.
        // Es la diferencia entre probar una regla de programación en un milisegundo y no poder
        // probarla. TryAdd porque es un servicio del host y el host puede haber puesto el suyo.
        services.TryAddSingleton(TimeProvider.System);

        // El motor de sincronización programada. Se registra aunque no haya ningún flujo
        // programado: gira cada 5 segundos, pregunta a la base qué venció, y la respuesta normal
        // es «nada». Encender un flujo después no pide reiniciar nada, que es lo que un cliente
        // espera de una opción que dice «automático».
        //
        // Vive en esta capa y no en infraestructura porque necesita a IntegracionService, que es
        // de aplicación, y las capas solo se ven en un sentido. El ReclamadorFlujos sí es
        // infraestructura —habla SQL crudo— y por eso lo registra AddInfrastructure.
        services.AddHostedService<Integracion.SincronizacionBackgroundService>();

        return services;
    }
}
