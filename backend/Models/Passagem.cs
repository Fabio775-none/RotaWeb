namespace RotaWeb.Api.Models;

/// <summary>
/// Registro histórico de uma passagem confirmada ou cancelada.
/// O usuário comprador pertence ao banco de autenticação e por isso o vínculo é validado pela aplicação.
/// </summary>
public sealed class Passagem
{
    /// <summary>
    /// UUID público da passagem.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Código amigável de consulta no formato RW + 14 caracteres hexadecimais.
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>
    /// Viagem adquirida.
    /// </summary>
    public int ViagemId { get; set; }

    /// <summary>
    /// UUID da conta compradora no banco rotaweb_auth.
    /// </summary>
    public Guid UsuarioId { get; set; }

    /// <summary>
    /// Número da poltrona.
    /// </summary>
    public int Assento { get; set; }

    /// <summary>
    /// Nome da pessoa que embarcará.
    /// </summary>
    public string Passageiro { get; set; } = string.Empty;

    /// <summary>
    /// Documento textual informado para o passageiro.
    /// </summary>
    public string Documento { get; set; } = string.Empty;

    /// <summary>
    /// Tipo do documento apresentado para conferência no embarque.
    /// </summary>
    public string? TipoDocumento { get; set; }

    /// <summary>
    /// Data de nascimento do passageiro, usada para determinar a idade na data da viagem.
    /// </summary>
    public DateOnly? DataNascimento { get; set; }

    /// <summary>
    /// Nome do responsável adulto quando o passageiro é menor de 18 anos.
    /// </summary>
    public string? ResponsavelNome { get; set; }

    /// <summary>
    /// Documento do responsável adulto.
    /// </summary>
    public string? ResponsavelDocumento { get; set; }

    /// <summary>
    /// Data de nascimento do responsável, para confirmar que possui 18 anos ou mais.
    /// </summary>
    public DateOnly? ResponsavelDataNascimento { get; set; }

    /// <summary>
    /// Vínculo declarado entre o menor e o adulto responsável.
    /// </summary>
    public string? ResponsavelVinculo { get; set; }

    /// <summary>
    /// Confirmação de que o responsável informado acompanhará o menor.
    /// </summary>
    public bool ResponsavelAcompanha { get; set; }

    /// <summary>
    /// Confirmação do comprador de que a documentação exigida será apresentada no embarque.
    /// </summary>
    public bool DocumentacaoMenorConfirmada { get; set; }

    /// <summary>
    /// Preço histórico copiado no momento da compra.
    /// </summary>
    public int ValorCentavos { get; set; }

    /// <summary>
    /// Status: Confirmada ou Cancelada.
    /// </summary>
    public string Status { get; set; } = "Confirmada";

    /// <summary>
    /// Instante UTC da confirmação.
    /// </summary>
    public DateTime CompradoEm { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Instante UTC do cancelamento, quando houver.
    /// </summary>
    public DateTime? CanceladoEm { get; set; }

    /// <summary>
    /// Navegação para a viagem.
    /// </summary>
    public Viagem Viagem { get; set; } = null!;
}
