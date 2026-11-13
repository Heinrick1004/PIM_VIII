using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Infrastructure.Data.Configurations;

public sealed class TarefaConfiguration : IEntityTypeConfiguration<Tarefa>
{
    public void Configure(EntityTypeBuilder<Tarefa> builder)
    {
        builder.ToTable("Tarefas", table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_Tarefas_Id", "[Id] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_Tarefas_Titulo", "LEN(LTRIM(RTRIM([Titulo]))) BETWEEN 1 AND 200");
            table.HasCheckConstraint("CK_Tarefas_Status", "[Status] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_Tarefas_Prioridade", "[Prioridade] IN (1, 2, 3)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(x => x.Titulo)
            .HasColumnType("nvarchar(200)")
            .HasMaxLength(Tarefa.TituloMaxLength)
            .IsRequired();

        builder.Property(x => x.Descricao)
            .HasColumnType("nvarchar(max)")
            .IsRequired(false);

        builder.Property(x => x.Status)
            .HasColumnType("tinyint")
            .HasConversion<byte>()
            .HasDefaultValue(StatusTarefa.Pendente)
            .HasSentinel((StatusTarefa)0)
            .IsRequired();

        builder.Property(x => x.Prioridade)
            .HasColumnType("tinyint")
            .HasConversion<byte>()
            .HasDefaultValue(PrioridadeTarefa.Media)
            .HasSentinel((PrioridadeTarefa)0)
            .IsRequired();

        builder.Property(x => x.CriadaEmUtc)
            .HasColumnType("datetime2")
            .HasConversion(SqlServerUtcConverters.DateTime)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.PrazoUtc)
            .HasColumnType("datetime2")
            .HasConversion(SqlServerUtcConverters.NullableDateTime)
            .IsRequired(false);

        builder.Property(x => x.CriadorId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(x => x.ResponsavelId)
            .HasColumnType("uniqueidentifier")
            .IsRequired(false);

        builder.Property(x => x.RowVersion)
            .HasField("_rowVersion")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("Versao")
            .HasColumnType("rowversion")
            .IsRequired()
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(x => x.CriadorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(x => x.ResponsavelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.Status, x.ResponsavelId });
    }
}
