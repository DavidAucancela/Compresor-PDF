using System.Text.Json;
using System.Text.Json.Serialization;
using CompresorPdf.Core.Diagnostico;

namespace CompresorPdf.Core.Config;

/// <summary>Persistencia de preferencias en JSON (RNF-07). Tolerante a archivos corruptos.</summary>
public sealed class RepositorioPreferenciasJson : IRepositorioPreferencias
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _ruta;
    private readonly IRegistro _registro;

    public RepositorioPreferenciasJson(string? ruta = null, IRegistro? registro = null)
    {
        _ruta = ruta ?? RutasApp.ArchivoPreferencias;
        _registro = registro ?? RegistroNulo.Instancia;
    }

    public PreferenciasUsuario Cargar()
    {
        try
        {
            if (!File.Exists(_ruta))
                return PreferenciasUsuario.PorDefecto();

            var json = File.ReadAllText(_ruta);
            return JsonSerializer.Deserialize<PreferenciasUsuario>(json, Opciones)
                   ?? PreferenciasUsuario.PorDefecto();
        }
        catch (Exception ex)
        {
            _registro.Error($"No se pudieron leer las preferencias de {_ruta}, se usan las por defecto", ex);
            return PreferenciasUsuario.PorDefecto();
        }
    }

    public void Guardar(PreferenciasUsuario preferencias)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_ruta)!);
            File.WriteAllText(_ruta, JsonSerializer.Serialize(preferencias, Opciones));
        }
        catch (Exception ex)
        {
            _registro.Error($"No se pudieron guardar las preferencias en {_ruta}", ex);
        }
    }
}
