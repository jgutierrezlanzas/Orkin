using System.Net;
using System.Net.Sockets;
using System.Text;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Orkin.Cotizador.Core.Application.Notificaciones;

namespace Orkin.Cotizador.Tests.Notificaciones;

/// <summary>
/// Verificación del envío real sin Internet ni datos reales: se levanta un servidor SMTP mínimo
/// en el loopback y se comprueba la conversación y el sobre que produce <see cref="SmtpClient"/>.
/// <para>No es un mock del proveedor: se prueba el protocolo de verdad. Un proveedor que dijera
/// «enviado» sin abrir un socket, o que pusiera el cuerpo en el asunto, no pasaría estas pruebas.
/// No se usa ninguna dirección ni servidor del ERP: todo es <c>ejemplo.local</c> en 127.0.0.1.</para>
/// </summary>
public sealed class ProveedorCorreoSmtpTests
{
    [Fact]
    public async Task Envia_el_sobre_y_el_cuerpo_por_smtp_real()
    {
        await using var servidor = new ServidorSmtpLocal();
        await servidor.IniciarAsync();

        var proveedor = CrearProveedor(new CorreoOpciones
        {
            Habilitado = true,
            Servidor = "127.0.0.1",
            Puerto = servidor.Puerto,
            UsarSsl = false,
            De = "notificaciones@ejemplo.local",
            NombreDe = "ORKIN",
            TimeoutSegundos = 5
        });

        var resultado = await proveedor.EnviarAsync(new CorreoSaliente(
            "destinatario@ejemplo.local",
            "Programacion de visita 0042978",
            "<div>Visita confirmada</div>"));

        Assert.True(resultado.Exito, resultado.Error);
        Assert.StartsWith("SMTP-", resultado.IdExterno, StringComparison.Ordinal);

        var conversacion = await servidor.TranscriptoAsync();
        Assert.Contains("EHLO", conversacion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MAIL FROM:<notificaciones@ejemplo.local>", conversacion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RCPT TO:<destinatario@ejemplo.local>", conversacion, StringComparison.OrdinalIgnoreCase);
        // SmtpClient puede codificar el sobre en base64 (o en una palabra codificada RFC 2047)
        // según el juego de caracteres; lo que importa es que el contenido viaje, no el código de
        // transferencia que eligió.
        Assert.True(ContieneTextoOBase64(conversacion, "Programacion de visita 0042978"));
        Assert.True(ContieneTextoOBase64(conversacion, "<div>Visita confirmada</div>"));
    }

    [Fact]
    public async Task Deshabilitado_devuelve_fallo_explicito_y_no_abre_socket()
    {
        await using var servidor = new ServidorSmtpLocal();
        await servidor.IniciarAsync();

        var proveedor = CrearProveedor(new CorreoOpciones { Habilitado = false });

        var resultado = await proveedor.EnviarAsync(new CorreoSaliente(
            "destinatario@ejemplo.local", "Asunto", "<p>Cuerpo</p>"));

        Assert.False(resultado.Exito);
        Assert.Contains("deshabilitado", resultado.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(servidor.HuboConexion);
    }

    [Fact]
    public async Task Un_destinatario_invalido_falla_antes_de_conectar()
    {
        await using var servidor = new ServidorSmtpLocal();
        await servidor.IniciarAsync();

        var proveedor = CrearProveedor(new CorreoOpciones
        {
            Habilitado = true,
            Servidor = "127.0.0.1",
            Puerto = servidor.Puerto,
            UsarSsl = false,
            De = "notificaciones@ejemplo.local"
        });

        var resultado = await proveedor.EnviarAsync(new CorreoSaliente(
            "no-es-un-correo", "Asunto", "<p>Cuerpo</p>"));

        Assert.False(resultado.Exito);
        Assert.Contains("no es una dirección válida", resultado.Error, StringComparison.Ordinal);
        Assert.False(servidor.HuboConexion);
    }

    private static bool ContieneTextoOBase64(string transcripto, string texto) =>
        transcripto.Contains(texto, StringComparison.Ordinal) ||
        transcripto.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes(texto)), StringComparison.Ordinal);

    private static ProveedorCorreoSmtp CrearProveedor(CorreoOpciones opciones) =>
        new(Options.Create(opciones), NullLogger<ProveedorCorreoSmtp>.Instance);

    /// <summary>Servidor SMTP de juguete: lo mínimo para aceptar MAIL/RCPT/DATA/QUIT.</summary>
    private sealed class ServidorSmtpLocal : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly StringBuilder _transcripto = new();
        private readonly SemaphoreSlim _cerrojo = new(1, 1);
        private readonly CancellationTokenSource _detencion = new();
        private StreamWriter? _escritor;
        private Task? _atencion;

        public int Puerto => ((IPEndPoint)_listener.LocalEndpoint).Port;
        public bool HuboConexion { get; private set; }

        public Task IniciarAsync()
        {
            _listener.Start();
            _atencion = AtenderAsync(_detencion.Token);
            return Task.CompletedTask;
        }

        public async Task<string> TranscriptoAsync()
        {
            if (_atencion is not null)
            {
                await _atencion.WaitAsync(TimeSpan.FromSeconds(5));
            }

            await _cerrojo.WaitAsync();
            try
            {
                return _transcripto.ToString();
            }
            finally
            {
                _cerrojo.Release();
            }
        }

        private async Task AtenderAsync(CancellationToken detencion)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(detencion);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));

            try
            {
                using var cliente = await _listener.AcceptTcpClientAsync(timeout.Token);
                HuboConexion = true;

                using var flujo = cliente.GetStream();
                using var lector = new StreamReader(flujo, Encoding.UTF8, false, 1024, leaveOpen: true);
                using var escritor = new StreamWriter(flujo, new UTF8Encoding(false), 1024, leaveOpen: true)
                {
                    NewLine = "\r\n",
                    AutoFlush = true
                };
                _escritor = escritor;

                await ResponderAsync("220 localhost SMTP de prueba\r\n");

                string? linea;
                while ((linea = await lector.ReadLineAsync(timeout.Token)) is not null)
                {
                    await RegistrarAsync(linea);

                    if (linea.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase))
                    {
                        await ResponderAsync("250-localhost\r\n250 SIZE 10485760\r\n");
                    }
                    else if (linea.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
                    {
                        await ResponderAsync("250 localhost\r\n");
                    }
                    else if (linea.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase) ||
                             linea.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase))
                    {
                        await ResponderAsync("250 OK\r\n");
                    }
                    else if (linea.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                    {
                        await ResponderAsync("354 Termine con CRLF punto CRLF\r\n");

                        while ((linea = await lector.ReadLineAsync(timeout.Token)) is not null)
                        {
                            await RegistrarAsync(linea);
                            if (linea == ".")
                            {
                                break;
                            }
                        }

                        await ResponderAsync("250 Mensaje aceptado\r\n");
                    }
                    else if (linea.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                    {
                        await ResponderAsync("221 Adios\r\n");
                        break;
                    }
                    else
                    {
                        await ResponderAsync("250 OK\r\n");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Ningún cliente conectó: las pruebas que esperan fallo local así lo comprueban.
            }
            catch (SocketException) when (detencion.IsCancellationRequested)
            {
                // TcpListener.Stop interrumpe Accept con SocketException en Windows.
            }
            finally
            {
                _escritor = null;
            }
        }

        private async Task ResponderAsync(string texto)
        {
            var escritor = _escritor;
            if (escritor is not null)
            {
                await escritor.WriteAsync(texto);
            }
        }

        private async Task RegistrarAsync(string linea)
        {
            await _cerrojo.WaitAsync();
            try
            {
                _transcripto.AppendLine(linea);
            }
            finally
            {
                _cerrojo.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            _detencion.Cancel();
            _listener.Stop();

            if (_atencion is not null)
            {
                try
                {
                    await _atencion.WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch (TimeoutException)
                {
                    // El cliente ya se fue; no hay nada que esperar.
                }
            }

            _cerrojo.Dispose();
            _detencion.Dispose();
        }
    }
}
