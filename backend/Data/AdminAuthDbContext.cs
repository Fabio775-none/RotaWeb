using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RotaWeb.Api.Models;

namespace RotaWeb.Api.Data;

/// <summary>
/// Contexto exclusivo das credenciais administrativas.
/// O isolamento físico permite política de backup, usuário PostgreSQL e privilégios próprios.
/// </summary>
public sealed class AdminAuthDbContext
    : IdentityDbContext<AdminUser, AdminRole, Guid>
{
    /// <summary>
    /// Construtor usado pela injeção de dependência.
    /// </summary>
    public AdminAuthDbContext(DbContextOptions<AdminAuthDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Sessões administrativas persistidas.
    /// </summary>
    public DbSet<AdminSession> Sessions => Set<AdminSession>();

    /// <summary>
    /// Configura a segurança e os índices do banco administrativo.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Carrega o modelo padrão do Identity.
        base.OnModelCreating(builder);

        // Configura os campos adicionais da conta administrativa.
        builder.Entity<AdminUser>(entity =>
        {
            entity.Property(user => user.Nome).HasMaxLength(100).IsRequired();
            entity.Property(user => user.CriadoEm).IsRequired();
            entity.HasIndex(user => user.NormalizedEmail).IsUnique();
        });

        // Configura as sessões opacas de administrador.
        builder.Entity<AdminSession>(entity =>
        {
            entity.ToTable("AdminSessions");
            entity.HasKey(session => session.Id);
            entity.Property(session => session.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(session => session.SecurityStamp).IsRequired();
            entity.Property(session => session.Estagio).HasMaxLength(20).IsRequired();
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
