import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

import '../config/app_config.dart';
import '../models/taskflow_models.dart';
import 'token_store.dart';

class ApiException implements Exception {
  const ApiException(this.statusCode, this.message);
  final int statusCode;
  final String message;

  @override
  String toString() => message;
}

class ApiClient {
  ApiClient({http.Client? client, TokenStore? tokenStore})
      : _client = client ?? http.Client(),
        _tokenStore = tokenStore ?? TokenStore();

  final http.Client _client;
  final TokenStore _tokenStore;
  final String baseUrl = AppConfig.apiBaseUrl;
  static const _requestTimeout = Duration(seconds: 15);

  Future<Map<String, dynamic>> _request(
    String method,
    String path, {
    Object? body,
    bool authenticated = true,
  }) async {
    final token = authenticated ? await _tokenStore.read() : null;
    final headers = <String, String>{'Accept': 'application/json'};
    if (body != null) headers['Content-Type'] = 'application/json';
    if (token != null) headers['Authorization'] = 'Bearer $token';
    late final Uri uri;
    try {
      uri = Uri.parse('$baseUrl$path');
    } on FormatException {
      throw const ApiException(
          0, 'O endere\u00e7o configurado da API \u00e9 inv\u00e1lido.');
    }
    late final http.Response response;
    try {
      switch (method) {
        case 'GET':
          response =
              await _client.get(uri, headers: headers).timeout(_requestTimeout);
        case 'POST':
          response = await _client
              .post(
                uri,
                headers: headers,
                body: jsonEncode(body),
              )
              .timeout(_requestTimeout);
        case 'PATCH':
          response = await _client
              .patch(
                uri,
                headers: headers,
                body: jsonEncode(body),
              )
              .timeout(_requestTimeout);
        default:
          throw StateError('M\u00e9todo HTTP n\u00e3o suportado.');
      }
    } on TimeoutException {
      throw const ApiException(
          0, 'A API demorou para responder. Tente novamente.');
    } on ApiException {
      rethrow;
    } catch (error) {
      throw ApiException(0, _connectionErrorMessage(error));
    }
    if (response.statusCode == 401 && authenticated) {
      await _tokenStore.clear();
    }
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ApiException(response.statusCode, _errorMessage(response));
    }
    if (response.body.isEmpty) return <String, dynamic>{};
    final decoded = jsonDecode(utf8.decode(response.bodyBytes));
    if (decoded is! Map<String, dynamic>) {
      throw const FormatException('Resposta inesperada da API TaskFlow.');
    }
    return decoded;
  }

  String _connectionErrorMessage(Object error) {
    final message = error.toString().toLowerCase();
    if (message.contains('failed host lookup') ||
        message.contains('unknown host') ||
        message.contains('no address associated with hostname')) {
      return 'N\u00e3o foi poss\u00edvel localizar o endere\u00e7o da API. Confira TASKFLOW_API_BASE_URL e a rede.';
    }
    if (message.contains('connection refused') ||
        message.contains('actively refused') ||
        message.contains('network is unreachable') ||
        message.contains('no route to host')) {
      return 'A API est\u00e1 indispon\u00edvel nesse endere\u00e7o ou porta. Confirme se ela est\u00e1 em execu\u00e7\u00e3o.';
    }
    return 'Falha de rede ao conectar \u00e0 API TaskFlow. Verifique a conex\u00e3o e o endere\u00e7o configurado.';
  }

  String _errorMessage(http.Response response) {
    try {
      final body = jsonDecode(utf8.decode(response.bodyBytes));
      if (body is Map<String, dynamic>) {
        final detail = body['detail'] ?? body['title'] ?? body['error'];
        if (detail is String && detail.isNotEmpty) return detail;
      }
    } catch (_) {
      // Respostas não JSON são comunicadas pelo status HTTP abaixo.
    }
    return switch (response.statusCode) {
      401 => 'Sessão expirada. Entre novamente.',
      403 => 'Você não tem permissão para esta ação.',
      404 => 'O item solicitado não foi encontrado.',
      409 => 'A tarefa foi alterada. Atualize os dados e tente novamente.',
      429 => 'Muitas tentativas de login. Aguarde um minuto e tente novamente.',
      _ => 'A API retornou HTTP ${response.statusCode}.',
    };
  }

  Future<String> login(String email, String password) async {
    final response = await _request(
      'POST',
      '/api/auth/login',
      authenticated: false,
      body: {'email': email, 'senha': password},
    );
    final token = response['accessToken'];
    if (token is! String || token.isEmpty) {
      throw const FormatException('A API não retornou um token de acesso.');
    }
    await _tokenStore.write(token);
    return token;
  }

  Future<TaskFlowUser> currentUser() async =>
      TaskFlowUser.fromJson(await _request('GET', '/api/auth/me'));

  Future<List<TaskFlowTask>> tasks() async {
    final response = await _request('GET', '/api/tasks?page=1&pageSize=100');
    final items = response['items'];
    if (items is! List) {
      throw const FormatException('Lista de tarefas inválida.');
    }
    return items
        .map((item) => TaskFlowTask.fromJson(item as Map<String, dynamic>))
        .toList();
  }

  Future<TaskFlowTask> task(String id) async =>
      TaskFlowTask.fromJson(await _request('GET', '/api/tasks/$id'));

  Future<List<TaskHistoryEntry>> history(String id) async {
    final response = await _request(
      'GET',
      '/api/tasks/$id/history?page=1&pageSize=20',
    );
    final items = response['items'];
    if (items is! List) throw const FormatException('Histórico inválido.');
    return items
        .map((item) => TaskHistoryEntry.fromJson(item as Map<String, dynamic>))
        .toList();
  }

  Future<TaskFlowTask> updateStatus(TaskFlowTask task, int status) async =>
      TaskFlowTask.fromJson(
        await _request(
          'PATCH',
          '/api/tasks/${task.id}/status',
          body: {'status': status, 'rowVersion': task.rowVersion},
        ),
      );

  Future<void> logout() => _tokenStore.clear();
}
