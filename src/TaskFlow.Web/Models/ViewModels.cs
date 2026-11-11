using System.ComponentModel.DataAnnotations;
using TaskFlow.Web.Contracts;

namespace TaskFlow.Web.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Informe seu e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Informe sua senha.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public sealed class DashboardViewModel
{
    public string Name { get; init; } = "";
    public string Profile { get; init; } = "";
    public int? Total { get; init; }
    public int? Pending { get; init; }
    public int? InProgress { get; init; }
    public int? Completed { get; init; }
    public IReadOnlyList<PublicTask> RecentTasks { get; init; } = [];
    public string? Error { get; init; }
}

public sealed class TaskListViewModel
{
    public PagedTasks? Results { get; init; }
    public IReadOnlyDictionary<Guid, string> UserNames { get; init; } = new Dictionary<Guid, string>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public StatusTarefa? Status { get; init; }
    public PrioridadeTarefa? Priority { get; init; }
    public Guid? ResponsibleId { get; init; }
    public bool WithoutResponsible { get; init; }
    public string? Title { get; init; }
    public bool IsAdministrator { get; init; }
    public string? Error { get; init; }
}

public sealed class TaskDetailsViewModel
{
    public required PublicTask Task { get; init; }
    public PagedTaskHistory? History { get; init; }
    public IReadOnlyDictionary<Guid, string> UserNames { get; init; } = new Dictionary<Guid, string>();
    public int HistoryPage { get; init; }
    public bool CanChangeStatus { get; init; }
    public bool IsAdministrator { get; init; }
    public string? Error { get; init; }
}

public sealed class TaskFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o título.")]
    [StringLength(200, ErrorMessage = "O título pode ter até 200 caracteres.")]
    public string Title { get; set; } = "";

    public string? Description { get; set; }

    [EnumDataType(typeof(PrioridadeTarefa), ErrorMessage = "Selecione uma prioridade válida.")]
    public PrioridadeTarefa Priority { get; set; } = PrioridadeTarefa.Media;

    [Display(Name = "Prazo")]
    public string? DueLocal { get; set; }

    public string? DueUtcInitial { get; set; }
    public int? TimezoneOffsetMinutes { get; set; }
    public Guid? ResponsibleId { get; set; }
    public string? RowVersion { get; set; }
    public IReadOnlyList<UserOption> Employees { get; set; } = [];
}

public sealed record UserOption(Guid Id, string Name);

public sealed class UserListViewModel
{
    public PagedUsers? Results { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public string? Error { get; init; }
}

public sealed class UserFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(120, ErrorMessage = "O nome pode ter até 120 caracteres.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(254)]
    public string Email { get; set; } = "";

    [DataType(DataType.Password)]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "A senha deve ter entre 12 e 128 caracteres.")]
    public string? Password { get; set; }

    [EnumDataType(typeof(PerfilUsuario), ErrorMessage = "Selecione um perfil válido.")]
    public PerfilUsuario Profile { get; set; } = PerfilUsuario.Funcionario;
}

public static class EnumLabels
{
    public static string Status(StatusTarefa status) => status switch
    {
        StatusTarefa.Pendente => "Pendente",
        StatusTarefa.EmAndamento => "Em andamento",
        StatusTarefa.Concluida => "Concluída",
        _ => "Desconhecido"
    };

    public static string Priority(PrioridadeTarefa priority) => priority switch
    {
        PrioridadeTarefa.Baixa => "Baixa",
        PrioridadeTarefa.Media => "Média",
        PrioridadeTarefa.Alta => "Alta",
        _ => "Desconhecida"
    };

    public static string Profile(PerfilUsuario profile) => profile switch
    {
        PerfilUsuario.Administrador => "Administrador",
        PerfilUsuario.Funcionario => "Funcionário",
        _ => "Desconhecido"
    };
}
