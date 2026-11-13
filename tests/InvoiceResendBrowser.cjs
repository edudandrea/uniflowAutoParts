// Local visual check with disposable HTTP fixtures; never transmits to SEFAZ.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const http = require('node:http');
const { spawn } = require('node:child_process');
const pause = ms => new Promise(r => setTimeout(r, ms));
const root = path.resolve(__dirname, '../front/dist/uniflow-auto-parts/browser');
const output = process.env.NFE_SCREENSHOT_DIR || os.tmpdir();
const invoice = { id: 7, numero: '1', serie: '1', emissao: '2026-11-13T14:14:00Z', cliente: 'Mecanica Cecconi Ltda – EPP', documentoCliente: '88657788000154', tipo: 'NFe', situacao: 'Rejeitada', valor: 83.71, chaveAcesso: '43261112345678000195550010000000011123456780', ambiente: 1, temPdf: false, podeCancelar: false, podeCorrigir: false, bloqueioEventos: 'Nota sem autorização', podeReenviar: true, codigoAutorizacao: '203', motivoAutorizacao: 'Emissor não habilitado para emissão da NF-e', protocolo: '' };
const session = { authenticated: true, available: true, portalUrl: null, empresa: { id: 1, nome: 'AutoParts', razaoSocial: 'AutoParts', cnpj: '12345678000195', hubTenantId: 'visual-1' }, usuario: { id: '1', sujeito: 'visual-1', nome: 'Usuário de teste', email: 'visual@example.test', perfil: 'Operador' } };
const certificate = { fileName: 'fixture.pfx', base64: 'browser-fixture-only', password: 'fixture-only', uf: 'RS', environment: 'producao' };
let resends = 0;
const list = { notas: [invoice], total: 1, pagina: 1, tamanhoPagina: 10, resumo: { emitidas: 1, autorizadas: 0, processando: 0, canceladas: 0, valorTotal: 0 } };
const fixtures = { '/api/auth/session': session, '/api/notas-fiscais': list };
const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  if (url.pathname.startsWith('/api/')) {
    if (req.method === 'POST' && url.pathname === '/api/notas-fiscais/7/autorizacao/reenviar') {
      resends++; invoice.situacao = 'Autorizada'; invoice.codigoAutorizacao = '100'; invoice.motivoAutorizacao = 'Autorizado o uso da NF-e'; invoice.protocolo = '143260000000001'; invoice.podeReenviar = false; list.resumo.autorizadas = 1; list.resumo.valorTotal = 83.71;
      const result = { id: 7, numero: invoice.numero, serie: invoice.serie, chaveAcesso: invoice.chaveAcesso, ambiente: 1, situacao: invoice.situacao, codigo: invoice.codigoAutorizacao, motivo: invoice.motivoAutorizacao, protocolo: invoice.protocolo, podeReenviar: false };
      res.writeHead(200, { 'Content-Type': 'application/json' }).end(JSON.stringify(result)); return;
    }
    if (req.method !== 'GET' || !fixtures[url.pathname]) { res.writeHead(404).end(); return; }
    res.writeHead(200, { 'Content-Type': 'application/json' }).end(JSON.stringify(fixtures[url.pathname])); return;
  }
  let file = path.resolve(root, '.' + decodeURIComponent(url.pathname));
  if (!file.startsWith(root + path.sep)) file = path.join(root, 'index.html');
  if (!fs.existsSync(file) || !fs.statSync(file).isFile()) file = path.join(root, 'index.html');
  const type = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.png': 'image/png', '.ico': 'image/x-icon' }[path.extname(file)] || 'application/octet-stream';
  res.writeHead(200, { 'Content-Type': type }); fs.createReadStream(file).pipe(res);
});
(async () => {
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  const origin = `http://127.0.0.1:${server.address().port}`;
  const browserPort = 9798;
  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'autoparts-nfe-review-'));
  const edge = spawn('C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', ['--headless', '--disable-gpu', '--no-first-run', '--disable-extensions', `--remote-debugging-port=${browserPort}`, `--user-data-dir=${profile}`, 'about:blank'], { windowsHide: true, stdio: 'ignore' });
  let socket;
  const errors = [];
  try {
    let targets;
    for (let i = 0; i < 60; i++) { try { targets = await (await fetch(`http://localhost:${browserPort}/json`)).json(); break; } catch { await pause(200); } }
    assert.ok(targets, 'Browser failed to start');
    socket = new WebSocket(targets.find(t => t.type === 'page').webSocketDebuggerUrl);
    await new Promise(r => socket.addEventListener('open', r, { once: true }));
    let sequence = 0; const pending = new Map();
    const send = (method, params = {}) => new Promise((resolve, reject) => { const id = ++sequence; pending.set(id, { resolve, reject }); socket.send(JSON.stringify({ id, method, params })); });
    socket.onmessage = e => { const m = JSON.parse(e.data); if (m.id) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.reject(m.error) : p.resolve(m.result); } else if (m.method === 'Runtime.exceptionThrown') errors.push(m.params.exceptionDetails); };
    const evaluate = async expression => { const v = await send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true }); if (v.exceptionDetails) throw Error(JSON.stringify(v.exceptionDetails)); return v.result.value; };
    const waitFor = async expression => { for (let i = 0; i < 100; i++) { if (await evaluate(expression)) return; await pause(100); } throw Error('Timeout: ' + expression); };
    await send('Page.enable'); await send('Runtime.enable');
    await send('Emulation.setDeviceMetricsOverride', { width: 1680, height: 1050, deviceScaleFactor: 1, mobile: false });
    await send('Page.addScriptToEvaluateOnNewDocument', { source: `if (location.origin === ${JSON.stringify(origin)}) localStorage.setItem('uniflow:certificado:empresa-1:usuario-visual-1', ${JSON.stringify(JSON.stringify(certificate))});` });
    await send('Page.navigate', { url: origin + '/app/notas' });
    await waitFor("!!document.querySelector('.resend-action')");
    assert.equal(await evaluate("document.querySelector('.resend-action').disabled"), false);
    assert.ok(await evaluate("parseFloat(getComputedStyle(document.querySelector('td')).fontSize) >= 13"), 'Invoice text should be larger');
    assert.ok(await evaluate("document.querySelector('.action').getBoundingClientRect().height >= 36"), 'Action controls should be larger');
    const table = await send('Page.captureScreenshot', { format: 'png' }); fs.writeFileSync(path.join(output, 'invoices-desktop.png'), Buffer.from(table.data, 'base64'));
    await evaluate("document.querySelector('.resend-action').click(); true");
    await waitFor("document.querySelector('.resend-dialog').open");
    assert.equal(resends, 0, 'Opening the review must not transmit');
    await evaluate("document.querySelector('.resend-submit').click(); true");
    await waitFor("document.querySelector('.resend-result')?.textContent.includes('143260000000001')");
    assert.equal(resends, 1);
    assert.ok(await evaluate("document.querySelector('.resend-result').textContent.includes('Autorizado o uso da NF-e')"));
    const result = await send('Page.captureScreenshot', { format: 'png' }); fs.writeFileSync(path.join(output, 'invoice-resend-result.png'), Buffer.from(result.data, 'base64'));
    await send('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: true }); await pause(200);
    assert.equal(await evaluate("document.querySelector('.resend-dialog').scrollWidth <= document.querySelector('.resend-dialog').clientWidth + 1"), true, 'Resend dialog overflows on mobile');
    const mobile = await send('Page.captureScreenshot', { format: 'png' }); fs.writeFileSync(path.join(output, 'invoice-resend-mobile.png'), Buffer.from(mobile.data, 'base64'));
    assert.deepEqual(errors, [], 'Browser runtime errors');
    console.log('Invoice browser checks passed: larger text/actions, reviewed resend, visible SEFAZ response/protocol and mobile dialog. Mock API only.');
  } finally { socket?.close(); edge.kill(); server.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; server.close(); });
