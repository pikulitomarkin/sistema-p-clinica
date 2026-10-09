namespace ClinicaPsi.Shared;

/// <summary>
/// Relógio de Brasília. Os horários de consulta são gravados como
/// timestamp sem fuso, no horário escolhido pelo usuário no Brasil.
/// O container da aplicação roda em UTC, então DateTime.Now desloca a janela.
/// </summary>
public static class HorarioBrasil
{
    private static readonly TimeZoneInfo Zona = ResolverZona();

    public static DateTime Agora
    {
        get
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zona);
            return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        }
    }

    public static DateTime Hoje => Agora.Date;

    private static TimeZoneInfo ResolverZona()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    }
}
