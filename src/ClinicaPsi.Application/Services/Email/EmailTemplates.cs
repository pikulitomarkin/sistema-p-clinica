using System.Net;
using System.Text;

namespace ClinicaPsi.Application.Services.Email;

public static class EmailTemplates
{
    private const string Brand = "Psicóloga Ana Santos";
    private const string Accent = "#2F6F5E";

    public static string ResetSenha(string nome, string resetUrl)
    {
        var safeNome = Html(nome);
        var body = $@"
<p>Olá, <strong>{safeNome}</strong>,</p>
<p>Recebemos uma solicitação para redefinir a senha da sua conta na clínica <strong>{Brand}</strong>.</p>
<p style=""margin:24px 0"">
  <a href=""{Attr(resetUrl)}"" style=""background:{Accent};color:#fff;padding:12px 22px;border-radius:6px;text-decoration:none;display:inline-block"">
    Redefinir senha
  </a>
</p>
<p>Se o botão não funcionar, copie e cole este link no navegador:</p>
<p style=""word-break:break-all;font-size:13px;color:#555"">{Html(resetUrl)}</p>
<p>Se você não solicitou esta alteração, ignore este e-mail. O link expira em algumas horas.</p>";
        return Wrap("Redefinição de senha", body);
    }

    public static string BoasVindasPsicologo(string nome, string emailLogin, string? senhaProvisoria, string loginUrl)
    {
        var safeNome = Html(nome);
        var credenciais = new StringBuilder();
        credenciais.Append($@"<p><strong>Login:</strong> {Html(emailLogin)}</p>");
        if (!string.IsNullOrWhiteSpace(senhaProvisoria))
        {
            credenciais.Append($@"<p><strong>Senha provisória:</strong> <code style=""background:#f4f4f4;padding:2px 6px;border-radius:4px"">{Html(senhaProvisoria)}</code></p>");
            credenciais.Append("<p>No primeiro acesso, altere esta senha por uma de sua preferência.</p>");
        }
        else
        {
            credenciais.Append("<p>Use a senha definida no cadastro para acessar o sistema.</p>");
        }

        var body = $@"
<p>Olá, <strong>{safeNome}</strong>,</p>
<p>Seu acesso como psicólogo(a) na clínica <strong>{Brand}</strong> foi criado.</p>
{credenciais}
<p style=""margin:24px 0"">
  <a href=""{Attr(loginUrl)}"" style=""background:{Accent};color:#fff;padding:12px 22px;border-radius:6px;text-decoration:none;display:inline-block"">
    Acessar o sistema
  </a>
</p>
<p style=""word-break:break-all;font-size:13px;color:#555"">{Html(loginUrl)}</p>";
        return Wrap("Bem-vindo(a) à equipe", body);
    }

    public static string BoasVindasPaciente(string nome, string emailLogin, string senhaProvisoria, string loginUrl, string? trocarSenhaUrl = null)
    {
        var safeNome = Html(nome);
        var linkTroca = string.IsNullOrWhiteSpace(trocarSenhaUrl) ? loginUrl : trocarSenhaUrl;
        var body = $@"
<p>Olá, <strong>{safeNome}</strong>,</p>
<p>Você foi cadastrado(a) como paciente na clínica <strong>{Brand}</strong>.</p>
<p>Seguem seus dados de acesso à área do cliente:</p>
<table style=""border-collapse:collapse;margin:16px 0"">
  <tr><td style=""padding:6px 12px 6px 0;color:#555"">Login</td><td><strong>{Html(emailLogin)}</strong></td></tr>
  <tr><td style=""padding:6px 12px 6px 0;color:#555"">Senha provisória</td><td><code style=""background:#f4f4f4;padding:2px 6px;border-radius:4px"">{Html(senhaProvisoria)}</code></td></tr>
</table>
<p>Por segurança, no primeiro login será solicitado que você <strong>troque a senha provisória</strong>.</p>
<p style=""margin:24px 0"">
  <a href=""{Attr(linkTroca)}"" style=""background:{Accent};color:#fff;padding:12px 22px;border-radius:6px;text-decoration:none;display:inline-block"">
    Fazer primeiro acesso
  </a>
</p>
<p style=""word-break:break-all;font-size:13px;color:#555"">{Html(linkTroca)}</p>
<p>Se você não esperava este e-mail, entre em contato conosco.</p>";
        return Wrap("Bem-vindo(a) — acesso ao portal", body);
    }

    private static string Wrap(string titulo, string bodyHtml)
    {
        return $@"<!DOCTYPE html>
<html lang=""pt-BR"">
<head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""></head>
<body style=""margin:0;padding:0;background:#f0f4f2;font-family:Georgia,'Times New Roman',serif;color:#1a1a1a"">
  <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background:#f0f4f2;padding:24px 12px"">
    <tr><td align=""center"">
      <table role=""presentation"" width=""100%"" style=""max-width:560px;background:#ffffff;border-radius:8px;overflow:hidden"">
        <tr>
          <td style=""background:{Accent};padding:20px 28px"">
            <div style=""font-size:20px;font-weight:bold;color:#fff;letter-spacing:0.02em"">{Brand}</div>
            <div style=""font-size:13px;color:#d7ebe3;margin-top:4px"">{Html(titulo)}</div>
          </td>
        </tr>
        <tr>
          <td style=""padding:28px;font-size:16px;line-height:1.55"">
            {bodyHtml}
            <hr style=""border:none;border-top:1px solid #e5e5e5;margin:28px 0 16px"">
            <p style=""font-size:12px;color:#777;margin:0"">
              {Brand} · Londrina, PR<br>
              Este é um e-mail automático. Não responda a esta mensagem.
            </p>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
    }

    private static string Html(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
    private static string Attr(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
