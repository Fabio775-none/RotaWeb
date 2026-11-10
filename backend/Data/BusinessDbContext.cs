using Microsoft.EntityFrameworkCore;
using RotaWeb.Api.Models;

namespace RotaWeb.Api.Data;

/// <summary>
/// Contexto do banco operacional rotaweb.
/// Credenciais e hashes de senha não pertencem a este banco.
/// </summary>
public sealed class BusinessDbContext : DbContext
{
    /// <summary>
    /// Construtor usado pela injeção de dependência.
    /// </summary>
    public BusinessDbContext(DbContextOptions<BusinessDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Municípios utilizados pelas viagens.
    /// </summary>
    public DbSet<Cidade> Cidades => Set<Cidade>();

    /// <summary>
    /// Frota de ônibus.
    /// </summary>
    public DbSet<Onibus> Onibus => Set<Onibus>();

    /// <summary>
    /// Programação de viagens.
    /// </summary>
    public DbSet<Viagem> Viagens => Set<Viagem>();

    /// <summary>
    /// Histórico de passagens.
    /// </summary>
    public DbSet<Passagem> Passagens => Set<Passagem>();

    /// <summary>
    /// Trilha de auditoria administrativa.
    /// </summary>
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    /// <summary>
    /// Define as restrições físicas que complementam a validação da API.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Configura a tabela de cidades com chave fornecida pelo IBGE.
        builder.Entity<Cidade>(entity =>
        {
            entity.ToTable("Cidades");
            entity.HasKey(city => city.Id);
            entity.Property(city => city.Id).ValueGeneratedNever();
            entity.Property(city => city.Nome).HasMaxLength(120).IsRequired();
            entity.Property(city => city.Uf).HasMaxLength(2).IsRequired();
            entity.HasIndex(city => new { city.Uf, city.Nome });
        });

        // Configura a frota e garante identificadores únicos.
        builder.Entity<Onibus>(entity =>
        {
            entity.ToTable("Onibus", table =>
            {
                table.HasCheckConstraint("CK_Onibus_Assentos", "\"Assentos\" BETWEEN 16 AND 60");
            });
            entity.HasKey(bus => bus.Id);
            entity.Property(bus => bus.Prefixo).HasMaxLength(12).IsRequired();
            entity.Property(bus => bus.Placa).HasMaxLength(7).IsRequired();
            entity.Property(bus => bus.Modelo).HasMaxLength(80).IsRequired();
            entity.Property(bus => bus.Categoria).HasMaxLength(20).IsRequired();
            entity.HasIndex(bus => bus.Prefixo).IsUnique();
            entity.HasIndex(bus => bus.Placa).IsUnique();
        });

        // Configura viagem, relacionamentos e checks invariantes.
        builder.Entity<Viagem>(entity =>
        {
            entity.ToTable("Viagens", table =>
            {
                table.HasCheckConstraint("CK_Viagens_Chegada", "\"Chegada\" > \"Partida\"");
                table.HasCheckConstraint("CK_Viagens_Preco", "\"PrecoCentavos\" > 0");
                table.HasCheckConstraint("CK_Viagens_Rota", "\"OrigemId\" <> \"DestinoId\"");
            });
            entity.HasKey(trip => trip.Id);
            entity.Property(trip => trip.Status).HasMaxLength(20).IsRequired();
            entity.HasOne(trip => trip.Onibus)
                .WithMany()
                .HasForeignKey(trip => trip.OnibusId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(trip => trip.Origem)
                .WithMany()
                .HasForeignKey(trip => trip.OrigemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(trip => trip.Destino)
                .WithMany()
                .HasForeignKey(trip => trip.DestinoId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(trip => new { trip.OrigemId, trip.DestinoId, trip.Partida });
        });

        // Configura passagem e mantém histórico de cancelamento.
        builder.Entity<Passagem>(entity =>
        {
            entity.ToTable("Passagens", table =>
            {
                table.HasCheckConstraint("CK_Passagens_Assento", "\"Assento\" BETWEEN 1 AND 60");
            });
            entity.HasKey(ticket => ticket.Id);
            entity.Property(ticket => ticket.Codigo).HasMaxLength(20).IsRequired();
            entity.Property(ticket => ticket.Passageiro).HasMaxLength(100).IsRequired();
            entity.Property(ticket => ticket.Documento).HasMaxLength(40).IsRequired();
            entity.Property(ticket => ticket.TipoDocumento).HasMaxLength(30);
            entity.Property(ticket => ticket.DataNascimento).HasColumnType("date");
            entity.Property(ticket => ticket.ResponsavelNome).HasMaxLength(100);
            entity.Property(ticket => ticket.ResponsavelDocumento).HasMaxLength(40);
            entity.Property(ticket => ticket.ResponsavelDataNascimento).HasColumnType("date");
            entity.Property(ticket => ticket.ResponsavelVinculo).HasMaxLength(40);
            entity.Property(ticket => ticket.Status).HasMaxLength(20).IsRequired();
            entity.HasIndex(ticket => ticket.Codigo).IsUnique();
            entity.HasOne(ticket => ticket.Viagem)
                .WithMany()
                .HasForeignKey(ticket => ticket.ViagemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(ticket => ticket.UsuarioId);
            entity.HasIndex(ticket => new { ticket.ViagemId, ticket.Assento })
                .IsUnique()
                .HasFilter("\"Status\" = 'Confirmada'");
        });

        // Configura auditoria para consultas cronológicas e por administrador.
        builder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("AuditEvents");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Acao).HasMaxLength(80).IsRequired();
            entity.Property(audit => audit.Entidade).HasMaxLength(80).IsRequired();
            entity.Property(audit => audit.EntidadeId).HasMaxLength(80).IsRequired();
            entity.Property(audit => audit.Detalhes).HasMaxLength(1000);
            entity.HasIndex(audit => new { audit.AdminId, audit.CriadoEm });
        });
    }
}
