import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../services/api_service.dart';

class DashboardScreen extends StatefulWidget {
  const DashboardScreen({super.key});
  @override
  State<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> {
  Map<String, dynamic>? _data;
  String? _error;
  final _fmt = NumberFormat.decimalPattern('es');

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _error = null);
    try {
      final data = await ApiService.instance.get('/dashboard');
      if (mounted) setState(() => _data = data);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Text(_error!, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            OutlinedButton(onPressed: _load, child: const Text('Reintentar')),
          ]),
        ),
      );
    }
    if (_data == null) return const Center(child: CircularProgressIndicator());
    final d = _data!;
    final metrics = [
      ['Inventario actual', '${_fmt.format(d['inventory'] ?? 0)} gal'],
      ['Despachado hoy', '${_fmt.format(d['dispatchedToday'] ?? 0)} gal'],
      ['Tickets activos', '${d['activeTickets'] ?? 0}'],
      ['Tanques críticos', '${d['lowTanks'] ?? 0}'],
    ];
    final recent = (d['recent'] as List?) ?? [];
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          GridView.count(
            crossAxisCount: 2,
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            mainAxisSpacing: 12,
            crossAxisSpacing: 12,
            childAspectRatio: 1.6,
            children: metrics
                .map((m) => Card(
                      child: Padding(
                        padding: const EdgeInsets.all(12),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Text(m[0], style: Theme.of(context).textTheme.bodySmall),
                            const SizedBox(height: 6),
                            Text(m[1], style: Theme.of(context).textTheme.titleLarge),
                          ],
                        ),
                      ),
                    ))
                .toList(),
          ),
          const SizedBox(height: 20),
          Text('Despachos recientes', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          if (recent.isEmpty) const Padding(padding: EdgeInsets.all(16), child: Text('Aún no hay consumos.')),
          ...recent.map((r) => Card(
                child: ListTile(
                  leading: const Icon(Icons.local_gas_station),
                  title: Text(r['ticket'] ?? ''),
                  subtitle: Text('${r['plate'] ?? ''} · ${_fmt.format(r['gallonsServed'] ?? 0)} gal'),
                ),
              )),
        ],
      ),
    );
  }
}
