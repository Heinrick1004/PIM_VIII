using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Data.Configurations;

public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios", table =>
        {
            table.HasCheckConstraint("CK_Usuarios_Id", "[Id] <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("CK_Usuarios_Nome", "LEN(LTRIM(RTRIM([Nome]))) > 0");
            table.HasCheckConstraint("CK_Usuarios_Email", "LEN([Email]) > 0 AND DATALENGTH([Email]) = DATALENGTH(LTRIM(RTRIM([Email])))");
            table.HasCheckConstraint("CK_Usuarios_PasswordHash", "LEN(LTRIM(RTRIM([PasswordHash]))) > 0");
            table.HasCheckConstraint("CK_Usuarios_Perfil", "[Perfil] IN (1, 2)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(x => x.Nome)
            .HasColumnType("nvarchar(120)")
            .HasMaxLength(Usuario.NomeMaxLength)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasColumnType("nvarchar(254)")
            .HasMaxLength(Usuario.EmailMaxLength)
            .UseCollation("Latin1_General_100_CI_AS")
            .IsRequired();

        builder.HasIndex(x => x.Email)
            .IsUnique();

        builder.Property(x => x.PasswordHash)
            .HasColumnType("nvarchar(512)")
            .HasMaxLength(Usuario.PasswordHashMaxLength)
            .IsRequired();

        builder.Property(x => x.Perfil)
            .HasColumnType("tinyint")
            .HasConversion<byte>()
            .IsRequired();
    }
}
