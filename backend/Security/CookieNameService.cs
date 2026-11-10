namespace RotaWeb.Api.Security;

/// <summary>
/// Resolve os nomes dos cookies conforme o ambiente.
/// Em produção usamos o prefixo __Host-, que impede Domain e exige Secure + Path=/.
/// </summary>
public sealed class CookieNameService
{
    /// <summary>
    /// Indica se a aplicação está em ambiente de desenvolvimento local.
    /// </summary>
    private readonly bool _development;

    /// <summary>
    /// Constrói o serviço a partir do ambiente atual do ASP.NET Core.
    /// </summary>
    public CookieNameService(IWebHostEnvironment environment)
    {
        _development = environment.IsDevelopment();
    }

    /// <summary>
    /// Cookie opaco de sessão do passageiro.
    /// </summary>
    public string PassengerSession => _development ? "RotaWeb.Passenger" : "__Host-RotaWeb.Passenger";

    /// <summary>
    /// Cookie opaco da sessão administrativa autenticada.
    /// </summary>
    public string AdminSession => _development ? "RotaWeb.Admin" : "__Host-RotaWeb.Admin";

    /// <summary>
    /// Cookie legado removido durante login, para compatibilidade com versões anteriores.
    /// </summary>
    public string AdminPreAuth => _development ? "RotaWeb.Admin.PreAuth" : "__Host-RotaWeb.Admin.PreAuth";

    /// <summary>
    /// Nome do cookie interno do sistema antiforgery.
    /// </summary>
    public string Antiforgery => _development ? "RotaWeb.Antiforgery" : "__Host-RotaWeb.Antiforgery";
}
