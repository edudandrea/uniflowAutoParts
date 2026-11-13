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
const product = { id: 17, sku: 610, descricao: 'FILTRO COMBUSTÍVEL', preco: 83.71, custo: 30, ativo: true, ncm: 84212300, origemMercadoria: '0', unidadeComercial: 'UN' };
const customer = { id: 7, nomeRazaoSocial: 'Mecanica Cecconi Ltda – EPP', cpfCnpj: '88.657.788/0001-54', telefone: '(54) 3214-5566', ativo: true, relacionamento: 1 };
const address = { inscricaoEstadual: '0290123456', indicadorIe: 1, consumidorFinal: false, logradouro: 'Rua das Indústrias', numero: '1450', complemento: 'Sala 02', bairro: 'Distrito Industrial', municipio: 'Caxias do Sul', codigoMunicipio: '4305108', uf: 'RS', cep: '95098340' };
const session = { authenticated: true, available: true, portalUrl: null, empresa: { id: 1, nome: 'AutoParts', razaoSocial: 'AutoParts', cnpj: '12345678000195', hubTenantId: 'visual-1' }, usuario: { id: '1', sujeito: 'visual-1', nome: 'Usuário de teste', email: 'visual@example.test', perfil: 'Operador' } };
let addressRequests = 0, emissions = 0, consultations = 0; const downloads = { xml: 0, pdf: 0 };
const invoice = { id: 37, numero: '2', serie: '1', chaveAcesso: '43'+'1'.repeat(42), ambiente: 1, situacao: 'Autorizada', codigo: '100', motivo: 'Autorizado o uso da NF-e', protocolo: '143260000000001', podeReenviar: false };
const certificate = { fileName: 'fixture.pfx', base64: 'browser-fixture-only', password: 'fixture-only', uf: 'RS', environment: 'producao' };
const fixtures = { '/api/auth/session': session, '/api/fiscal/ambiente': { environment: 'producao', seriePadrao: 1 }, '/api/clientes': [customer], '/api/produtos': { produtos: [product], total: 1 }, '/api/fiscal/destinatario/7': address };
const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  if (url.pathname.startsWith('/api/')) {
    const download = url.pathname.match(/^\/api\/notas-fiscais\/37\/(xml|pdf)$/);
    if (req.method === 'GET' && download) {
      const format=download[1]; downloads[format]++;
      res.writeHead(200, { 'Content-Type': format === 'pdf' ? 'application/pdf' : 'application/xml' });
      res.end(format === 'pdf' && process.env.NFE_PDF_DIR ? fs.readFileSync(path.join(process.env.NFE_PDF_DIR,'danfe-authorized.pdf')) : format === 'pdf' ? '%PDF-1.4\nfixture\n%%EOF' : '<nfeProc/>'); return;
    }
    if (req.method === 'POST' && url.pathname === '/api/notas-fiscais/emitir') {
      emissions++; res.writeHead(200, { 'Content-Type': 'application/json' }).end(JSON.stringify(invoice)); return;
    }
    if (req.method === 'POST' && url.pathname === '/api/notas-fiscais/37/consultar') {
      consultations++; res.writeHead(200, { 'Content-Type': 'application/json' }).end(JSON.stringify({ codigo: '105', motivo: 'Lote em processamento', situacao: 'Processando', pendente: false })); return;
    }
    if (url.pathname === '/api/notas-fiscais') {
      res.writeHead(200, { 'Content-Type': 'application/json' }).end(JSON.stringify({ notas: [{ ...invoice, cliente: customer.nomeRazaoSocial, documentoCliente: customer.cpfCnpj, emissao: '2026-11-13T19:00:00Z', tipo: 'NFe', valor: 83.71, temPdf: true, podeCancelar: false, podeCorrigir: false }], total: 1, pagina: 1, tamanhoPagina: 10, resumo: { emitidas: 1, autorizadas: 0, processando: 1, canceladas: 0, valorTotal: 0 } })); return;
    }
    if (req.method !== 'GET' || !fixtures[url.pathname]) { res.writeHead(404).end(); return; }
    if (url.pathname.includes('/destinatario/')) addressRequests++;
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
    await send('Browser.setDownloadBehavior', { behavior: 'allow', downloadPath: output, eventsEnabled: true });
    await send('Emulation.setDeviceMetricsOverride', { width: 1360, height: 1000, deviceScaleFactor: 1, mobile: false });
    const draft = { cliente: customer, itens: [{ produto: product, quantidade: 1, preco: 83.71, desconto: 0 }], pagamentos: [{ id: 1, forma: 'Pix', valor: 83.71 }], tipo: 'Venda', vendedor: '', emitir: true, modelo: '55', finalidade: 'Normal', natureza: 'Venda de mercadoria adquirida ou recebida de terceiros', cfop: '5102', serie: '1', desconto: 0, frete: 0, despesas: 0, observacoes: '', csosn: '102' };
    await send('Page.addScriptToEvaluateOnNewDocument', { source: `if (location.origin === ${JSON.stringify(origin)}) localStorage.setItem('uniflow:certificado:empresa-1:usuario-visual-1', ${JSON.stringify(JSON.stringify(certificate))}); if (location.origin === ${JSON.stringify(origin)}) localStorage.setItem('uniflow:venda:rascunho:empresa-1:usuario-visual-1', ${JSON.stringify(JSON.stringify(draft))});` });
    await send('Page.navigate', { url: origin + '/app/vendas' });
    await waitFor("!!document.querySelector('.finish-button') && !document.querySelector('.sales-page > .notice.error')");
    await evaluate("document.querySelector('.finish-button').click(); true");
    await waitFor("document.querySelector('.sale-review').open && document.querySelector('[aria-label=\"Logradouro do destinatário\"]')?.value === 'Rua das Indústrias'");
    assert.equal(addressRequests, 1, 'Address must load automatically');
    assert.equal(await evaluate("document.querySelector('[aria-label=\"Código IBGE do destinatário\"]').value"), '4305108');
    assert.equal(await evaluate("document.querySelector('.review-navigation').children.length"), 4);
    const desktop = await send('Page.captureScreenshot', { format: 'png' }); fs.writeFileSync(path.join(output, 'nfe-review-desktop.png'), Buffer.from(desktop.data, 'base64'));
    await evaluate("document.querySelectorAll('.review-navigation button')[2].click(); true");
    assert.equal(await evaluate("document.querySelectorAll('.review-navigation button')[2].getAttribute('aria-current')"), 'location');
    await send('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: true }); await pause(200);
    assert.equal(await evaluate("document.querySelector('.sale-review').scrollWidth <= document.querySelector('.sale-review').clientWidth + 1"), true, 'Dialog overflow on mobile');
    assert.equal(await evaluate("document.querySelector('.invoice-button').getBoundingClientRect().bottom <= innerHeight"), true, 'Emission button outside viewport');
    await evaluate("document.querySelector('.review-body').scrollTop = 0; true");
    const mobile = await send('Page.captureScreenshot', { format: 'png' }); fs.writeFileSync(path.join(output, 'nfe-review-mobile.png'), Buffer.from(mobile.data, 'base64'));
    await evaluate("document.querySelector('.invoice-button').click(); true");
    await waitFor("!!document.querySelector('.swal2-success')");
    await evaluate("document.querySelector('.swal2-confirm').click(); true");
    await waitFor("!document.querySelector('.swal2-container')");
    assert.equal(emissions, 1);
    for (const selector of ['.review-download:not(.review-pdf)', '.review-pdf']) {
      assert.equal(await evaluate("document.querySelector('"+selector+"').disabled"), false);
      assert.ok(await evaluate("document.querySelector('"+selector+"').getBoundingClientRect().bottom <= innerHeight"), 'Download button outside mobile viewport');
      await evaluate("document.querySelector('"+selector+"').click(); true");
      await pause(150);
    }
    assert.equal(downloads.xml, 1); assert.equal(downloads.pdf, 1);
    const downloadMobile=await send('Page.captureScreenshot',{format:'png'}); fs.writeFileSync(path.join(output,'authorized-downloads-mobile.png'),Buffer.from(downloadMobile.data,'base64'));
    await send('Page.navigate', { url: origin + '/app/notas' });
    await waitFor("document.querySelector('tbody')?.textContent.includes('Autorizada')");
    await send('Emulation.setDeviceMetricsOverride', { width: 1360, height: 1000, deviceScaleFactor: 1, mobile: false });
    for (const label of ['Baixar PDF da nota 2', 'Baixar XML da nota 2']) {
      await evaluate("[...document.querySelectorAll('button')].find(b=>b.getAttribute('aria-label')==='"+label+"').click(); true");
      await pause(150);
    }
    assert.equal(downloads.xml, 2); assert.equal(downloads.pdf, 2);
    assert.equal(emissions, 1, 'Downloads must never retransmit the invoice');
    const downloadDesktop=await send('Page.captureScreenshot',{format:'png'}); fs.writeFileSync(path.join(output,'authorized-downloads-desktop.png'),Buffer.from(downloadDesktop.data,'base64'));
    assert.deepEqual(errors, [], 'Browser runtime errors');
    console.log('Fiscal downloads browser checks passed: XML and PDF in emission review and NF-e menu, mobile buttons visible, no retransmission or runtime errors. Mock API only.');
  } finally { socket?.close(); edge.kill(); server.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; server.close(); });
