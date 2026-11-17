import 'package:flutter/material.dart';

import '../models/taskflow_models.dart';
import '../services/api_client.dart';
import 'task_details_screen.dart';

class TaskListScreen extends StatefulWidget {
  const TaskListScreen({
    super.key,
    required this.api,
    required this.user,
    required this.onSessionExpired,
  });

  final ApiClient api;
  final TaskFlowUser user;
  final Future<void> Function() onSessionExpired;

  @override
  State<TaskListScreen> createState() => _TaskListScreenState();
}

class _TaskListScreenState extends State<TaskListScreen> {
  late Future<List<TaskFlowTask>> _tasks;

  @override
  void initState() {
    super.initState();
    _tasks = widget.api.tasks();
  }

  Future<void> _refresh() async {
    final next = widget.api.tasks();
    setState(() => _tasks = next);
    try {
      await next;
    } catch (_) {
      // O FutureBuilder apresenta o erro no estado da lista.
    }
  }

  Future<void> _open(TaskFlowTask task) async {
    await Navigator.of(context).push<void>(
      MaterialPageRoute(
        builder: (_) => TaskDetailsScreen(
          api: widget.api,
          user: widget.user,
          taskId: task.id,
          onSessionExpired: widget.onSessionExpired,
        ),
      ),
    );
    if (mounted) await _refresh();
  }

  String _date(BuildContext context, DateTime? value) => value == null
      ? 'Sem prazo'
      : MaterialLocalizations.of(context).formatMediumDate(value.toLocal());

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          title: const Text('Minhas tarefas'),
          actions: [
            IconButton(
              tooltip: 'Atualizar',
              onPressed: _refresh,
              icon: const Icon(Icons.refresh_rounded),
            ),
          ],
        ),
        body: RefreshIndicator(
          onRefresh: _refresh,
          child: FutureBuilder<List<TaskFlowTask>>(
            future: _tasks,
            builder: (context, snapshot) {
              if (snapshot.connectionState == ConnectionState.waiting) {
                return const Center(child: CircularProgressIndicator());
              }
              if (snapshot.hasError) {
                if (snapshot.error case ApiException(:final statusCode)) {
                  if (statusCode == 401) {
                    WidgetsBinding.instance.addPostFrameCallback((_) {
                      widget.onSessionExpired();
                    });
                  }
                }
                return _MessageState(
                  icon: Icons.cloud_off_outlined,
                  message: snapshot.error is ApiException
                      ? (snapshot.error as ApiException).message
                      : 'Não foi possível carregar as tarefas.',
                  action: FilledButton.tonal(
                    onPressed: _refresh,
                    child: const Text('Tentar novamente'),
                  ),
                );
              }
              final tasks = snapshot.data ?? const <TaskFlowTask>[];
              if (tasks.isEmpty) {
                return const _MessageState(
                  icon: Icons.inbox_outlined,
                  message: 'Nenhuma tarefa disponível.',
                );
              }
              return ListView.separated(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
                itemCount: tasks.length,
                separatorBuilder: (_, __) => const SizedBox(height: 12),
                itemBuilder: (context, index) {
                  final task = tasks[index];
                  return Card(
                    child: InkWell(
                      borderRadius: BorderRadius.circular(16),
                      onTap: () => _open(task),
                      child: Padding(
                        padding: const EdgeInsets.all(16),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Expanded(
                                  child: Text(
                                    task.title,
                                    style: Theme.of(context)
                                        .textTheme
                                        .titleMedium
                                        ?.copyWith(fontWeight: FontWeight.w600),
                                  ),
                                ),
                                const SizedBox(width: 10),
                                _StatusBadge(status: task.statusLabel),
                              ],
                            ),
                            const SizedBox(height: 14),
                            Row(
                              children: [
                                const Icon(
                                  Icons.flag_outlined,
                                  size: 16,
                                  color: Colors.white60,
                                ),
                                const SizedBox(width: 6),
                                Text(
                                  task.priorityLabel,
                                  style: const TextStyle(color: Colors.white70),
                                ),
                                const Spacer(),
                                const Icon(
                                  Icons.event_outlined,
                                  size: 16,
                                  color: Colors.white60,
                                ),
                                const SizedBox(width: 6),
                                Text(
                                  _date(context, task.dueAt),
                                  style: const TextStyle(color: Colors.white70),
                                ),
                              ],
                            ),
                          ],
                        ),
                      ),
                    ),
                  );
                },
              );
            },
          ),
        ),
      );
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.status});
  final String status;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
        decoration: BoxDecoration(
          color: const Color(0xFF6D8DFF).withValues(alpha: 0.16),
          borderRadius: BorderRadius.circular(30),
        ),
        child: Text(
          status,
          style: const TextStyle(
            color: Color(0xFFB7C5FF),
            fontSize: 12,
            fontWeight: FontWeight.w600,
          ),
        ),
      );
}

class _MessageState extends StatelessWidget {
  const _MessageState({required this.icon, required this.message, this.action});
  final IconData icon;
  final String message;
  final Widget? action;

  @override
  Widget build(BuildContext context) => ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        children: [
          SizedBox(
            height: MediaQuery.sizeOf(context).height * 0.62,
            child: Center(
              child: Padding(
                padding: const EdgeInsets.all(30),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(icon, size: 42, color: Colors.white54),
                    const SizedBox(height: 12),
                    Text(message, textAlign: TextAlign.center),
                    if (action != null) ...[
                      const SizedBox(height: 16),
                      action!
                    ],
                  ],
                ),
              ),
            ),
          ),
        ],
      );
}
