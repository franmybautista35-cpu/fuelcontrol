import 'dart:typed_data';
import 'package:flutter/material.dart';
import '../services/api_service.dart';
import 'requests_screen.dart' show fmtDate;

class TicketsScreen extends StatefulWidget {
  const TicketsScreen({super.key});
  @override
  State<TicketsScreen> createState() => _TicketsScreenState();
}

class _TicketsScreenState extends State<TicketsScreen> {
  List<dynamic> _tickets = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final t = await ApiService.instance.get('/tickets');
      if (mounted) setState(() => _tickets = t);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _showQr(String id, String sequence) {
    showDialog(
      context: context,
      builder: (_) => Dialog(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(sequence, style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 12),
              FutureBuilder<List<int>>(
                future: ApiService.instance.getBytes('/tickets/$id/qr'),
                builder: (context, snapshot) {
                  if (snapshot.connectionState != ConnectionState.done) {
                    return const SizedBox(height: 220, width: 220, child: Center(child: CircularProgressIndicator()));
                  }
                  if (snapshot.hasError || !snapshot.hasData) {
                    return SizedBox(width: 220, child: Text('No se pudo cargar el QR: ${snapshot.error}'));
                  }
                  return Image.memory(Uint8List.fromList(snapshot.data!), width: 240, height: 240);
                },
              ),
              const SizedBox(height: 12),
              TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cerrar')),
            ],
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) return Center(child: Text(_error!));
    return RefreshIndicator(
      onRefresh: _load,
      child: _tickets.isEmpty
          ? ListView(children: const [Padding(padding: EdgeInsets.all(32), child: Center(child: Text('Aún no hay tickets.')))])
          : ListView.builder(
              padding: const EdgeInsets.all(12),
              itemCount: _tickets.length,
              itemBuilder: (_, i) {
                final t = _tickets[i];
                return Card(
                  child: ListTile(
                    leading: const Icon(Icons.qr_code_2),
                    title: Text(t['sequence'] ?? ''),
                    subtitle: Text(
                        '${t['request']?['employee']?['fullName'] ?? ''} · ${t['request']?['vehicle']?['plate'] ?? ''}\nVence ${fmtDate(t['expiresAtUtc'])}'),
                    isThreeLine: true,
                    trailing: Chip(label: Text(t['status'] ?? '')),
                    onTap: () => _showQr(t['id'], t['sequence'] ?? ''),
                  ),
                );
              },
            ),
    );
  }
}
