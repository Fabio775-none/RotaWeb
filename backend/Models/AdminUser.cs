using Microsoft.AspNetCore.Identity;

namespace RotaWeb.Api.Models;

/// <summary>
/// Conta administrativa armazenada apenas no banco rotaweb_admin_auth.
/// Separar essas credenciais reduz o impacto de um comprometimento do banco operacional ou de passageiros.
/// </summary>
public sealed class AdminUser : IdentityUser<Guid>
{
    /// <summary>
    /// Nome do administrador mostrado na área de gestão.
    /// </summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Momento UTC em que a conta administrativa foi criada.
    /// </summary>
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Tipo de perfil administrativo separado do perfil de passageiro.
/// </summary>
public sealed class AdminRole : IdentityRole<Guid>
{
}
