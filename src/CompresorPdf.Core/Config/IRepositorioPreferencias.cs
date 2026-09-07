namespace CompresorPdf.Core.Config;

public interface IRepositorioPreferencias
{
    PreferenciasUsuario Cargar();
    void Guardar(PreferenciasUsuario preferencias);
}
