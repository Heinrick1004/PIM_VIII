-- Dia 1: referência técnica; ainda não é a estratégia oficial de provisionamento.
-- Preferência para EF Core migrations no Dia 2: não aplicar ambos no mesmo banco.
-- Executar apenas em um banco de desenvolvimento já selecionado explicitamente.
-- Não cria banco, não remove dados e não modifica tabelas existentes.
-- Uma segunda execução aborta para evitar aceitar silenciosamente um schema divergente.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
    THROW 50001, N'Selecione explicitamente um banco de desenvolvimento do TaskFlow.', 1;

IF @@TRANCOUNT <> 0
    THROW 50002, N'Execute o script fora de uma transação existente.', 1;

IF OBJECT_ID(N'dbo.Usuarios') IS NOT NULL
    OR OBJECT_ID(N'dbo.Tarefas') IS NOT NULL
    OR OBJECT_ID(N'dbo.HistoricoTarefas') IS NOT NULL
    OR OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL
    THROW 50003, N'Objetos do TaskFlow ou migrations já existem. Nenhuma alteração foi aplicada.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    CREATE TABLE dbo.Usuarios
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Usuarios_Id DEFAULT NEWSEQUENTIALID(),
        Nome NVARCHAR(120) NOT NULL,
        Email NVARCHAR(254) COLLATE Latin1_General_100_CI_AS NOT NULL,
        PasswordHash NVARCHAR(512) NOT NULL,
        Perfil TINYINT NOT NULL,
        CONSTRAINT PK_Usuarios PRIMARY KEY (Id),
        CONSTRAINT UQ_Usuarios_Email UNIQUE (Email),
        CONSTRAINT CK_Usuarios_Id CHECK (Id <> '00000000-0000-0000-0000-000000000000'),
        CONSTRAINT CK_Usuarios_Nome CHECK (LEN(LTRIM(RTRIM(Nome))) > 0),
        CONSTRAINT CK_Usuarios_Email CHECK
        (
            LEN(Email) > 0
            AND DATALENGTH(Email) = DATALENGTH(LTRIM(RTRIM(Email)))
        ),
        CONSTRAINT CK_Usuarios_Perfil CHECK (Perfil IN (1, 2)),
        CONSTRAINT CK_Usuarios_PasswordHash CHECK (LEN(LTRIM(RTRIM(PasswordHash))) > 0)
    );

    CREATE TABLE dbo.Tarefas
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Tarefas_Id DEFAULT NEWSEQUENTIALID(),
        Titulo NVARCHAR(200) NOT NULL,
        Descricao NVARCHAR(MAX) NULL,
        Status TINYINT NOT NULL CONSTRAINT DF_Tarefas_Status DEFAULT (1),
        Prioridade TINYINT NOT NULL CONSTRAINT DF_Tarefas_Prioridade DEFAULT (2),
        CriadaEmUtc DATETIME2(7) NOT NULL CONSTRAINT DF_Tarefas_CriadaEmUtc DEFAULT SYSUTCDATETIME(),
        PrazoUtc DATETIME2(7) NULL,
        CriadorId UNIQUEIDENTIFIER NOT NULL,
        ResponsavelId UNIQUEIDENTIFIER NULL,
        Versao ROWVERSION NOT NULL,
        CONSTRAINT PK_Tarefas PRIMARY KEY (Id),
        CONSTRAINT FK_Tarefas_Criador FOREIGN KEY (CriadorId) REFERENCES dbo.Usuarios (Id),
        CONSTRAINT FK_Tarefas_Responsavel FOREIGN KEY (ResponsavelId) REFERENCES dbo.Usuarios (Id),
        CONSTRAINT CK_Tarefas_Id CHECK (Id <> '00000000-0000-0000-0000-000000000000'),
        CONSTRAINT CK_Tarefas_Titulo CHECK (LEN(LTRIM(RTRIM(Titulo))) BETWEEN 1 AND 200),
        CONSTRAINT CK_Tarefas_Status CHECK (Status IN (1, 2, 3)),
        CONSTRAINT CK_Tarefas_Prioridade CHECK (Prioridade IN (1, 2, 3))
    );

    CREATE TABLE dbo.HistoricoTarefas
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_HistoricoTarefas_Id DEFAULT NEWSEQUENTIALID(),
        TarefaId UNIQUEIDENTIFIER NOT NULL,
        UsuarioId UNIQUEIDENTIFIER NOT NULL,
        Acao NVARCHAR(80) NOT NULL,
        DataUtc DATETIME2(7) NOT NULL CONSTRAINT DF_HistoricoTarefas_DataUtc DEFAULT SYSUTCDATETIME(),
        Descricao NVARCHAR(1000) NULL,
        CONSTRAINT PK_HistoricoTarefas PRIMARY KEY (Id),
        CONSTRAINT FK_HistoricoTarefas_Tarefa FOREIGN KEY (TarefaId) REFERENCES dbo.Tarefas (Id),
        CONSTRAINT FK_HistoricoTarefas_Usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuarios (Id),
        CONSTRAINT CK_HistoricoTarefas_Id CHECK (Id <> '00000000-0000-0000-0000-000000000000'),
        CONSTRAINT CK_HistoricoTarefas_Acao CHECK (LEN(LTRIM(RTRIM(Acao))) BETWEEN 1 AND 80)
    );

    CREATE INDEX IX_Tarefas_Status_ResponsavelId
        ON dbo.Tarefas (Status, ResponsavelId);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
