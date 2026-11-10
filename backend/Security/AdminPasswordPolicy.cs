namespace RotaWeb.Api.Security;

/// <summary>
/// Centraliza a política adicional aplicada exclusivamente às credenciais administrativas.
/// O IdentityOptions do ASP.NET Core é compartilhado no contêiner, portanto esta validação explícita
/// mantém a política de passageiro fiel à documentação e torna a senha administrativa mais rigorosa.
/// </summary>
public static class AdminPasswordPolicy
{
    /// <summary>
    /// Valida a senha administrativa sem registrar, retornar ou transformar o segredo em log.
    /// </summary>
    public static bool IsValid(string password, out string error)
    {
        // Não aceita valor ausente.
        if (string.IsNullOrWhiteSpace(password))
        {
            error = "A senha administrativa é obrigatória.";
            return false;
        }

        // Exige comprimento superior ao cadastro público.
        if (password.Length < 14)
        {
            error = "A senha administrativa precisa ter pelo menos 14 caracteres.";
            return false;
        }

        // Exige todos os grupos principais de caracteres.
        if (!password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(character => !char.IsLetterOrDigit(character)))
        {
            error = "A senha administrativa precisa conter maiúscula, minúscula, número e símbolo.";
            return false;
        }

        // Impede sequências excessivamente repetitivas.
        if (password.Distinct().Count() < 6)
        {
            error = "A senha administrativa precisa conter pelo menos 6 caracteres distintos.";
            return false;
        }

        // Nenhuma regra foi violada.
        error = string.Empty;
        return true;
    }
}
