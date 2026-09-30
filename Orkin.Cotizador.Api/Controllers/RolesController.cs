using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orkin.Cotizador.Api.DTOs;
using Orkin.Cotizador.Api.Extensions;
using Orkin.Cotizador.Core.Domain.Comun;
using Orkin.Cotizador.Core.Domain.Entidades;
using Orkin.Cotizador.Core.Infrastructure.Persistence;

namespace Orkin.Cotizador.Api.Controllers;

/// <summary>
/// Administración de roles y permisos (ADMIN.ROLES).
/// <para>La lista de permisos del catálogo es de solo lectura — es el contrato de autorización
/// del sistema, no un dato que se administra —; lo que se edita es qué permisos tiene cada
/// rol. Un permiso puede quitarse de todos los roles sin romper nada: la política simplemente
/// deja de conceder a alguien.</para>
/// <para>Los roles de sistema no se borran: cambiar los permisos de un rol de sistema es un
/// cambio de despliegue (nota del seed), y la UI lo permite; borrarlo, no.</para>
/// </summary>
[ApiController]
[Route("api/roles")]
[Produces("application/json")]
[Authorize("ADMIN.ROLES")]
public sealed class RolesController : ControllerBase
{
    private readonly CotizadorDbContext _contexto;

    public RolesController(CotizadorDbContext contexto)
    {
        _contexto = contexto;
    }

    [HttpGet]
    public async Task<IResult> Listar(CancellationToken cancellationToken)
    {
        var roles = await _contexto.Rol
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var permisosPorRol = await _contexto.RolPermiso
            .AsNoTracking()
            .Join(_contexto.Permiso.AsNoTracking(),
                rp => rp.PermisoId,
                p => p.Id,
                (rp, p) => new { rp.RolId, p.Codigo })
            .ToListAsync(cancellationToken);

        var respuesta = roles.Select(r => new RolResponse(
            r.Id,
            r.Name ?? string.Empty,
            r.Description,
            r.EsSistema,
            r.EsActivo,
            permisosPorRol.Where(p => p.RolId == r.Id).Select(p => p.Codigo).ToList()))
            .ToList();

        return ApiResponse<IReadOnlyList<RolResponse>>.Ok(respuesta).AHttp();
    }

    /// <summary>El catálogo completo de permisos, para la pantalla de roles.</summary>
    [HttpGet("/api/permisos")]
    public async Task<IResult> Permisos(CancellationToken cancellationToken)
    {
        var permisos = await _contexto.Permiso
            .AsNoTracking()
            .OrderBy(p => p.ModuloId).ThenBy(p => p.Orden)
            .Select(p => new PermisoResponse(p.Codigo, p.Nombre, p.Modulo!.Nombre ?? p.ModuloId.ToString(), p.Descripcion))
            .ToListAsync(cancellationToken);

        return ApiResponse<IReadOnlyList<PermisoResponse>>.Ok(permisos).AHttp();
    }

    /// <summary>
    /// Fija los permisos de un rol. La lista reemplaza la anterior: quien guardó vio la pantalla
    /// completa con los casilleros, no un diff.
    /// </summary>
    [HttpPut("{id:guid}/permisos")]
    public async Task<IResult> AsignarPermisos(Guid id, [FromBody] AsignarPermisosRolRequest request, CancellationToken cancellationToken)
    {
        var rol = await _contexto.Rol.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rol is null)
        {
            return ApiResponse<RolResponse>.Error("NO_ENCONTRADO", "El rol no existe.").AHttp();
        }

        var codigos = request.Permisos
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var permisos = await _contexto.Permiso
            .AsNoTracking()
            .Where(p => codigos.Contains(p.Codigo))
            .ToListAsync(cancellationToken);

        if (permisos.Count != codigos.Count)
        {
            var encontrados = permisos.Select(p => p.Codigo).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var faltantes = codigos.Where(c => !encontrados.Contains(c)).ToList();
            return ApiResponse<RolResponse>.Error("PERMISO_INVALIDO", $"Estos permisos no existen: {string.Join(", ", faltantes)}.").AHttp();
        }

        var actuales = await _contexto.RolPermiso
            .Where(rp => rp.RolId == id)
            .ToListAsync(cancellationToken);

        _contexto.RolPermiso.RemoveRange(actuales);

        foreach (var permiso in permisos)
        {
            _contexto.RolPermiso.Add(new RolPermiso { RolId = id, PermisoId = permiso.Id });
        }

        rol.UpdatedAt = DateTime.UtcNow;
        await _contexto.SaveChangesAsync(cancellationToken);

        return ApiResponse<RolResponse>.Ok(new RolResponse(
            rol.Id, rol.Name ?? string.Empty, rol.Description, rol.EsSistema, rol.EsActivo,
            codigos.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList())).AHttp();
    }
}
