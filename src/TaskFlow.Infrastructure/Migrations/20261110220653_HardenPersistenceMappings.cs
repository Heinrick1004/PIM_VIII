using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenPersistenceMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Usuarios_Email",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Usuarios",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                collation: "Latin1_General_100_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(254)",
                oldMaxLength: 254);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Usuarios_Email",
                table: "Usuarios",
                sql: "LEN([Email]) > 0 AND DATALENGTH([Email]) = DATALENGTH(LTRIM(RTRIM([Email])))");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Tarefas");

            migrationBuilder.AddColumn<byte[]>(
                name: "Versao",
                table: "Tarefas",
                type: "rowversion",
                rowVersion: true,
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Usuarios_Email",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Usuarios",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(254)",
                oldMaxLength: 254,
                oldCollation: "Latin1_General_100_CI_AS");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Usuarios_Email",
                table: "Usuarios",
                sql: "LEN([Email]) > 0 AND DATALENGTH([Email]) = DATALENGTH(LTRIM(RTRIM([Email])))");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Tarefas");

            migrationBuilder.AddColumn<byte[]>(
                name: "Versao",
                table: "Tarefas",
                type: "rowversion",
                rowVersion: true,
                nullable: true);
        }
    }
}
