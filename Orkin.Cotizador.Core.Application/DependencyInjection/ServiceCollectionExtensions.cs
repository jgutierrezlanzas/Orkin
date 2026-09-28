using Microsoft.Extensions.DependencyInjection;
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

        return services;
    }
}
