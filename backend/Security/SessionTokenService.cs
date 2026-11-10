using System.Security.Cryptography;
using System.Text;

namespace RotaWeb.Api.Security;

/// <summary>
/// Cria tokens de sessão criptograficamente aleatórios e gera hashes para persistência segura.
/// O token bruto existe somente no navegador; o banco recebe apenas o hash.
/// </summary>
public sealed class SessionTokenService
{
    /// <summary>
    /// Gera 32 bytes aleatórios e os codifica em Base64URL sem padding.
    /// </summary>
    public string CreateToken()
    {
        // RandomNumberGenerator usa fonte criptograficamente segura do sistema operacional.
        var bytes = RandomNumberGenerator.GetBytes(32);

        // Base64URL evita caracteres problemáticos em cookies.
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Produz SHA-256 hexadecimal do token para armazenamento e consulta.
    /// </summary>
    public string HashToken(string token)
    {
        // Converte o texto para bytes UTF-8 antes do hash.
        var input = Encoding.UTF8.GetBytes(token);

        // Calcula o digest de 256 bits.
        var hash = SHA256.HashData(input);

        // Converte para hexadecimal estável e sem separadores.
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Gera um identificador não reversível do User-Agent para auditoria básica.
    /// </summary>
    public string? HashUserAgent(string? userAgent)
    {
        // Evita criar um valor artificial quando o cabeçalho não existe.
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        // Limita a entrada antes de hashear para evitar uso desnecessário de memória com cabeçalhos anômalos.
        var normalized = userAgent.Length > 512 ? userAgent[..512] : userAgent;

        // Reaproveita SHA-256, adequado porque o objetivo aqui é identificação e não senha.
        return HashToken(normalized);
    }
}
