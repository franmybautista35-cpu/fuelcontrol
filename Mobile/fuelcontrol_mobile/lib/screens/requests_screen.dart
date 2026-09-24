import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../services/api_service.dart';

// Mismo grupo que la política "Manage" del backend.
const manageRoles = ['Administrator', 'Supervisor'];

String fmtDate(String? iso) {
  if (iso == null) return '';
  try {
    return DateFormat('dd/MM/yyyy HH:mm').format(DateTime.parse(iso).toLocal());
  } catch (_) {
    return iso;
  }
}

class RequestsScreen extends StatefulWidget {
  const RequestsScreen({super.key});
  @override
  State<RequestsScreen> createState() => _RequestsScreenState();
}

class _RequestsScreenState extends State<RequestsScreen> {
  List<dynamic> _requests = [];
  bool _loading = true;
  String? _error;

  bool get _canManage => manageRoles.contains(ApiService.instance.role);

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
      final r = await ApiService.instance.get('/requests');
      if (mounted) setState(() => _requests = r);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _approve(int id) async {
    try {
      final x = await ApiService.instance.post('/requests/$id/approve');
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Ticket ${x['sequence']} emitido.')));
      _load();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.toString()), backgroundColor: Colors.red));
    }
  }

  Future<void> _openNewRequest() async {
    final created = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (_) => const _NewRequestSheet(),
    );
    if (created == true) _load();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: Text(_error!))
              : RefreshIndicator(
                  onRefresh: _load,
                  child: _requests.isEmpty
                      ? ListView(children: const [
                          Padding(padding: EdgeInsets.all(32), child: Center(child: Text('Aún no hay solicitudes.')))
                        ])
                      : ListView.builder(
                          padding: const EdgeInsets.fromLTRB(12, 12, 12, 88),
                          itemCount: _requests.length,
                          itemBuilder: (_, i) {
                            final r = _requests[i];
                            final status = (r['status'] ?? '').toString();
                            return Card(
                              child: ListTile(
                                title: Text('${r['employee']?['fullName'] ?? ''} · ${r['vehicle']?['plate'] ?? ''}'),
                                subtitle: Text('${r['authorizedGallons']} gal · Vence ${fmtDate(r['expiresAtUtc'])}'),
                                trailing: status == 'Pending' && _canManage
                                    ? FilledButton(onPressed: () => _approve(r['id']), child: const Text('Aprobar'))
                                    : Chip(label: Text(status)),
                              ),
                            );
                          },
                        ),
                ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _openNewRequest,
        icon: const Icon(Icons.add),
        label: const Text('Nueva solicitud'),
      ),
    );
  }
}

class _NewRequestSheet extends StatefulWidget {
  const _NewRequestSheet();
  @override
  State<_NewRequestSheet> createState() => _NewRequestSheetState();
}

class _NewRequestSheetState extends State<_NewRequestSheet> {
  Map<String, dynamic>? _catalogs;
  int? _employeeId, _vehicleId, _departmentId;
  String _fuelType = 'Gasolina';
  final _gallonsCtrl = TextEditingController();
  DateTime? _expires;
  final _notesCtrl = TextEditingController();
  bool _loading = true;
  bool _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadCatalogs();
  }

  Future<void> _loadCatalogs() async {
    try {
      final c = await ApiService.instance.get('/catalogs');
      if (mounted) setState(() {
        _catalogs = c;
        _loading = false;
      });
    } catch (e) {
      if (mounted) setState(() {
        _error = e.toString();
        _loading = false;
      });
    }
  }

  Future<void> _pickExpiry() async {
    final date = await showDatePicker(
      context: context,
      initialDate: DateTime.now().add(const Duration(days: 1)),
      firstDate: DateTime.now(),
      lastDate: DateTime.now().add(const Duration(days: 365)),
    );
    if (date == null || !mounted) return;
    final time = await showTimePicker(context: context, initialTime: TimeOfDay.now());
    if (time == null) return;
    setState(() => _expires = DateTime(date.year, date.month, date.day, time.hour, time.minute));
  }

  Future<void> _submit() async {
    if (_employeeId == null || _vehicleId == null || _departmentId == null || _expires == null || _gallonsCtrl.text.isEmpty) {
      setState(() => _error = 'Completa todos los campos requeridos.');
      return;
    }
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await ApiService.instance.post('/requests', {
        'employeeId': _employeeId,
        'vehicleId': _vehicleId,
        'departmentId': _departmentId,
        'authorizedGallons': double.parse(_gallonsCtrl.text),
        'fuelType': _fuelType,
        'expiresAtUtc': _expires!.toUtc().toIso8601String(),
        'isRecurring': false,
        'notes': _notesCtrl.text.isEmpty ? null : _notesCtrl.text,
      });
      if (!mounted) return;
      Navigator.of(context).pop(true);
    } catch (e) {
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom, left: 16, right: 16, top: 16),
      child: SingleChildScrollView(
        child: SafeArea(
          child: _loading
              ? const Padding(padding: EdgeInsets.all(32), child: Center(child: CircularProgressIndicator()))
              : Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text('Nueva solicitud', style: Theme.of(context).textTheme.titleLarge),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<int>(
                      decoration: const InputDecoration(labelText: 'Empleado', border: OutlineInputBorder()),
                      items: (_catalogs?['employees'] as List? ?? [])
                          .map<DropdownMenuItem<int>>((e) => DropdownMenuItem(value: e['id'], child: Text(e['fullName'] ?? '')))
                          .toList(),
                      onChanged: (v) => setState(() => _employeeId = v),
                    ),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<int>(
                      decoration: const InputDecoration(labelText: 'Vehículo', border: OutlineInputBorder()),
                      items: (_catalogs?['vehicles'] as List? ?? [])
                          .map<DropdownMenuItem<int>>((v) => DropdownMenuItem(value: v['id'], child: Text(v['plate'] ?? '')))
                          .toList(),
                      onChanged: (v) => setState(() => _vehicleId = v),
                    ),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<int>(
                      decoration: const InputDecoration(labelText: 'Departamento', border: OutlineInputBorder()),
                      items: (_catalogs?['departments'] as List? ?? [])
                          .map<DropdownMenuItem<int>>((d) => DropdownMenuItem(value: d['id'], child: Text(d['name'] ?? '')))
                          .toList(),
                      onChanged: (v) => setState(() => _departmentId = v),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: _gallonsCtrl,
                      decoration: const InputDecoration(labelText: 'Galones autorizados', border: OutlineInputBorder()),
                      keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    ),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<String>(
                      value: _fuelType,
                      decoration: const InputDecoration(labelText: 'Combustible', border: OutlineInputBorder()),
                      items: const [
                        DropdownMenuItem(value: 'Gasolina', child: Text('Gasolina')),
                        DropdownMenuItem(value: 'Gasoil', child: Text('Gasoil')),
                      ],
                      onChanged: (v) => setState(() => _fuelType = v ?? 'Gasolina'),
                    ),
                    const SizedBox(height: 12),
                    OutlinedButton.icon(
                      onPressed: _pickExpiry,
                      icon: const Icon(Icons.event),
                      label: Text(_expires == null ? 'Fecha de vencimiento' : fmtDate(_expires!.toIso8601String())),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: _notesCtrl,
                      decoration: const InputDecoration(labelText: 'Notas', border: OutlineInputBorder()),
                      maxLines: 2,
                    ),
                    const SizedBox(height: 12),
                    if (_error != null)
                      Padding(padding: const EdgeInsets.only(bottom: 8), child: Text(_error!, style: const TextStyle(color: Colors.red))),
                    FilledButton(
                      onPressed: _saving ? null : _submit,
                      child: _saving
                          ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                          : const Text('Guardar'),
                    ),
                    const SizedBox(height: 16),
                  ],
                ),
        ),
      ),
    );
  }
}
