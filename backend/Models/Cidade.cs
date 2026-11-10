namespace RotaWeb.Api.Models;

/// <summary>
/// Município brasileiro persistido somente quando é utilizado por uma viagem.
/// O Id é o código oficial de sete dígitos do IBGE, conforme a documentação do projeto.
/// </summary>
public sealed class Cidade
{
    /// <summary>
    /// Código IBGE do município; não é gerado pelo banco.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Nome oficial do município.
    /// </summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Sigla da unidade federativa com duas letras.
    /// </summary>
    public string Uf { get; set; } = string.Empty;
}
