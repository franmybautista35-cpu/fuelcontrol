import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import '../services/api_service.dart';

class ScannerScreen extends StatefulWidget {
  const ScannerScreen({super.key});
  @override
  State<ScannerScreen> createState() => _ScannerScreenState();
}

class _ScannerScreenState extends State<ScannerScreen> {
  final _controller = MobileScannerController(formats: [BarcodeFormat.qrCode]);
  bool _scanning = true;
  bool _validating = false;
  Map<String, dynamic>? _validated;
  String? _payload;
  String? _error;

  final _manualCtrl = TextEditingController();
  int? _tankId;
  final _gallonsCtrl = TextEditingController();
  final _stationCtrl = TextEditingController(text: 'Estación principal');
  final _notesCtrl = TextEditingController();
  List<dynamic> _tanks = [];
  bool _dispatching = false;

  @override
  void initState() {
    super.initState();
    _loadTanks();
  }

  Future<void> _loadTanks() async {
    try {
      final c = await ApiService.instance.get('/catalogs');
      if (mounted) setState(() => _tanks = c['tanks'] ?? []);
    } catch (_) {
      // Los tanques se pueden recargar manualmente reabriendo la pantalla; no es crítico aquí.
    }
  }

  void _onDetect(BarcodeCapture capture) {
    if (_validating || !_scanning) return;
    final barcodes = capture.barcodes;
    if (barcodes.isEmpty) return;
    final code = barcodes.first.rawValue;
    if (code == null || code.isEmpty) return;
    setState(() => _scanning = false);
    _validate(code);
  }

  Future<void> _validate(String payload) async {
    setState(() {
      _validating = true;
      _error = null;
    });
    try {
      final r = await ApiService.instance.post('/tickets/validate', {'qrPayload': payload});
      if (!mounted) return;
      setState(() {
        _validated = r;
        _payload = payload;
        _gallonsCtrl.text = '${r['gallons'] ?? ''}';
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.toString();
        _validated = null;
      });
    } finally {
      if (mounted) setState(() => _validating = false);
    }
  }

  Future<void> _dispatch() async {
    if (_tankId == null || _gallonsCtrl.text.isEmpty || _payload == null) {
      setState(() => _error = 'Selecciona el tanque e indica los galones servidos.');
      return;
    }
    setState(() {
      _dispatching = true;
      _error = null;
    });
    try {
      final r = await ApiService.instance.post('/dispatches', {
        'qrPayload': _payload,
        'tankId': _tankId,
        'gallonsServed': double.parse(_gallonsCtrl.text),
        'station': _stationCtrl.text,
        'notes': _notesCtrl.text.isEmpty ? null : _notesCtrl.text,
      });
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(r['message'] ?? 'Despacho registrado.')));
      _reset();
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _dispatching = false);
    }
  }

  void _reset() {
    setState(() {
      _validated = null;
      _payload = null;
      _scanning = true;
      _manualCtrl.clear();
      _tankId = null;
      _gallonsCtrl.clear();
      _notesCtrl.clear();
      _error = null;
    });
  }

  @override
  void dispose() {
    _controller.dispose();
    _manualCtrl.dispose();
    _gallonsCtrl.dispose();
    _stationCtrl.dispose();
    _notesCtrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(12),
      children: [
        if (_scanning)
          ClipRRect(
            borderRadius: BorderRadius.circular(12),
            child: SizedBox(
              height: 280,
              child: Stack(
                fit: StackFit.expand,
                children: [
                  MobileScanner(controller: _controller, onDetect: _onDetect),
                  if (_validating) const ColoredBox(color: Colors.black45, child: Center(child: CircularProgressIndicator())),
                ],
              ),
            ),
          ),
        const SizedBox(height: 12),
        if (!_scanning)
          OutlinedButton.icon(
            onPressed: _reset,
            icon: const Icon(Icons.qr_code_scanner),
            label: const Text('Escanear otro ticket'),
          ),
        const SizedBox(height: 8),
        ExpansionTile(
          title: const Text('Pegar el contenido del QR manualmente'),
          children: [
            Padding(
              padding: const EdgeInsets.all(8),
              child: Column(
                children: [
                  TextField(controller: _manualCtrl, maxLines: 3, decoration: const InputDecoration(border: OutlineInputBorder())),
                  const SizedBox(height: 8),
                  FilledButton(onPressed: () => _validate(_manualCtrl.text.trim()), child: const Text('Validar ticket')),
                ],
              ),
            ),
          ],
        ),
        const SizedBox(height: 12),
        if (_error != null)
          Card(
            color: Colors.red.shade50,
            child: Padding(padding: const EdgeInsets.all(12), child: Text(_error!, style: const TextStyle(color: Colors.red))),
          ),
        if (_validated != null)
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(_validated!['sequence'] ?? '', style: Theme.of(context).textTheme.titleMedium),
                  Text(_validated!['employee'] ?? ''),
                  Text('Vehículo: ${_validated!['vehicle'] ?? ''}'),
                  Text('${_validated!['gallons']} gal de ${_validated!['fuelType']}'),
                  const SizedBox(height: 16),
                  DropdownButtonFormField<int>(
                    decoration: const InputDecoration(labelText: 'Tanque', border: OutlineInputBorder()),
                    items: _tanks
                        .map<DropdownMenuItem<int>>((t) => DropdownMenuItem(value: t['id'], child: Text('${t['name']} · ${t['fuelType']}')))
                        .toList(),
                    onChanged: (v) => setState(() => _tankId = v),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _gallonsCtrl,
                    decoration: const InputDecoration(labelText: 'Galones servidos', border: OutlineInputBorder()),
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _stationCtrl,
                    decoration: const InputDecoration(labelText: 'Estación', border: OutlineInputBorder()),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _notesCtrl,
                    decoration: const InputDecoration(labelText: 'Observaciones', border: OutlineInputBorder()),
                    maxLines: 2,
                  ),
                  const SizedBox(height: 16),
                  FilledButton(
                    onPressed: _dispatching ? null : _dispatch,
                    child: _dispatching
                        ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
                        : const Text('Confirmar despacho'),
                  ),
                ],
              ),
            ),
          ),
      ],
    );
  }
}
