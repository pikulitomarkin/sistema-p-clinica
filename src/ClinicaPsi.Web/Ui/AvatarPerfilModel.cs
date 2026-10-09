namespace ClinicaPsi.Web.Ui;

public class AvatarPerfilModel
{
    public AvatarPerfilModel()
    {
    }

    public AvatarPerfilModel(string? fotoUrl, string? nome, int tamanho = 40)
    {
        FotoUrl = fotoUrl;
        Nome = nome ?? "";
        Tamanho = tamanho;
    }

    public string? FotoUrl { get; set; }
    public string Nome { get; set; } = "";
    public int Tamanho { get; set; } = 40;
}
