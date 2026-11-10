using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Perfil = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.CheckConstraint("CK_Usuarios_Email", "LEN([Email]) > 0 AND DATALENGTH([Email]) = DATALENGTH(LTRIM(RTRIM([Email])))");
                    table.CheckConstraint("CK_Usuarios_Id", "[Id] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_Usuarios_Nome", "LEN(LTRIM(RTRIM([Nome]))) > 0");
                    table.CheckConstraint("CK_Usuarios_PasswordHash", "LEN(LTRIM(RTRIM([PasswordHash]))) > 0");
                    table.CheckConstraint("CK_Usuarios_Perfil", "[Perfil] IN (1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "Tarefas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Prioridade = table.Column<byte>(type: "tinyint", nullable: false),
                    CriadaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PrazoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CriadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponsavelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Versao = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tarefas", x => x.Id);
                    table.CheckConstraint("CK_Tarefas_Id", "[Id] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_Tarefas_Prioridade", "[Prioridade] IN (1, 2, 3)");
                    table.CheckConstraint("CK_Tarefas_Status", "[Status] IN (1, 2, 3)");
                    table.CheckConstraint("CK_Tarefas_Titulo", "LEN(LTRIM(RTRIM([Titulo]))) BETWEEN 1 AND 200");
                    table.ForeignKey(
                        name: "FK_Tarefas_Usuarios_CriadorId",
                        column: x => x.CriadorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tarefas_Usuarios_ResponsavelId",
                        column: x => x.ResponsavelId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistoricoTarefas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TarefaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Acao = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DataUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricoTarefas", x => x.Id);
                    table.CheckConstraint("CK_HistoricoTarefas_Acao", "LEN(LTRIM(RTRIM([Acao]))) BETWEEN 1 AND 80");
                    table.CheckConstraint("CK_HistoricoTarefas_Id", "[Id] <> '00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "FK_HistoricoTarefas_Tarefas_TarefaId",
                        column: x => x.TarefaId,
                        principalTable: "Tarefas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricoTarefas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoTarefas_TarefaId",
                table: "HistoricoTarefas",
                column: "TarefaId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoTarefas_UsuarioId",
                table: "HistoricoTarefas",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Tarefas_CriadorId",
                table: "Tarefas",
                column: "CriadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Tarefas_ResponsavelId",
                table: "Tarefas",
                column: "ResponsavelId");

            migrationBuilder.CreateIndex(
                name: "IX_Tarefas_Status_ResponsavelId",
                table: "Tarefas",
                columns: new[] { "Status", "ResponsavelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricoTarefas");

            migrationBuilder.DropTable(
                name: "Tarefas");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
