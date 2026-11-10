namespace RotaWeb.Api.Services;

/// <summary>
/// Regras de idade usadas pelo cadastro e pela emissão de passagens.
/// </summary>
public static class AgePolicy
{
    /// <summary>
    /// Calcula a idade completa em uma data de referência.
    /// </summary>
    public static int CalculateAge(DateOnly birthDate, DateOnly referenceDate)
    {
        var age = referenceDate.Year - birthDate.Year;
        if (birthDate > referenceDate.AddYears(-age))
        {
            age--;
        }
        return age;
    }

    /// <summary>
    /// Retorna true quando a pessoa tem pelo menos 18 anos completos na data de referência.
    /// </summary>
    public static bool IsAdult(DateOnly birthDate, DateOnly referenceDate)
        => birthDate <= referenceDate && CalculateAge(birthDate, referenceDate) >= 18;
}
