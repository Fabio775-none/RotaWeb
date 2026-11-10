using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RotaWeb.Api.Contracts;
using RotaWeb.Api.Models;
using RotaWeb.Api.Security;
using RotaWeb.Api.Services;

namespace RotaWeb.Api.Controllers;

/// <summary>
/// Cadastro, login, logout e perfil do passageiro.
/// Credenciais são manipuladas exclusivamente pelo banco rotaweb_auth.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// UserManager aplica normalização, política de senha, hashing e lockout do Identity.
    /// </summary>
    private readonly UserManager<PassengerUser> _users;

    /// <summary>
    /// Serviço de sessão opaca revogável.
    /// </summary>
    private readonly PassengerSessionService _sessions;

    /// <summary>
    /// Inicializa dependências do controller.
    /// </summary>
    public AuthController(UserManager<PassengerUser> users, PassengerSessionService sessions)
    {
        _users = users;
        _sessions = sessions;
    }

    /// <summary>
    /// Cria uma conta pública com papel Passageiro.
    /// </summary>
    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        // Normaliza espaços para que validações e exibição usem o mesmo valor.
        var name = request.Nome.Trim();
        var email = request.Email.Trim();

        // A conta compradora do RotaWeb é restrita a maiores de 18 anos.
        if (!request.DataNascimento.HasValue)
        {
            return BadRequest(new { mensagem = "Informe a data de nascimento." });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (!AgePolicy.IsAdult(request.DataNascimento.Value, today))
        {
            return BadRequest(new { mensagem = "A conta compradora deve pertencer a uma pessoa com 18 anos ou mais." });
        }

        // Resposta genérica para e-mail existente evita detalhar o estado interno do Identity.
        if (await _users.FindByEmailAsync(email) is not null)
        {
            return Conflict(new { mensagem = "Já existe uma conta com este e-mail." });
        }

        // Cria entidade com UUID aleatório e lockout habilitado desde o primeiro acesso.
        var user = new PassengerUser
        {
            Id = Guid.NewGuid(),
            Nome = name,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            LockoutEnabled = true,
            DataNascimento = request.DataNascimento.Value,
            CriadoEm = DateTime.UtcNow
        };

        // Identity valida a política e persiste somente PasswordHash.
        var result = await _users.CreateAsync(user, request.Senha);

        // Converte erros conhecidos em mensagem adequada sem vazar dados sensíveis.
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                mensagem = "Não foi possível criar a conta.",
                erros = result.Errors.Select(error => error.Description).ToArray()
            });
        }

        // Toda conta pública recebe exclusivamente o papel Passageiro.
        await _users.AddToRoleAsync(user, "Passageiro");

        // Retorna somente dados de perfil; hash e campos de segurança nunca saem da API.
        return StatusCode(StatusCodes.Status201Created, new
        {
            mensagem = "Conta criada com sucesso.",
            usuario = new { user.Id, user.Nome, user.Email, user.DataNascimento }
        });
    }

    /// <summary>
    /// Valida senha, aplica lockout contra força bruta e emite sessão opaca HttpOnly.
    /// </summary>
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        // Busca a conta pelo e-mail normalizado pelo próprio Identity.
        var user = await _users.FindByEmailAsync(request.Email.Trim());

        // Mensagem única para conta inexistente, bloqueada ou senha errada reduz enumeração de usuários.
        if (user is null || await _users.IsLockedOutAsync(user))
        {
            return Unauthorized(new { mensagem = "E-mail ou senha incorretos." });
        }

        // Verifica hash usando PasswordHasher configurado pelo Identity.
        var valid = await _users.CheckPasswordAsync(user, request.Senha);

        // Falha incrementa o contador de lockout e devolve mensagem genérica.
        if (!valid)
        {
            await _users.AccessFailedAsync(user);
            return Unauthorized(new { mensagem = "E-mail ou senha incorretos." });
        }

        // Login correto zera o contador de falhas anterior.
        await _users.ResetAccessFailedCountAsync(user);

        // Cria sessão revogável e grava somente token opaco em cookie HttpOnly.
        await _sessions.IssueAsync(HttpContext, user, request.ManterConectado);

        // Resposta contém apenas dados adequados à interface.
        return Ok(new
        {
            mensagem = "Login realizado com sucesso.",
            usuario = new { user.Id, user.Nome, user.Email, user.DataNascimento, perfil = "Passageiro" }
        });
    }

    /// <summary>
    /// Retorna o perfil básico da sessão atual.
    /// </summary>
    [HttpGet("me")]
    [Authorize(Policy = "PassengerOnly")]
    public async Task<IActionResult> Me()
    {
        // Extrai UUID somente depois da autorização da sessão.
        var userId = User.GetRequiredUserId();

        // Recarrega a conta para refletir mudanças recentes de nome ou e-mail.
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Unauthorized();
        }

        // Não expõe propriedades internas de segurança.
        return Ok(new { user.Id, user.Nome, user.Email, user.DataNascimento, perfil = "Passageiro" });
    }

    /// <summary>
    /// Atualiza somente o nome de exibição do passageiro.
    /// </summary>
    [HttpPut("profile")]
    [Authorize(Policy = "PassengerOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        // Carrega conta autenticada.
        var user = await _users.FindByIdAsync(User.GetRequiredUserId().ToString());
        if (user is null)
        {
            return Unauthorized();
        }

        if (!request.DataNascimento.HasValue ||
            !AgePolicy.IsAdult(request.DataNascimento.Value, DateOnly.FromDateTime(DateTime.UtcNow)))
        {
            return BadRequest(new { mensagem = "A conta compradora deve permanecer vinculada a uma pessoa com 18 anos ou mais." });
        }

        // Atualiza nome e data de nascimento já validados.
        user.Nome = request.Nome.Trim();
        user.DataNascimento = request.DataNascimento.Value;
        var result = await _users.UpdateAsync(user);

        // Falha inesperada do Identity retorna problema sem detalhes secretos.
        if (!result.Succeeded)
        {
            return BadRequest(new { mensagem = "Não foi possível atualizar o perfil." });
        }

        // Retorna o perfil atualizado.
        return Ok(new { mensagem = "Perfil atualizado.", usuario = new { user.Id, user.Nome, user.Email, user.DataNascimento } });
    }

    /// <summary>
    /// Altera a senha com confirmação da senha atual e revoga todas as sessões anteriores.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize(Policy = "PassengerOnly")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        // Localiza a conta autenticada.
        var user = await _users.FindByIdAsync(User.GetRequiredUserId().ToString());
        if (user is null)
        {
            return Unauthorized();
        }

        // Identity confirma a senha atual, valida a nova e atualiza SecurityStamp.
        var result = await _users.ChangePasswordAsync(user, request.SenhaAtual, request.NovaSenha);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                mensagem = "Não foi possível alterar a senha.",
                erros = result.Errors.Select(error => error.Description).ToArray()
            });
        }

        // Revoga todas as sessões existentes para neutralizar cookies antigos eventualmente copiados.
        await _sessions.RevokeAllAsync(user.Id);

        // Emite uma sessão nova e curta para a sessão atual continuar autenticada.
        await _sessions.IssueAsync(HttpContext, user, persistent: false);

        // Confirma a alteração sem retornar qualquer credencial.
        return Ok(new { mensagem = "Senha alterada com sucesso." });
    }

    /// <summary>
    /// Revoga a sessão atual no servidor e remove o cookie.
    /// </summary>
    [HttpPost("logout")]
    [Authorize(Policy = "PassengerOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // Revoga o token e remove o cookie.
        await _sessions.RevokeCurrentAsync(HttpContext);

        // 204 evita corpo desnecessário.
        return NoContent();
    }
}
