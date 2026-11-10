using Microsoft.AspNetCore.Identity;

namespace RotaWeb.Api.Models;

/// <summary>
/// Conta de passageiro armazenada exclusivamente no banco rotaweb_auth.
/// A classe herda do Identity para aproveitar hash de senha, lockout e token TOTP testados pelo framework.
/// </summary>
public sealed class PassengerUser : IdentityUser<Guid>
{
    /// <summary>
    /// Nome exibido para o passageiro dentro do RotaWeb.
    /// </summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Data de nascimento usada para aplicar a regra de comprador maior de 18 anos.
    /// Pode permanecer nula apenas em contas legadas até que o perfil seja atualizado.
    /// </summary>
    public DateOnly? DataNascimento { get; set; }

    /// <summary>
    /// Momento UTC em que a conta foi criada.
    /// </summary>
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Tipo de perfil do banco de passageiros.
/// Um tipo próprio evita conflito de serviços com o banco separado de administradores.
/// </summary>
public sealed class PassengerRole : IdentityRole<Guid>
{
}
