import 'package:flutter/material.dart';
import 'services/api_service.dart';
import 'screens/login_screen.dart';
import 'screens/home_shell.dart';

void main() {
  runApp(const FuelControlApp());
}

class FuelControlApp extends StatefulWidget {
  const FuelControlApp({super.key});
  @override
  State<FuelControlApp> createState() => _FuelControlAppState();
}

class _FuelControlAppState extends State<FuelControlApp> {
  bool _ready = false;

  @override
  void initState() {
    super.initState();
    // Restaura la sesión guardada (URL del servidor, token y rol) antes de
    // decidir si mostrar el login o entrar directo a la app.
    ApiService.instance.loadSession().then((_) => setState(() => _ready = true));
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'FuelControl',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorSchemeSeed: const Color(0xFF123A66),
        useMaterial3: true,
      ),
      home: !_ready
          ? const Scaffold(body: Center(child: CircularProgressIndicator()))
          : (ApiService.instance.isLoggedIn ? const HomeShell() : const LoginScreen()),
    );
  }
}
