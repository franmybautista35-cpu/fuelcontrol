import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

/// Excepción con el mensaje ya listo para mostrar al usuario
/// (viene del campo "message"/"title" que devuelve la API, o del texto plano).
class ApiException implements Exception {
  final String message;
  ApiException(this.message);
  @override
  String toString() => message;
}

/// Cliente HTTP único para toda la app. Guarda la URL base del servidor,
/// el token JWT y el rol del usuario en SharedPreferences para que
/// sobrevivan a que se cierre y reabra la app.
class ApiService {
  ApiService._();
  static final ApiService instance = ApiService._();

  String baseUrl = '';
  String? token;
  String? role;
  String? fullName;

  bool get isLoggedIn => token != null && token!.isNotEmpty;

  Future<void> loadSession() async {
    final prefs = await SharedPreferences.getInstance();
    baseUrl = prefs.getString('baseUrl') ?? '';
    token = prefs.getString('token');
    role = prefs.getString('role');
    fullName = prefs.getString('fullName');
  }

  Future<void> _persist() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('baseUrl', baseUrl);
    if (token != null) await prefs.setString('token', token!);
    if (role != null) await prefs.setString('role', role!);
    if (fullName != null) await prefs.setString('fullName', fullName!);
  }

  Future<void> logout() async {
    token = null;
    role = null;
    fullName = null;
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove('token');
    await prefs.remove('role');
    await prefs.remove('fullName');
  }

  Future<void> setBaseUrl(String url) async {
    baseUrl = url.trim();
    await _persist();
  }

  Uri _uri(String path, [Map<String, String>? query]) {
    final clean = baseUrl.endsWith('/') ? baseUrl.substring(0, baseUrl.length - 1) : baseUrl;
    return Uri.parse('$clean/api$path').replace(queryParameters: query);
  }

  Map<String, String> get _headers => {
        'Content-Type': 'application/json',
        if (token != null) 'Authorization': 'Bearer $token',
      };

  Future<dynamic> _handle(http.Response r) async {
    if (r.statusCode == 204) return null;
    if (r.statusCode >= 200 && r.statusCode < 300) {
      if (r.bodyBytes.isEmpty) return null;
      return jsonDecode(utf8.decode(r.bodyBytes));
    }
    var message = 'Error ${r.statusCode}';
    try {
      final body = jsonDecode(utf8.decode(r.bodyBytes));
      if (body is Map && (body['message'] != null || body['title'] != null)) {
        message = (body['message'] ?? body['title']).toString();
      } else if (r.body.isNotEmpty) {
        message = r.body;
      }
    } catch (_) {
      if (r.body.isNotEmpty) message = r.body;
    }
    throw ApiException(message);
  }

  Future<dynamic> get(String path, [Map<String, String>? query]) async {
    final r = await http.get(_uri(path, query), headers: _headers);
    return _handle(r);
  }

  Future<dynamic> post(String path, [Map<String, dynamic>? body]) async {
    final r = await http.post(_uri(path), headers: _headers, body: body != null ? jsonEncode(body) : null);
    return _handle(r);
  }

  Future<dynamic> put(String path, [Map<String, dynamic>? body]) async {
    final r = await http.put(_uri(path), headers: _headers, body: body != null ? jsonEncode(body) : null);
    return _handle(r);
  }

  /// Para endpoints que devuelven un archivo binario (la imagen PNG del QR).
  Future<List<int>> getBytes(String path) async {
    final r = await http.get(_uri(path), headers: _headers);
    if (r.statusCode >= 200 && r.statusCode < 300) return r.bodyBytes;
    throw ApiException('Error ${r.statusCode}');
  }

  Future<void> login(String username, String password) async {
    final data = await post('/auth/login', {'username': username, 'password': password});
    token = data['token'];
    role = data['role'];
    fullName = data['fullName'];
    await _persist();
  }
}
