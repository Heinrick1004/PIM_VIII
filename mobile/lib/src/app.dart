import 'package:flutter/material.dart';

import 'models/taskflow_models.dart';
import 'screens/login_screen.dart';
import 'screens/profile_screen.dart';
import 'screens/task_list_screen.dart';
import 'services/api_client.dart';

class TaskFlowApp extends StatefulWidget {
  const TaskFlowApp({super.key});

  @override
  State<TaskFlowApp> createState() => _TaskFlowAppState();
}

class _TaskFlowAppState extends State<TaskFlowApp> {
  final _api = ApiClient();
  TaskFlowUser? _user;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _restoreSession();
  }

  Future<void> _restoreSession() async {
    try {
      final user = await _api.currentUser();
      if (mounted) setState(() => _user = user);
    } on ApiException {
      // A ausência de uma sessão válida abre o login; detalhes são exibidos
      // nas telas após a autenticação.
    } catch (_) {
      // O app também pode abrir sem a API disponível para permitir novo login.
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _signedIn() async {
    final user = await _api.currentUser();
    if (mounted) setState(() => _user = user);
  }

  Future<void> _signedOut() async {
    await _api.logout();
    if (mounted) setState(() => _user = null);
  }

  @override
  Widget build(BuildContext context) => MaterialApp(
        title: 'TaskFlow',
        debugShowCheckedModeBanner: false,
        theme: ThemeData(
          useMaterial3: true,
          brightness: Brightness.dark,
          scaffoldBackgroundColor: const Color(0xFF10141D),
          colorScheme: ColorScheme.fromSeed(
            seedColor: const Color(0xFF6D8DFF),
            brightness: Brightness.dark,
            surface: const Color(0xFF191F2B),
          ),
          appBarTheme: const AppBarTheme(
            backgroundColor: Color(0xFF10141D),
            surfaceTintColor: Colors.transparent,
          ),
          inputDecorationTheme: InputDecorationTheme(
            filled: true,
            fillColor: const Color(0xFF191F2B),
            border: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: BorderSide.none,
            ),
          ),
          cardTheme: CardThemeData(
            color: const Color(0xFF191F2B),
            margin: EdgeInsets.zero,
            shape:
                RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
          ),
        ),
        home: _loading
            ? const _LoadingScreen()
            : _user == null
                ? LoginScreen(api: _api, onSignedIn: _signedIn)
                : _HomeShell(api: _api, user: _user!, onSignedOut: _signedOut),
      );
}

class _HomeShell extends StatefulWidget {
  const _HomeShell({
    required this.api,
    required this.user,
    required this.onSignedOut,
  });

  final ApiClient api;
  final TaskFlowUser user;
  final Future<void> Function() onSignedOut;

  @override
  State<_HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<_HomeShell> {
  int _selected = 0;

  @override
  Widget build(BuildContext context) {
    final screens = [
      TaskListScreen(
        api: widget.api,
        user: widget.user,
        onSessionExpired: widget.onSignedOut,
      ),
      ProfileScreen(user: widget.user, onLogout: widget.onSignedOut),
    ];
    return Scaffold(
      body: SafeArea(
        child: IndexedStack(index: _selected, children: screens),
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _selected,
        onDestinationSelected: (index) => setState(() => _selected = index),
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.checklist_rounded),
            label: 'Tarefas',
          ),
          NavigationDestination(
            icon: Icon(Icons.person_outline_rounded),
            label: 'Perfil',
          ),
        ],
      ),
    );
  }
}

class _LoadingScreen extends StatelessWidget {
  const _LoadingScreen();

  @override
  Widget build(BuildContext context) =>
      const Scaffold(body: Center(child: CircularProgressIndicator()));
}
