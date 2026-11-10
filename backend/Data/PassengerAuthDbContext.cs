using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RotaWeb.Api.Models;

namespace RotaWeb.Api.Data;

/// <summary>
/// Contexto exclusivo das credenciais de passageiros.
/// Ele aponta para o banco rotaweb_auth e nunca para o banco operacional.
/// </summary>
public sealed class PassengerAuthDbContext
    : IdentityDbContext<PassengerUser, PassengerRole, Guid>
{
    /// <summary>
    /// Construtor padrão usado pela injeção de dependência do ASP.NET Core.
    /// </summary>
    public PassengerAuthDbContext(DbContextOptions<PassengerAuthDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Sessões opacas de passageiros.
    /// </summary>
    public DbSet<PassengerSession> Sessions => Set<PassengerSession>();

    /// <summary>
    /// Configura nomes, tamanhos, índices e relacionamentos específicos do banco de autenticação.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Mantém toda a configuração padrão e segura do ASP.NET Core Identity.
        base.OnModelCreating(builder);

        // Ajusta a entidade de usuário com os campos adicionais definidos pelo RotaWeb.
        builder.Entity<PassengerUser>(entity =>
        {
            entity.Property(user => user.Nome).HasMaxLength(100).IsRequired();
            entity.Property(user => user.DataNascimento).HasColumnType("date");
            entity.Property(user => user.CriadoEm).IsRequired();
            entity.HasIndex(user => user.NormalizedEmail).IsUnique();
        });

        // Configura a tabela de sessões revogáveis.
        builder.Entity<PassengerSession>(entity =>
        {
            entity.ToTable("PassengerSessions");
            entity.HasKey(session => session.Id);
            entity.Property(session => session.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(session => session.SecurityStamp).IsRequired();
            entity.Property(session => session.UserAgentHash).HasMaxLength(64);
            entity.HasIndex(session => session.TokenHash).IsUnique();
            entity.HasIndex(session => new { session.UserId, session.ExpiraEm });
            entity.HasOne(session => session.User)
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
