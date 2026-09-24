import 'package:flutter/material.dart';
import '../services/api_service.dart';
import 'home_shell.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});
  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  late final TextEditingController _urlCtrl = TextEditingController(text: ApiService.instance.baseUrl);
  final _userCtrl = TextEditingController(text: 'admin');
  final _passCtrl = TextEditingController(text: 'Admin123!');
  bool _loading = false;
  String? _error;

  Future<void> _submit() async {
    if (_urlCtrl.text.trim().isEmpty) {
      setState(() => _error = 'Ingresa la URL del servidor (la misma que usas en el navegador).');
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      await ApiService.instance.setBaseUrl(_urlCtrl.text);
      await ApiService.instance.login(_userCtrl.text.trim(), _passCtrl.text);
      if (!mounted) return;
      Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => const HomeShell()));
    } catch (e) {
      setState(() => _error = 'No se pudo iniciar sesión: $e');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 380),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Icon(Icons.local_gas_station, size: 56, color: Color(0xFF123A66)),
                  const SizedBox(height: 8),
                  Text('FuelControl', style: Theme.of(context).textTheme.headlineMedium, textAlign: TextAlign.center),
                  const Text('Tickets digitales e inventario de combustible', textAlign: TextAlign.center),
                  const SizedBox(height: 24),
                  TextField(
                    controller: _urlCtrl,
                    decoration: const InputDecoration(
                      labelText: 'URL del servidor',
                      hintText: 'https://xxxx.devtunnels.ms',
                      helperText: 'La misma que usas en el navegador para entrar a FuelControl',
                      border: OutlineInputBorder(),
                    ),
                    keyboardType: TextInputType.url,
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _userCtrl,
                    decoration: const InputDecoration(labelText: 'Usuario', border: OutlineInputBorder()),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _passCtrl,
                    decoration: const InputDecoration(labelText: 'Contraseña', border: OutlineInputBorder()),
                    obscureText: true,
                    onSubmitted: (_) => _submit(),
                  ),
                  const SizedBox(height: 16),
                  if (_error != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: Text(_error!, style: const TextStyle(color: Colors.red)),
                    ),
                  FilledButton(
                    onPressed: _loading ? null : _submit,
                    child: _loading
                        ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                        : const Text('Iniciar sesión'),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
