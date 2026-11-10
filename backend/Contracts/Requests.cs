using System.ComponentModel.DataAnnotations;

namespace RotaWeb.Api.Contracts;

/// <summary>
/// Dados recebidos no cadastro público de passageiro.
/// A confirmação também é validada no servidor para não depender apenas do frontend.
/// </summary>
public sealed class RegisterRequest
{
    /// <summary>
    /// Nome de exibição, entre 3 e 100 caracteres.
    /// </summary>
    [Required, StringLength(100, MinimumLength = 3)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// E-mail usado como identificador de acesso.
    /// </summary>
    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Data de nascimento do comprador. O RotaWeb exige 18 anos completos para criar conta compradora.
    /// </summary>
    [Required]
    public DateOnly? DataNascimento { get; set; }

    /// <summary>
    /// Senha submetida somente ao servidor de autenticação; nunca é gravada em texto puro.
    /// </summary>
    [Required, StringLength(200, MinimumLength = 10)]
    public string Senha { get; set; } = string.Empty;

    /// <summary>
    /// Repetição da senha usada para detectar erro de digitação.
    /// </summary>
    [Required, Compare(nameof(Senha))]
    public string ConfirmarSenha { get; set; } = string.Empty;
}

/// <summary>
/// Dados de login de passageiro.
/// </summary>
public sealed class LoginRequest
{
    /// <summary>
    /// E-mail da conta.
    /// </summary>
    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Senha em texto somente durante a requisição TLS.
    /// </summary>
    [Required, StringLength(200)]
    public string Senha { get; set; } = string.Empty;

    /// <summary>
    /// Solicita sessão persistente limitada por prazo absoluto.
    /// </summary>
    public bool ManterConectado { get; set; }
}

/// <summary>
/// Atualização do nome do perfil do passageiro.
/// </summary>
public sealed class UpdateProfileRequest
{
    /// <summary>
    /// Novo nome de exibição.
    /// </summary>
    [Required, StringLength(100, MinimumLength = 3)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Data de nascimento do comprador. A conta compradora deve permanecer 18+.
    /// </summary>
    [Required]
    public DateOnly? DataNascimento { get; set; }
}

/// <summary>
/// Alteração autenticada de senha.
/// </summary>
public sealed class ChangePasswordRequest
{
    /// <summary>
    /// Senha atual, exigida para autorizar a mudança.
    /// </summary>
    [Required]
    public string SenhaAtual { get; set; } = string.Empty;

    /// <summary>
    /// Nova senha submetida à política forte configurada no Identity.
    /// </summary>
    [Required, StringLength(200, MinimumLength = 10)]
    public string NovaSenha { get; set; } = string.Empty;

    /// <summary>
    /// Confirmação da nova senha.
    /// </summary>
    [Required, Compare(nameof(NovaSenha))]
    public string ConfirmarNovaSenha { get; set; } = string.Empty;
}

/// <summary>
/// Login administrativo inicial por senha.
/// Após senha válida, o servidor cria uma sessão administrativa curta e revogável.
/// </summary>
public sealed class AdminLoginRequest
{
    /// <summary>
    /// E-mail administrativo.
    /// </summary>
    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Senha administrativa.
    /// </summary>
    [Required, StringLength(200)]
    public string Senha { get; set; } = string.Empty;
}

 /// <summary>
/// Confirmação de passagem de um assento específico.
/// </summary>
public sealed class CreateTicketRequest
{
    /// <summary>
    /// Identificador da viagem escolhida.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int ViagemId { get; set; }

    /// <summary>
    /// Assento solicitado.
    /// </summary>
    [Range(1, 60)]
    public int Assento { get; set; }

    /// <summary>
    /// Nome da pessoa que viajará.
    /// </summary>
    [Required, StringLength(100, MinimumLength = 3)]
    public string Passageiro { get; set; } = string.Empty;

    /// <summary>
    /// Documento textual do passageiro; a API valida tamanho e formato básico.
    /// </summary>
    [Required, StringLength(40, MinimumLength = 3)]
    public string Documento { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string TipoDocumento { get; set; } = string.Empty;

    [Required]
    public DateOnly? DataNascimento { get; set; }

    [StringLength(100)]
    public string? ResponsavelNome { get; set; }

    [StringLength(40)]
    public string? ResponsavelDocumento { get; set; }

    public DateOnly? ResponsavelDataNascimento { get; set; }

    [StringLength(40)]
    public string? ResponsavelVinculo { get; set; }

    public bool ResponsavelAcompanha { get; set; }

    public bool DocumentacaoMenorConfirmada { get; set; }

    /// <summary>
    /// Preço que o frontend exibiu no momento da confirmação.
    /// O servidor rejeita a compra se o preço mudou desde a consulta.
    /// </summary>
    [Range(1, 1_000_000)]
    public int PrecoEsperadoCentavos { get; set; }
}


/// <summary>
/// Passageiro associado a uma das poltronas de uma compra múltipla.
/// </summary>
public sealed class TicketPassengerRequest
{
    /// <summary>
    /// Número da poltrona escolhida.
    /// </summary>
    [Range(1, 60)]
    public int Assento { get; set; }

    /// <summary>
    /// Nome da pessoa que embarcará nesta poltrona.
    /// </summary>
    [Required, StringLength(100, MinimumLength = 3)]
    public string Passageiro { get; set; } = string.Empty;

    /// <summary>
    /// Documento informado para conferência no embarque.
    /// </summary>
    [Required, StringLength(40, MinimumLength = 3)]
    public string Documento { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de documento. Para 12+ o sistema não aceita Certidão de Nascimento como documento de embarque.
    /// </summary>
    [Required, StringLength(30)]
    public string TipoDocumento { get; set; } = string.Empty;

    /// <summary>
    /// Data de nascimento do passageiro. A idade é calculada na data da viagem.
    /// </summary>
    [Required]
    public DateOnly? DataNascimento { get; set; }

    /// <summary>
    /// Dados do responsável quando o passageiro é menor de 18 anos.
    /// </summary>
    [StringLength(100)]
    public string? ResponsavelNome { get; set; }

    [StringLength(40)]
    public string? ResponsavelDocumento { get; set; }

    public DateOnly? ResponsavelDataNascimento { get; set; }

    [StringLength(40)]
    public string? ResponsavelVinculo { get; set; }

    /// <summary>
    /// Nesta versão do RotaWeb, todo menor deve viajar acompanhado pelo responsável adulto informado.
    /// É uma regra de negócio mais protetiva que o mínimo legal aplicável aos jovens de 16 e 17 anos.
    /// </summary>
    public bool ResponsavelAcompanha { get; set; }

    /// <summary>
    /// Declaração de que os documentos exigidos serão apresentados à empresa no embarque.
    /// </summary>
    public bool DocumentacaoMenorConfirmada { get; set; }
}

/// <summary>
/// Confirma de uma a cinco poltronas em uma única compra.
/// O pagamento Pix desta versão é demonstrativo e não movimenta dinheiro real.
/// </summary>
public sealed class CreateTicketBatchRequest
{
    /// <summary>
    /// Viagem escolhida.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int ViagemId { get; set; }

    /// <summary>
    /// Passageiros/poltronas. O controller reforça o limite de 1 a 5 itens.
    /// </summary>
    [Required]
    public List<TicketPassengerRequest> Passageiros { get; set; } = [];

    /// <summary>
    /// Preço unitário exibido pelo frontend, em centavos.
    /// </summary>
    [Range(1, 1_000_000)]
    public int PrecoEsperadoCentavos { get; set; }

    /// <summary>
    /// Referência da simulação Pix exibida ao usuário.
    /// Não é usada para processar uma transação bancária.
    /// </summary>
    [Required, StringLength(80, MinimumLength = 8)]
    public string ReferenciaPagamentoDemo { get; set; } = string.Empty;
}

/// <summary>
/// Cadastro ou edição administrativa de ônibus.
/// </summary>
public sealed class BusRequest
{
    /// <summary>
    /// Prefixo único da frota.
    /// </summary>
    [Required, StringLength(12, MinimumLength = 2)]
    public string Prefixo { get; set; } = string.Empty;

    /// <summary>
    /// Placa sem hífen com sete caracteres.
    /// </summary>
    [Required, RegularExpression("^[A-Za-z0-9]{7}$")]
    public string Placa { get; set; } = string.Empty;

    /// <summary>
    /// Modelo do veículo.
    /// </summary>
    [Required, StringLength(80, MinimumLength = 3)]
    public string Modelo { get; set; } = string.Empty;

    /// <summary>
    /// Categoria comercial permitida pelo projeto.
    /// </summary>
    [Required]
    public string Categoria { get; set; } = string.Empty;

    /// <summary>
    /// Capacidade total entre 16 e 60 lugares.
    /// </summary>
    [Range(16, 60)]
    public int Assentos { get; set; }
}

/// <summary>
/// Cadastro ou edição administrativa de viagem.
/// Horários devem chegar em ISO 8601 com fuso e são convertidos para UTC.
/// </summary>
public sealed class TripRequest
{
    /// <summary>
    /// Veículo ativo que realizará a viagem.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int OnibusId { get; set; }

    /// <summary>
    /// Código IBGE da cidade de origem.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int OrigemId { get; set; }

    /// <summary>
    /// Código IBGE da cidade de destino.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int DestinoId { get; set; }

    /// <summary>
    /// Data/hora de partida com offset.
    /// </summary>
    public DateTimeOffset Partida { get; set; }

    /// <summary>
    /// Data/hora prevista de chegada com offset.
    /// </summary>
    public DateTimeOffset Chegada { get; set; }

    /// <summary>
    /// Preço inteiro em centavos.
    /// </summary>
    [Range(1, 1_000_000)]
    public int PrecoCentavos { get; set; }
}
