using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ClinicaPsi.Application.Services.MercadoPago;

/// <summary>
/// Valida o header x-signature dos webhooks Mercado Pago
/// (manifest: id:{data.id};request-id:{x-request-id};ts:{ts};).
/// </summary>
public static class MercadoPagoSignatureValidator
{
    public static bool TryValidate(
        string? webhookSecret,
        string? xSignature,
        string? xRequestId,
        string? dataId,
        out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            error = "webhook_secret_missing";
            return false;
        }

        if (string.IsNullOrWhiteSpace(xSignature))
        {
            error = "x_signature_missing";
            return false;
        }

        if (string.IsNullOrWhiteSpace(xRequestId))
        {
            error = "x_request_id_missing";
            return false;
        }

        string? ts = null;
        string? v1 = null;
        foreach (var part in xSignature.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kv.Length != 2) continue;
            if (kv[0].Equals("ts", StringComparison.OrdinalIgnoreCase)) ts = kv[1];
            else if (kv[0].Equals("v1", StringComparison.OrdinalIgnoreCase)) v1 = kv[1];
        }

        if (string.IsNullOrWhiteSpace(ts) || string.IsNullOrWhiteSpace(v1))
        {
            error = "x_signature_malformed";
            return false;
        }

        // data.id em minúsculas quando alfanumérico (doc MP)
        var idPart = string.IsNullOrWhiteSpace(dataId)
            ? string.Empty
            : dataId.ToLowerInvariant();

        var manifest = $"id:{idPart};request-id:{xRequestId};ts:{ts};";
        var keyBytes = Encoding.UTF8.GetBytes(webhookSecret);
        var manifestBytes = Encoding.UTF8.GetBytes(manifest);

        byte[] hash;
        using (var hmac = new HMACSHA256(keyBytes))
            hash = hmac.ComputeHash(manifestBytes);

        var computed = Convert.ToHexString(hash).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(v1.ToLowerInvariant())))
        {
            error = "x_signature_invalid";
            return false;
        }

        // Rejeita timestamps absurdamente antigos/futuros (±10 min)
        if (long.TryParse(ts, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tsSeconds))
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - tsSeconds) > 600)
            {
                error = "x_signature_ts_out_of_range";
                return false;
            }
        }

        return true;
    }
}
