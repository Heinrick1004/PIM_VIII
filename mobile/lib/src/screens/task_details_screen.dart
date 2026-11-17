import 'package:flutter/material.dart';

import '../models/taskflow_models.dart';
import '../services/api_client.dart';

class TaskDetailsScreen extends StatefulWidget {
  const TaskDetailsScreen({
    super.key,
    required this.api,
    required this.user,
    required this.taskId,
    required this.onSessionExpired,
  });

  final ApiClient api;
  final TaskFlowUser user;
  final String taskId;
  final Future<void> Function() onSessionExpired;

  @override
  State<TaskDetailsScreen> createState() => _TaskDetailsScreenState();
}

class _TaskDetailsScreenState extends State<TaskDetailsScreen> {
  late Future<TaskFlowTask> _taskFuture;
  late Future<List<TaskHistoryEntry>> _historyFuture;
  int? _selectedStatus;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  void _load() {
    _taskFuture = widget.api.task(widget.taskId);
    _historyFuture = widget.api.history(widget.taskId);
  }

  Future<void> _refresh() async {
    setState(() {
      _selectedStatus = null;
      _load();
    });
    try {
      await Future.wait([_taskFuture, _historyFuture]);
    } catch (_) {
      // Os FutureBuilders apresentam individualmente os erros de leitura.
    }
  }

  Future<void> _update(TaskFlowTask task) async {
    final nextStatus = _selectedStatus;
    if (nextStatus == null || nextStatus == task.status || _saving) return;
    setState(() => _saving = true);
    try {
      await widget.api.updateStatus(task, nextStatus);
      if (!mounted) return;
      setState(() {
        _selectedStatus = null;
        _load();
      });
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Status da tarefa atualizado.')),
      );
    } on ApiException catch (error) {
      if (!mounted) return;
      if (error.statusCode == 401) {
        await widget.onSessionExpired();
      } else if (error.statusCode == 409) {
        await _refresh();
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text(
                'A tarefa mudou em outra sessão. Os dados foram atualizados.',
              ),
            ),
          );
        }
      } else {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(error.message)));
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  String _date(BuildContext context, DateTime? date) => date == null
      ? 'Sem prazo definido'
      : MaterialLocalizations.of(context).formatMediumDate(date.toLocal());

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          title: const Text('Detalhes da tarefa'),
          actions: [
            IconButton(
              onPressed: _refresh,
              tooltip: 'Atualizar',
              icon: const Icon(Icons.refresh_rounded),
            ),
          ],
        ),
        body: FutureBuilder<TaskFlowTask>(
          future: _taskFuture,
          builder: (context, snapshot) {
            if (snapshot.connectionState == ConnectionState.waiting) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError) {
              final error = snapshot.error;
              if (error is ApiException && error.statusCode == 401) {
                WidgetsBinding.instance.addPostFrameCallback((_) {
                  widget.onSessionExpired();
                });
              }
              return Center(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        error is ApiException
                            ? error.message
                            : 'Não foi possível abrir a tarefa.',
                      ),
                      const SizedBox(height: 12),
                      OutlinedButton(
                        onPressed: _refresh,
                        child: const Text('Tentar novamente'),
                      ),
                    ],
                  ),
                ),
              );
            }
            final task = snapshot.data!;
            final canChangeStatus =
                widget.user.profile == 1 || task.assigneeId == widget.user.id;
            return RefreshIndicator(
              onRefresh: _refresh,
              child: ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
                children: [
                  Text(
                    task.title,
                    style: Theme.of(context)
                        .textTheme
                        .headlineSmall
                        ?.copyWith(fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: 20),
                  _InfoCard(
                    children: [
                      _InfoRow(label: 'Status', value: task.statusLabel),
                      _InfoRow(label: 'Prioridade', value: task.priorityLabel),
                      _InfoRow(
                          label: 'Prazo', value: _date(context, task.dueAt)),
                      _InfoRow(
                        label: 'Criada em',
                        value: _date(context, task.createdAt),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  Text('Descrição',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 8),
                  _InfoCard(
                    children: [
                      Text(
                        task.description?.trim().isNotEmpty == true
                            ? task.description!
                            : 'Sem descrição.',
                      ),
                    ],
                  ),
                  if (canChangeStatus) ...[
                    const SizedBox(height: 20),
                    Text(
                      'Atualizar status',
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 8),
                    DropdownButtonFormField<int>(
                      initialValue: _selectedStatus ?? task.status,
                      decoration: const InputDecoration(
                        labelText: 'Novo status',
                        prefixIcon: Icon(Icons.sync_alt_rounded),
                      ),
                      items: const [
                        DropdownMenuItem(value: 1, child: Text('Pendente')),
                        DropdownMenuItem(value: 2, child: Text('Em andamento')),
                        DropdownMenuItem(value: 3, child: Text('Concluída')),
                      ],
                      onChanged: _saving
                          ? null
                          : (value) => setState(() => _selectedStatus = value),
                    ),
                    const SizedBox(height: 12),
                    FilledButton.icon(
                      onPressed: _saving ||
                              (_selectedStatus ?? task.status) == task.status
                          ? null
                          : () => _update(task),
                      icon: _saving
                          ? const SizedBox.square(
                              dimension: 18,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Icon(Icons.save_outlined),
                      label: const Text('Salvar status'),
                    ),
                  ],
                  const SizedBox(height: 26),
                  Text('Histórico',
                      style: Theme.of(context).textTheme.titleLarge),
                  const SizedBox(height: 12),
                  FutureBuilder<List<TaskHistoryEntry>>(
                    future: _historyFuture,
                    builder: (context, historySnapshot) {
                      if (historySnapshot.connectionState ==
                          ConnectionState.waiting) {
                        return const Center(
                          child: Padding(
                            padding: EdgeInsets.all(12),
                            child: CircularProgressIndicator(),
                          ),
                        );
                      }
                      if (historySnapshot.hasError) {
                        return Text(
                          historySnapshot.error is ApiException
                              ? (historySnapshot.error as ApiException).message
                              : 'Não foi possível carregar o histórico.',
                        );
                      }
                      final history = historySnapshot.data ?? [];
                      if (history.isEmpty) return const Text('Sem eventos.');
                      return _InfoCard(
                        children: [
                          for (final entry in history)
                            ListTile(
                              contentPadding: EdgeInsets.zero,
                              leading: const Icon(Icons.history_rounded),
                              title: Text(entry.description ?? entry.action),
                              subtitle: Text(
                                '${entry.action} · ${_date(context, entry.date)}',
                              ),
                            ),
                        ],
                      );
                    },
                  ),
                ],
              ),
            );
          },
        ),
      );
}

class _InfoCard extends StatelessWidget {
  const _InfoCard({required this.children});
  final List<Widget> children;

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: children,
          ),
        ),
      );
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label, required this.value});
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 8),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(
              width: 105,
              child: Text(label, style: const TextStyle(color: Colors.white60)),
            ),
            Expanded(child: Text(value)),
          ],
        ),
      );
}
