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
/// Administración de usuarios (ADMIN.USUARIOS).
/// <para>La contraseña se restablece, nunca se «edita»: quien la cambia es el administrador por
/// petición del usuario, y <c>ObligarCambio</c> deja el relevo firmado — el usuario entra con la
/// clave que le dieron y el sistema le exige la suya propia antes de dejarlo trabajar.</para>
/// <para>El usuario de sistema no se puede desactivar ni quitar roles: es la red que impide
/// quedarse sin administrador, y el resto del sistema confía en que exista.</para>
/// </summary>
[ApiController]
[Route("api/usuarios")]
[Produces("application/json")]
[Authorize("ADMIN.USUARIOS")]
public sealed class UsuariosController : ControllerBase
{
    private readonly UserManager<Usuario> _userManager;
    private readonly CotizadorDbContext _contexto;

    public UsuariosController(UserManager<Usuario> userManager, CotizadorDbContext contexto)
    {
        _userManager = userManager;
        _contexto = contexto;
    }

    [HttpGet]
    public async Task<IResult> Listar(CancellationToken cancellationToken)
    {
        var rolesPorUsuario = await _contexto.UsuarioRol
            .AsNoTracking()
            .Join(_contexto.Rol.IgnoreQueryFilters(),
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync(cancellationToken);

        var usuarios = await _userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.NombreCompleto)
            .ToListAsync(cancellationToken);

        var respuesta = usuarios.Select(u => new UsuarioResponse(
            u.Id,
            u.UserName ?? string.Empty,
            u.NombreCompleto,
            u.Email,
            u.Documento,
            u.Cargo,
            u.EsActivo,
            u.EsUsuarioSistema,
            u.DebeCambiarContrasena,
            u.UltimoAccesoUtc,
            rolesPorUsuario.Where(r => r.UserId == u.Id).Select(r => r.Name ?? string.Empty).ToList()))
            .ToList();

        return ApiResponse<IReadOnlyList<UsuarioResponse>>.Ok(respuesta).AHttp();
    }

    [HttpPost]
    public async Task<IResult> Crear([FromBody] CrearUsuarioRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NombreCuenta) ||
            string.IsNullOrWhiteSpace(request.NombreCompleto) ||
            string.IsNullOrWhiteSpace(request.Contrasena))
        {
            return ApiResponse<UsuarioResponse>.Error("VALIDACION", "Cuenta, nombre y contraseña son obligatorios.").AHttp();
        }

        var nombreCuenta = request.NombreCuenta.Trim();

        if (await _userManager.FindByNameAsync(nombreCuenta) is not null)
        {
            return ApiResponse<UsuarioResponse>.Error("YA_EXISTE", $"Ya existe un usuario con la cuenta '{nombreCuenta}'.").AHttp();
        }

        var rolesExistentes = await ValidarRolesAsync(request.Roles, cancellationToken);
        if (rolesExistentes is null)
        {
            return ApiResponse<UsuarioResponse>.Error("ROL_INVALIDO", "Uno o más roles no existen.").AHttp();
        }

        var usuario = new Usuario
        {
            UserName = nombreCuenta,
            NombreCompleto = request.NombreCompleto.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Correo) ? null : request.Correo.Trim(),
            Documento = request.Documento,
            Cargo = request.Cargo,
            EsActivo = request.EsActivo,
            DebeCambiarContrasena = true // toda clave nueva la elige quien la recibe, no quien la reparte
        };

        var creado = await _userManager.CreateAsync(usuario, request.Contrasena);
        if (!creado.Succeeded)
        {
            // Los errores de política de clave de Identity hablan en inglés y del campo interno;
            // el mensaje legible se arma con la primera razón real.
            var motivo = creado.Errors.FirstOrDefault()?.Description ?? "No se pudo crear el usuario.";
            return ApiResponse<UsuarioResponse>.Error("CLAVE_INVALIDA", $"Revise la contraseña: {motivo}").AHttp();
        }

        foreach (var rol in rolesExistentes)
        {
            await _userManager.AddToRoleAsync(usuario, rol);
        }

        return ApiResponse<UsuarioResponse>.Ok(AResponse(usuario, rolesExistentes)).AHttp();
    }

    [HttpPut("{id:guid}")]
    public async Task<IResult> Actualizar(Guid id, [FromBody] ActualizarUsuarioRequest request, CancellationToken cancellationToken)
    {
        var usuario = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (usuario is null)
        {
            return ApiResponse<UsuarioResponse>.Error("NO_ENCONTRADO", "El usuario no existe.").AHttp();
        }

        var rolesExistentes = await ValidarRolesAsync(request.Roles, cancellationToken);
        if (rolesExistentes is null)
        {
            return ApiResponse<UsuarioResponse>.Error("ROL_INVALIDO", "Uno o más roles no existen.").AHttp();
        }

        usuario.NombreCompleto = request.NombreCompleto.Trim();
        usuario.Email = string.IsNullOrWhiteSpace(request.Correo) ? null : request.Correo.Trim();
        usuario.Documento = request.Documento;
        usuario.Cargo = request.Cargo;

        // El usuario de sistema no se desactiva: sin él, un sistema cuyos administradores
        // renunciaron todos no tiene quien cree al siguiente.
        if (!usuario.EsUsuarioSistema)
        {
            usuario.EsActivo = request.EsActivo;
        }

        await _userManager.UpdateAsync(usuario);

        // Los roles se reemplazan completos: quitar y poner, porque quien edita vio la lista
        // final y no las diferencias.
        var rolesActuales = await _userManager.GetRolesAsync(usuario);
        foreach (var actual in rolesActuales)
        {
            if (!rolesExistentes.Contains(actual, StringComparer.Ordinal))
            {
                await _userManager.RemoveFromRoleAsync(usuario, actual);
            }
        }
        foreach (var nuevo in rolesExistentes)
        {
            if (!rolesActuales.Contains(nuevo, StringComparer.Ordinal))
            {
                await _userManager.AddToRoleAsync(usuario, nuevo);
            }
        }

        return ApiResponse<UsuarioResponse>.Ok(AResponse(usuario, rolesExistentes)).AHttp();
    }

    [HttpPut("{id:guid}/contrasena")]
    public async Task<IResult> CambiarContrasena(Guid id, [FromBody] CambiarContrasenaUsuarioRequest request)
    {
        var usuario = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (usuario is null)
        {
            return ApiResponse<object>.Error("NO_ENCONTRADO", "El usuario no existe.").AHttp();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
        var cambio = await _userManager.ResetPasswordAsync(usuario, token, request.Contrasena);

        if (!cambio.Succeeded)
        {
            var motivo = cambio.Errors.FirstOrDefault()?.Description ?? "No se pudo cambiar la contraseña.";
            return ApiResponse<object>.Error("CLAVE_INVALIDA", $"Revise la contraseña: {motivo}").AHttp();
        }

        usuario.DebeCambiarContrasena = request.ObligarCambio;
        await _userManager.UpdateAsync(usuario);

        return ApiResponse<object>.Ok(new { exito = true }).AHttp();
    }

    // ── Ayudas ─────────────────────────────────────────────────────────────

    private async Task<List<string>?> ValidarRolesAsync(IReadOnlyList<string> nombres, CancellationToken ct)
    {
        if (nombres.Count == 0)
        {
            return [];
        }

        var buscados = nombres.Select(n => n.Trim()).Distinct(StringComparer.Ordinal).ToList();
        var existentes = await _contexto.Rol
            .AsNoTracking()
            .Where(r => buscados.Contains(r.Name!))
            .Select(r => r.Name!)
            .ToListAsync(ct);

        return existentes.Count == buscados.Count ? existentes : null;
    }

    private static UsuarioResponse AResponse(Usuario u, IReadOnlyList<string> roles) => new(
        u.Id,
        u.UserName ?? string.Empty,
        u.NombreCompleto,
        u.Email,
        u.Documento,
        u.Cargo,
        u.EsActivo,
        u.EsUsuarioSistema,
        u.DebeCambiarContrasena,
        u.UltimoAccesoUtc,
        roles);
}
