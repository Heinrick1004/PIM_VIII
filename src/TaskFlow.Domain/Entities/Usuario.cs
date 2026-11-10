using System.ComponentModel.DataAnnotations;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public sealed class Usuario
{
    public const int NomeMaxLength = 120;
    public const int EmailMaxLength = 254;
    public const int PasswordHashMaxLength = 512;

    public Usuario(Guid id, string nome, string email, string passwordHash, PerfilUsuario perfil)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O identificador do usuário é obrigatório.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(nome) || nome.Trim().Length > NomeMaxLength)
        {
            throw new ArgumentException($"O nome é obrigatório e deve ter até {NomeMaxLength} caracteres.", nameof(nome));
        }

        var normalizedEmail = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail)
            || normalizedEmail.Length > EmailMaxLength
            || !new EmailAddressAttribute().IsValid(normalizedEmail))
        {
            throw new ArgumentException("Informe um e-mail válido.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(passwordHash) || passwordHash.Length > PasswordHashMaxLength)
        {
            throw new ArgumentException("O hash da senha é obrigatório.", nameof(passwordHash));
        }

        if (!Enum.IsDefined(perfil))
        {
            throw new ArgumentOutOfRangeException(nameof(perfil), "Perfil de usuário inválido.");
        }

        Id = id;
        Nome = nome.Trim();
        Email = normalizedEmail;
        PasswordHash = passwordHash;
        Perfil = perfil;
    }

    public Guid Id { get; }
    public string Nome { get; private set; }
    public string Email { get; private set; }
    /// <summary>
    /// Hash opaco previamente calculado pelo serviço de identidade.
    /// O domínio não recebe a senha original nem implementa seu algoritmo de hash.
    /// A validação aqui garante apenas presença e tamanho, não a origem criptográfica.
    /// </summary>
    public string PasswordHash { get; private set; }
    public PerfilUsuario Perfil { get; private set; }

    public void UpdateProfile(string nome, string email, PerfilUsuario perfil)
    {
        var updated = new Usuario(Id, nome, email, PasswordHash, perfil);
        Nome = updated.Nome;
        Email = updated.Email;
        Perfil = updated.Perfil;
    }

    public void UpdatePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || passwordHash.Length > PasswordHashMaxLength)
        {
            throw new ArgumentException("O hash da senha é obrigatório.", nameof(passwordHash));
        }

        PasswordHash = passwordHash;
    }
}
