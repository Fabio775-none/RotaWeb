namespace RotaWeb.Api.Models;

/// <summary>
/// Sessão opaca de passageiro.
/// O cookie guarda somente o token aleatório; o banco guarda apenas o hash desse token.
/// Dessa forma, um vazamento do banco não revela cookies utilizáveis diretamente.
/// </summary>
public sealed class PassengerSession
{
    /// <summary>
    /// Identificador interno da sessão.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Conta proprietária da sessão.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Hash SHA-256 hexadecimal do token aleatório enviado ao navegador.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Cópia do SecurityStamp da conta no instante do login.
    /// Alterar a senha ou o selo invalida sessões antigas automaticamente.
    /// </summary>
    public string SecurityStamp { get; set; } = string.Empty;

    /// <summary>
    /// Data UTC de criação da sessão.
    /// </summary>
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data UTC limite da sessão.
    /// </summary>
    public DateTime ExpiraEm { get; set; }

    /// <summary>
    /// Data UTC de revogação, preenchida no logout ou por ação de segurança.
    /// </summary>
    public DateTime? RevogadoEm { get; set; }

    /// <summary>
    /// Indica se o usuário solicitou uma sessão persistente.
    /// </summary>
    public bool Persistente { get; set; }

    /// <summary>
    /// Hash curto do User-Agent para auditoria sem armazenar o cabeçalho completo.
    /// </summary>
    public string? UserAgentHash { get; set; }

    /// <summary>
    /// Navegação EF para a conta de passageiro.
    /// </summary>
    public PassengerUser User { get; set; } = null!;
}
