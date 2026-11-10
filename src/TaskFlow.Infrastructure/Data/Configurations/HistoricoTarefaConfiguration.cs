using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Data.Configurations;

public sealed class HistoricoTarefaConfiguration : IEntityTypeConfiguration<HistoricoTarefa>
{
    public void Configure(EntityTypeBuilder<HistoricoTarefa> builder)
    {
        builder.ToTable("HistoricoTarefas", table =>
        {
            table.HasCheckConstraint("CK_HistoricoTarefas_Id", "[Id] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_HistoricoTarefas_Acao", "LEN(LTRIM(RTRIM([Acao]))) BETWEEN 1 AND 80");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(x => x.TarefaId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(x => x.UsuarioId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(x => x.Acao)
            .HasColumnType("nvarchar(80)")
            .HasMaxLength(HistoricoTarefa.AcaoMaxLength)
            .IsRequired();

        builder.Property(x => x.DataUtc)
            .HasColumnType("datetime2")
            .HasConversion(SqlServerUtcConverters.DateTime)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.Descricao)
            .HasColumnType("nvarchar(1000)")
            .HasMaxLength(HistoricoTarefa.DescricaoMaxLength)
            .IsRequired(false);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<Tarefa>()
            .WithMany()
            .HasForeignKey(x => x.TarefaId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
