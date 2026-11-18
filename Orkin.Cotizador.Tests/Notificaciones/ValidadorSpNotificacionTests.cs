using Orkin.Cotizador.Core.Application.Notificaciones;

namespace Orkin.Cotizador.Tests.Notificaciones;

/// <summary>
/// Guardián de los SPs legacy. La base real puede haber cambiado desde el script versionado:
/// invocar un SP que ahora borra el staging sería mutar datos del ERP creyendo que solo leíamos.
/// </summary>
public sealed class ValidadorSpNotificacionTests
{
    [Fact]
    public void Un_select_simple_es_solo_lectura()
    {
        Assert.True(ValidadorSpNotificacion.EsSoloLectura(
            "CREATE PROCEDURE dbo.SINCO_NOTIFICAR_CORREOS_ENVIADOS AS BEGIN " +
            "SELECT Cliente, CORREO FROM dbo.NotificarProgramacionVisitaTemporal END"));
    }

    [Fact]
    public void Los_comentarios_y_literales_no_disfrazan_una_escritura()
    {
        var conTextoEngañoso = """
            CREATE PROCEDURE dbo.SP AS
            BEGIN
              -- no hacer UPDATE aquí
              SELECT 'DELETE' AS Estado, CASE WHEN @x='INSERT' THEN 1 ELSE 0 END AS Valor
              FROM dbo.NotificarProgramacionVisitaTemporal
            END
            """;

        Assert.True(ValidadorSpNotificacion.EsSoloLectura(conTextoEngañoso));
    }

    [Theory]
    [InlineData("DELETE FROM dbo.NotificarProgramacionVisitaTemporal")]
    [InlineData("TRUNCATE TABLE dbo.NotificarProgramacionVisitaTemporal")]
    [InlineData("UPDATE vvLocacionesClientes SET CORREO1 = 'x'")]
    [InlineData("INSERT INTO dbo.logs VALUES (1)")]
    [InlineData("MERGE dbo.NotificarProgramacionVisitaTemporal USING x ON 1=1 WHEN MATCHED THEN DELETE")]
    [InlineData("EXEC sp_executesql @sql")]
    [InlineData("EXECUTE xp_cmdshell 'dir'")]
    [InlineData("OPENROWSET('SQLNCLI', 'x')")]
    [InlineData("GRANT EXECUTE ON dbo.SP TO alguien")]
    public void Cualquier_escritura_o_ejecucion_dinamica_se_rechaza(string definicion)
    {
        Assert.False(ValidadorSpNotificacion.EsSoloLectura(definicion));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Una_definicion_vacia_no_es_solo_lectura(string? definicion)
    {
        Assert.False(ValidadorSpNotificacion.EsSoloLectura(definicion));
    }

    [Fact]
    public void Los_nombres_conocidos_incluyen_los_cuatro_sps_del_contrato()
    {
        Assert.Equal(4, ValidadorSpNotificacion.NombresConocidos.Length);
        Assert.Contains("dbo.SINCO_NOTIFICAR_CLIENTES", ValidadorSpNotificacion.NombresConocidos);
        Assert.Contains("dbo.SINCO_NOTIFICAR_CORREOS_ENVIADOS", ValidadorSpNotificacion.NombresConocidos);
        Assert.Contains("dbo.SINCO_NOTIFICAR_PROXIMOS_SERVICIOS", ValidadorSpNotificacion.NombresConocidos);
        Assert.Contains("dbo.SINCO_NOTIFICAR_REQUISAS_ERRADAS", ValidadorSpNotificacion.NombresConocidos);
    }
}
