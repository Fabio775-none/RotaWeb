using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RotaWeb.Api.Data;

namespace RotaWeb.Api.Controllers;

/// <summary>
/// Readiness separada do liveness; a API só está pronta quando seus três bancos respondem.
/// Não retorna nomes de servidores, connection strings ou mensagens internas de conexão.
/// </summary>
[ApiController]
[Route("api/security")]
public sealed class ReadinessController : ControllerBase
{
    private readonly BusinessDbContext _business;
    private readonly PassengerAuthDbContext _passenger;
    private readonly AdminAuthDbContext _admin;

    public ReadinessController(BusinessDbContext business, PassengerAuthDbContext passenger, AdminAuthDbContext admin)
    {
        _business = business;
        _passenger = passenger;
        _admin = admin;
    }

    [HttpGet("ready")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        try
        {
            // Checagem em sequência: baixo custo e sem inicialização implícita de banco.
            if (await _business.Database.CanConnectAsync(cancellationToken) &&
                await _passenger.Database.CanConnectAsync(cancellationToken) &&
                await _admin.Database.CanConnectAsync(cancellationToken))
            {
                return Ok(new { status = "ready" });
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A resposta é sempre genérica para não expor a infraestrutura.
        }
        return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unavailable" });
    }
}
