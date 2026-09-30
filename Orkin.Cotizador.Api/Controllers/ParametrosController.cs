using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Parámetros generales (ADMIN.PARAMETROS): los umbrales y textos que la dirección ajusta sin
/// redeploy. Solo se cambia el <c>Valor</c>; el nombre es la clave y el tipo es el contrato de
/// cómo interpretarlo.
/// </summary>
[ApiController]
[Route("api/parametros")]
[Produces("application/json")]
[Authorize("ADMIN.PARAMETROS")]
public sealed class ParametrosController : ControllerBase
{
    private readonly CotizadorDbContext _contexto;

    public ParametrosController(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    [HttpGet]
    public async Task<IResult> Listar(CancellationToken cancellationToken)
    {
        var parametros = await _contexto.ParametroGeneral
            .AsNoTracking()
            .OrderBy(p => p.Grupo).ThenBy(p => p.Orden)
            .Select(p => new ParametroResponse(
                p.Id,
                p.Nombre,
                p.Descripcion,
                p.Valor,
                p.TipoDato!.Nombre ?? string.Empty,
                p.Grupo,
                p.Orden,
                p.EsSistema,
                p.EsSecreto))
            .ToListAsync(cancellationToken);

        return ApiResponse<IReadOnlyList<ParametroResponse>>.Ok(parametros).AHttp();
    }

    [HttpPut("{id:guid}")]
    public async Task<IResult> Actualizar(Guid id, [FromBody] ActualizarParametroRequest request, CancellationToken cancellationToken)
    {
        var parametro = await _contexto.ParametroGeneral.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (parametro is null)
        {
            return ApiResponse<ParametroResponse>.Error("NO_ENCONTRADO", "El parámetro no existe.").AHttp();
        }

        // La validación del valor la hace el tipo declarado, no la UI: un margen mínimo de "abc"
        // o de "-5" no se puede guardar por la puerta de atrás de un PUT.
        var valor = request.Valor?.Trim();
        var tipo = await _contexto.TipoDatoParametroCatalogo
            .AsNoTracking()
            .Where(t => t.Id == parametro.TipoDatoId)
            .Select(t => t.Codigo)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.Equals(tipo, "DECIMAL", StringComparison.OrdinalIgnoreCase))
        {
            if (!decimal.TryParse(valor, out var numero))
            {
                return ApiResponse<ParametroResponse>.Error("VALOR_INVALIDO", $"El valor '{valor}' no es un número.").AHttp();
            }
        }
        else if (string.Equals(tipo, "ENTERO", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(valor, out _))
            {
                return ApiResponse<ParametroResponse>.Error("VALOR_INVALIDO", $"El valor '{valor}' no es un entero.").AHttp();
            }
        }
        else if (string.Equals(tipo, "BOOLEANO", StringComparison.OrdinalIgnoreCase))
        {
            if (valor is not ("SI" or "NO" or "1" or "0" or "TRUE" or "FALSE"))
            {
                return ApiResponse<ParametroResponse>.Error("VALOR_INVALIDO", "El valor debe ser SI o NO.").AHttp();
            }
        }

        parametro.Valor = valor;

        // La descripción también es editable: quien administra el sistema necesita poder
        // explicar qué hace cada parámetro para que el que venga después no tenga que
        // adivinar. Es texto de gestión, no del PDF.
        if (request.Descripcion is not null)
        {
            parametro.Descripcion = request.Descripcion.Trim();
        }

        parametro.UpdatedAt = DateTime.UtcNow;
        await _contexto.SaveChangesAsync(cancellationToken);

        return ApiResponse<ParametroResponse>.Ok(new ParametroResponse(
            parametro.Id, parametro.Nombre, parametro.Descripcion, parametro.Valor,
            tipo ?? string.Empty, parametro.Grupo, parametro.Orden, parametro.EsSistema, parametro.EsSecreto)).AHttp();
    }
}
