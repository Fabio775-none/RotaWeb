namespace RotaWeb.Api.Security;

/// <summary>
/// Nomes centralizados dos esquemas de autenticação.
/// Manter constantes evita divergência entre configuração, autorização e handlers.
/// </summary>
public static class AuthSchemes
{
    /// <summary>
    /// Esquema de sessão de passageiro.
    /// </summary>
    public const string Passenger = "RotaWebPassengerSession";

    /// <summary>
    /// Esquema de sessão administrativa após login com senha.
    /// </summary>
    public const string Admin = "RotaWebAdminSession";
}
