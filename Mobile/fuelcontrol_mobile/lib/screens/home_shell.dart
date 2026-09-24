import 'package:flutter/material.dart';
import '../services/api_service.dart';
import 'login_screen.dart';
import 'dashboard_screen.dart';
import 'requests_screen.dart';
import 'tickets_screen.dart';
import 'scanner_screen.dart';

// Mismos grupos de roles que las políticas del backend (Program.cs):
// "Dispatch" = Administrator, Supervisor, Dispatcher.
const dispatchRoles = ['Administrator', 'Supervisor', 'Dispatcher'];

class HomeShell extends StatefulWidget {
  const HomeShell({super.key});
  @override
  State<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<HomeShell> {
  int _index = 0;

  bool get _canDispatch => dispatchRoles.contains(ApiService.instance.role);

  List<_Tab> get _tabs => [
        _Tab('Resumen', Icons.dashboard_outlined, const DashboardScreen()),
        _Tab('Solicitudes', Icons.assignment_outlined, const RequestsScreen()),
        _Tab('Tickets', Icons.qr_code_2, const TicketsScreen()),
        if (_canDispatch) _Tab('Despacho', Icons.local_shipping_outlined, const ScannerScreen()),
      ];

  Future<void> _logout() async {
    await ApiService.instance.logout();
    if (!mounted) return;
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const LoginScreen()),
      (route) => false,
    );
  }

  @override
  Widget build(BuildContext context) {
    final tabs = _tabs;
    final index = _index >= tabs.length ? 0 : _index;
    return Scaffold(
      appBar: AppBar(
        title: Text(tabs[index].title),
        actions: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 4),
            child: Center(child: Chip(label: Text(ApiService.instance.role ?? ''), visualDensity: VisualDensity.compact)),
          ),
          IconButton(onPressed: _logout, icon: const Icon(Icons.logout), tooltip: 'Cerrar sesión'),
        ],
      ),
      body: tabs[index].screen,
      bottomNavigationBar: NavigationBar(
        selectedIndex: index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: tabs.map((t) => NavigationDestination(icon: Icon(t.icon), label: t.title)).toList(),
      ),
    );
  }
}

class _Tab {
  final String title;
  final IconData icon;
  final Widget screen;
  _Tab(this.title, this.icon, this.screen);
}
