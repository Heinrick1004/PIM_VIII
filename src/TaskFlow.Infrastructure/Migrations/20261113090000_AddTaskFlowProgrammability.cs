using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskFlow.Infrastructure.Data;

namespace TaskFlow.Infrastructure.Migrations;

[DbContext(typeof(TaskFlowDbContext))]
[Migration("20261113090000_AddTaskFlowProgrammability")]
public sealed class AddTaskFlowProgrammability : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("CREATE FUNCTION dbo.fn_TarefasVisiveis (@UsuarioId UNIQUEIDENTIFIER) RETURNS TABLE AS RETURN (SELECT t.Id FROM dbo.Tarefas t WHERE @UsuarioId <> '00000000-0000-0000-0000-000000000000' AND (t.CriadorId=@UsuarioId OR t.ResponsavelId=@UsuarioId));", suppressTransaction: true);
        m.Sql("CREATE PROCEDURE dbo.usp_TarefasResumo @UsuarioId UNIQUEIDENTIFIER, @EhAdministrador BIT AS BEGIN SET NOCOUNT ON; SELECT COUNT(*) Total, COALESCE(SUM(CASE WHEN t.Status=1 THEN 1 ELSE 0 END),0) Pending, COALESCE(SUM(CASE WHEN t.Status=2 THEN 1 ELSE 0 END),0) InProgress, COALESCE(SUM(CASE WHEN t.Status=3 THEN 1 ELSE 0 END),0) Completed FROM dbo.Tarefas t WHERE @EhAdministrador=1 OR t.Id IN (SELECT Id FROM dbo.fn_TarefasVisiveis(@UsuarioId)); END;", suppressTransaction: true);
        m.Sql("CREATE TRIGGER dbo.TR_Tarefas_ProtegeDadosCriacao ON dbo.Tarefas AFTER UPDATE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.Id=d.Id WHERE i.CriadorId<>d.CriadorId OR i.CriadaEmUtc<>d.CriadaEmUtc) THROW 51001, 'TaskFlow: CriadorId e CriadaEmUtc são imutáveis.', 1; END;", suppressTransaction: true);
    }
    protected override void Down(MigrationBuilder m)
    {
        m.Sql("DROP TRIGGER dbo.TR_Tarefas_ProtegeDadosCriacao;");
        m.Sql("DROP PROCEDURE dbo.usp_TarefasResumo;");
        m.Sql("DROP FUNCTION dbo.fn_TarefasVisiveis;");
    }
}
