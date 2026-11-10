using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RotaWeb.Api.Contracts;
using RotaWeb.Api.Models;
using RotaWeb.Api.Services;

namespace RotaWeb.Api.Controllers;

/// <summary>
/// Acesso administrativo por e-mail e senha, com bloqueio de tentativas, CSRF
/// e sessão curta, opaca e revogável. Não exige aplicativo autenticador.
/// </summary>
[ApiController]
[Route("api/admin/auth")]
public sealed class AdminAuthController : ControllerBase
{
    private readonly UserManager<AdminUser> _users;
    private readonly AdminSessionService _sessions;

    public AdminAuthController(UserManager<AdminUser> users, AdminSessionService sessions)
    {
        _users = users;
        _sessions = sessions;
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("admin-auth")]
    public async Task<IActionResult> Login(AdminLoginRequest request)
    {
        // Uma mensagem única para contas inexistentes, bloqueadas ou senha incorreta.
        var user = await _users.FindByEmailAsync(request.Email.Trim());
        if (user is null ||
            (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow))
        {
            return Unauthorized(new { mensagem = "Credenciais administrativas inválidas." });
        }

        if (!await _users.CheckPasswordAsync(user, request.Senha))
        {
            user.AccessFailedCount += 1;
            if (user.AccessFailedCount >= 4)
            {
                user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(20);
                user.AccessFailedCount = 0;
            }

            var updated = await _users.UpdateAsync(user);
            if (!updated.Succeeded)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensagem = "Falha ao verificar a conta administrativa." });

            return Unauthorized(new { mensagem = "Credenciais administrativas inválidas." });
        }

        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        var result = await _users.UpdateAsync(user);
        if (!result.Succeeded)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensagem = "Falha ao atualizar a conta administrativa." });

        // A identidade administrativa depende sempre de senha válida, mesmo no Development.
        // Contas anteriormente configuradas com TOTP continuam utilizáveis, sem apagar dados.
        await _sessions.IssueAuthenticatedAsync(HttpContext, user);
        return Ok(new { mensagem = "Acesso administrativo liberado." });
    }

    [HttpGet("me")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Me()
    {
        var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(id, out var userId)) return Unauthorized();
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null) return Unauthorized();
        return Ok(new { user.Id, user.Nome, user.Email, perfil = "Administrador" });
    }

    [HttpPost("logout")]
    [Authorize(Policy = "AdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _sessions.RevokeCurrentAdminAsync(HttpContext);
        return NoContent();
    }
}
