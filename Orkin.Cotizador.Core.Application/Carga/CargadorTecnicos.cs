using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Core.Domain.Carga;
using Orkin.Cotizador.Core.Domain.Catalogos;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Core.Application.Carga;

/// <summary>
/// Carga el catálogo de técnicos desde el ERP del cliente. Es un maestro de personal: quien hace
/// el trabajo es personal del cliente, no de la plataforma. Hereda el núcleo común
/// (<see cref="CargadorCatalogoMaestro{T}"/>) porque se identifica por <c>Codigo</c> como
/// cualquier catálogo; lo que aporta son sus campos propios: teléfono, correo y cargo.
/// </summary>
public sealed class CargadorTecnicos : CargadorCatalogoMaestro<Tecnico>
{
    public CargadorTecnicos(CotizadorDbContext contexto) : base(contexto)
    {
    }

    public override string Catalogo => "TECNICOS";

    protected override DbSet<Tecnico> Set() => Contexto.Tecnico;

    protected override Tecnico CrearEntidad() => new();

    protected override Task AplicarExtrasAsync(
        Tecnico entidad, Dictionary<string, object?> mapeada, CancellationToken ct)
    {
        entidad.Telefono = Normalizacion.AString(mapeada.GetValueOrDefault("Telefono"));
        entidad.Correo = Normalizacion.AString(mapeada.GetValueOrDefault("Correo"));
        entidad.Cargo = Normalizacion.AString(mapeada.GetValueOrDefault("Cargo"));
        return Task.CompletedTask;
    }

    protected override Task<List<CampoSimulado>> DiferenciaExtrasAsync(
        Tecnico existente, Dictionary<string, object?> mapeada, CancellationToken ct)
    {
        var cambios = new List<CampoSimulado>();

        var telefono = Normalizacion.AString(mapeada.GetValueOrDefault("Telefono"));
        if (existente.Telefono != telefono)
        {
            cambios.Add(new CampoSimulado("Teléfono", existente.Telefono, telefono));
        }

        var correo = Normalizacion.AString(mapeada.GetValueOrDefault("Correo"));
        if (existente.Correo != correo)
        {
            cambios.Add(new CampoSimulado("Correo", existente.Correo, correo));
        }

        var cargo = Normalizacion.AString(mapeada.GetValueOrDefault("Cargo"));
        if (existente.Cargo != cargo)
        {
            cambios.Add(new CampoSimulado("Cargo", existente.Cargo, cargo));
        }

        return Task.FromResult(cambios);
    }
}
