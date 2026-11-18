using Orkin.Cotizador.Core.Application.Notificaciones;

namespace Orkin.Cotizador.Tests.Notificaciones;

/// <summary>
/// Validación de arranque. Un host mal escrito no puede descubrirse cuando el primer correo
/// lleva tres días en la cola, y el job con intervalo 0 no puede convertirse en un bucle caliente.
/// </summary>
public sealed class OpcionesNotificacionesTests
{
    private readonly CorreoOpcionesValidador _validadorCorreo = new();
    private readonly NotificacionesOpcionesValidador _validadorJob = new();

    [Fact]
    public void La_configuracion_por_defecto_es_valida_y_no_envia_correo()
    {
        Assert.True(_validadorCorreo.Validate(null, new CorreoOpciones()).Succeeded);
        Assert.True(_validadorJob.Validate(null, new NotificacionesOpciones()).Succeeded);

        Assert.False(new CorreoOpciones().Habilitado);
        Assert.False(new NotificacionesOpciones().HabilitadoJob);
        Assert.True(new NotificacionesOpciones().ValidarDefinicionesSp);
    }

    [Fact]
    public void Un_servidor_sin_autenticacion_es_valido()
    {
        var opciones = new CorreoOpciones
        {
            Habilitado = true,
            Servidor = "smtp.interno.local",
            De = "notificaciones@ejemplo.local"
        };

        Assert.True(_validadorCorreo.Validate(null, opciones).Succeeded);
    }

    [Fact]
    public void Habilitar_el_envio_sin_servidor_o_remitente_falla_al_arrancar()
    {
        var sinServidor = new CorreoOpciones { Habilitado = true, De = "a@b.com" };
        var sinRemitente = new CorreoOpciones { Habilitado = true, Servidor = "smtp.local" };
        var remitenteInvalido = new CorreoOpciones
        {
            Habilitado = true, Servidor = "smtp.local", De = "no es correo"
        };

        Assert.False(_validadorCorreo.Validate(null, sinServidor).Succeeded);
        Assert.False(_validadorCorreo.Validate(null, sinRemitente).Succeeded);
        Assert.False(_validadorCorreo.Validate(null, remitenteInvalido).Succeeded);
    }

    [Fact]
    public void Usuario_y_clave_van_juntos_o_ni_uno_de_los_dos()
    {
        var soloUsuario = new CorreoOpciones { Usuario = "usuario" };
        var soloClave = new CorreoOpciones { Clave = "secreto" };
        var ambos = new CorreoOpciones { Usuario = "usuario", Clave = "secreto" };

        Assert.False(_validadorCorreo.Validate(null, soloUsuario).Succeeded);
        Assert.False(_validadorCorreo.Validate(null, soloClave).Succeeded);
        Assert.True(_validadorCorreo.Validate(null, ambos).Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(70000)]
    public void El_puerto_fuera_de_rango_falla(int puerto)
    {
        var resultado = _validadorCorreo.Validate(null, new CorreoOpciones { Puerto = puerto });

        Assert.False(resultado.Succeeded);
        Assert.Contains("Puerto", resultado.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(nameof(NotificacionesOpciones.IntervaloSegundos), 4)]
    [InlineData(nameof(NotificacionesOpciones.TamanoLote), 0)]
    [InlineData(nameof(NotificacionesOpciones.MaximoIntentos), 11)]
    [InlineData(nameof(NotificacionesOpciones.BackoffBaseSegundos), 0)]
    [InlineData(nameof(NotificacionesOpciones.TamanoBandeja), 201)]
    public void Los_parametros_del_job_tienen_un_rango(string propiedad, int valor)
    {
        var opciones = new NotificacionesOpciones();
        typeof(NotificacionesOpciones).GetProperty(propiedad)!.SetValue(opciones, valor);

        var resultado = _validadorJob.Validate(null, opciones);

        Assert.False(resultado.Succeeded);
        Assert.Contains(propiedad, resultado.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_correo_interno_invalido_se_rechaza()
    {
        var resultado = _validadorJob.Validate(null,
            new NotificacionesOpciones { CorreoInterno = "no-es-correo" });

        Assert.False(resultado.Succeeded);
    }
}
