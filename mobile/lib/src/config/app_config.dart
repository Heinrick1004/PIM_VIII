class AppConfig {
  const AppConfig._();

  static const apiBaseUrl = String.fromEnvironment(
    'TASKFLOW_API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5046',
  );
}
