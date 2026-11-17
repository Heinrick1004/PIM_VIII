import 'package:flutter_test/flutter_test.dart';
import 'package:taskflow_mobile/src/models/taskflow_models.dart';

void main() {
  group('TaskFlowTask', () {
    test('reads the API JSON contract and enum values', () {
      final task = TaskFlowTask.fromJson({
        'id': 'task-id',
        'titulo': 'Revisar entrega',
        'descricao': 'Validar o fluxo',
        'status': 2,
        'prioridade': 3,
        'criadaEmUtc': '2026-11-13T10:00:00Z',
        'prazoUtc': '2026-11-20T18:00:00Z',
        'criadorId': 'creator-id',
        'responsavelId': 'assignee-id',
        'rowVersion': 'AQIDBAUGBwg=',
      });

      expect(task.title, 'Revisar entrega');
      expect(task.statusLabel, 'Em andamento');
      expect(task.priorityLabel, 'Alta');
      expect(task.rowVersion, 'AQIDBAUGBwg=');
      expect(task.dueAt?.isUtc, isTrue);
    });
  });

  test('reads numeric enum strings and rejects unknown shapes', () {
    expect(enumNumber('3'), 3);
    expect(() => enumNumber('Alta'), throwsFormatException);
  });

  test('maps profile labels from the API enum', () {
    final user = TaskFlowUser.fromJson({
      'id': 'user-id',
      'nome': 'Pessoa Teste',
      'email': 'teste@example.com',
      'perfil': 2,
    });

    expect(user.profileLabel, 'Funcionário');
  });
}
