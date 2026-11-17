import 'package:flutter/material.dart';

import '../models/taskflow_models.dart';

class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key, required this.user, required this.onLogout});

  final TaskFlowUser user;
  final Future<void> Function() onLogout;

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Meu perfil')),
        body: ListView(
          padding: const EdgeInsets.all(20),
          children: [
            const SizedBox(height: 24),
            CircleAvatar(
              radius: 42,
              backgroundColor: const Color(0xFF28365D),
              child: Text(
                user.name.isEmpty
                    ? '?'
                    : String.fromCharCode(user.name.runes.first).toUpperCase(),
                style: const TextStyle(fontSize: 30, color: Color(0xFFB7C5FF)),
              ),
            ),
            const SizedBox(height: 16),
            Text(
              user.name,
              textAlign: TextAlign.center,
              style: Theme.of(context)
                  .textTheme
                  .titleLarge
                  ?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 28),
            Card(
              child: Column(
                children: [
                  ListTile(
                    leading: const Icon(Icons.person_outline),
                    title: const Text('Nome'),
                    subtitle: Text(user.name),
                  ),
                  const Divider(height: 1, indent: 56),
                  ListTile(
                    leading: const Icon(Icons.email_outlined),
                    title: const Text('E-mail'),
                    subtitle: Text(user.email),
                  ),
                  const Divider(height: 1, indent: 56),
                  ListTile(
                    leading: const Icon(Icons.badge_outlined),
                    title: const Text('Perfil'),
                    subtitle: Text(user.profileLabel),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),
            OutlinedButton.icon(
              onPressed: () async {
                await onLogout();
              },
              icon: const Icon(Icons.logout_rounded),
              label: const Text('Sair da conta'),
              style: OutlinedButton.styleFrom(
                foregroundColor: const Color(0xFFFFA0A0),
                padding: const EdgeInsets.all(14),
              ),
            ),
          ],
        ),
      );
}
