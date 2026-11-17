class TaskFlowUser {
  const TaskFlowUser({
    required this.id,
    required this.name,
    required this.email,
    required this.profile,
  });

  final String id;
  final String name;
  final String email;
  final int profile;

  factory TaskFlowUser.fromJson(Map<String, dynamic> json) => TaskFlowUser(
        id: json['id'] as String,
        name: json['nome'] as String,
        email: json['email'] as String,
        profile: enumNumber(json['perfil']),
      );

  String get profileLabel => switch (profile) {
        1 => 'Administrador',
        2 => 'Funcionário',
        _ => 'Perfil $profile',
      };
}

class TaskFlowTask {
  const TaskFlowTask({
    required this.id,
    required this.title,
    required this.description,
    required this.status,
    required this.priority,
    required this.createdAt,
    required this.dueAt,
    required this.creatorId,
    required this.assigneeId,
    required this.rowVersion,
  });

  final String id;
  final String title;
  final String? description;
  final int status;
  final int priority;
  final DateTime createdAt;
  final DateTime? dueAt;
  final String creatorId;
  final String? assigneeId;
  final String rowVersion;

  factory TaskFlowTask.fromJson(Map<String, dynamic> json) => TaskFlowTask(
        id: json['id'] as String,
        title: json['titulo'] as String,
        description: json['descricao'] as String?,
        status: enumNumber(json['status']),
        priority: enumNumber(json['prioridade']),
        createdAt: DateTime.parse(json['criadaEmUtc'] as String),
        dueAt: json['prazoUtc'] == null
            ? null
            : DateTime.parse(json['prazoUtc'] as String),
        creatorId: json['criadorId'] as String,
        assigneeId: json['responsavelId'] as String?,
        rowVersion: json['rowVersion'] as String,
      );

  String get statusLabel => switch (status) {
        1 => 'Pendente',
        2 => 'Em andamento',
        3 => 'Concluída',
        _ => 'Status $status',
      };

  String get priorityLabel => switch (priority) {
        1 => 'Baixa',
        2 => 'Média',
        3 => 'Alta',
        _ => 'Prioridade $priority',
      };
}

class TaskHistoryEntry {
  const TaskHistoryEntry({
    required this.action,
    required this.date,
    required this.description,
  });

  final String action;
  final DateTime date;
  final String? description;

  factory TaskHistoryEntry.fromJson(Map<String, dynamic> json) =>
      TaskHistoryEntry(
        action: json['acao'] as String,
        date: DateTime.parse(json['dataUtc'] as String),
        description: json['descricao'] as String?,
      );
}

int enumNumber(dynamic value) {
  if (value is int) return value;
  if (value is String) {
    final parsed = int.tryParse(value);
    if (parsed != null) return parsed;
  }
  throw const FormatException('Enum inesperado recebido da API.');
}
