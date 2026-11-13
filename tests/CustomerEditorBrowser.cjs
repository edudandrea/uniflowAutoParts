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
const summary = { id: 7, empresaId: 1, tipoPessoa: 2, relacionamento: 1, nomeRazaoSocial: 'Cliente cadastrado', nomeFantasia: null, cpfCnpj: '88657788000154', email: 'cliente@example.test', telefone: '5432145566', celular: null, ativo: true, criadoEm: '2026-10-02T12:00:00Z' };
const address = { id: 6, empresaId: 1, nome: 'Endereco principal', cep: '95098340', logradouro: 'Rua das Industrias', numero: '1450', complemento: 'Sala 2', bairro: 'Distrito Industrial', codigoIbgeMunicipio: '4305108', municipio: 'Caxias do Sul', uf: 'RS', pais: 'Brasil', ativo: true };
const detail = { id: 7, criadoEm: summary.criadoEm, dados: { empresaId: 1, tipoPessoa: 2, relacionamento: 1, nomeRazaoSocial: summary.nomeRazaoSocial, nomeFantasia: null, cpfCnpj: summary.cpfCnpj, rg: null, orgaoEmissor: null, estadoCivil: null, dataNascimento: null, profissao: null, cnh: null, inscricaoEstadual: '0290123456', inscricaoMunicipal: '12345', indicadorIe: 1, consumidorFinal: false, email: summary.email, telefone: summary.telefone, celular: null, clienteDesde: '2026-10-02', origem: 'Indicacao', vendedorPadrao: null, limiteCredito: 1500, observacao: 'Observacao real do cadastro', ativo: true, enderecoId: 6, endereco: { ...address, tipo: 2, principal: true }, contatos: [{ nome: 'Contato principal', cargo: 'Gerente', email: 'contato@example.test', telefone: '5432145566', celular: null, principal: true }, { nome: 'Contato adicional', cargo: null, email: 'outro@example.test', telefone: null, celular: null, principal: false }], dadosComerciais: { condicaoPagamento: '30 dias', limiteCredito: 1500, tabelaPreco: 'Oficina', vendedorPadrao: null, origem: 'Indicacao', bloquearVenda: false, permiteFiado: true } } };
const session = { authenticated: true, available: true, portalUrl: null, empresa: { id: 1, nome: 'AutoParts', razaoSocial: 'AutoParts', cnpj: '12345678000195', hubTenantId: 'visual-1' }, usuario: { id: '1', sujeito: 'visual-1', nome: 'Usuário de teste', email: 'visual@example.test', perfil: 'Operador' } };
let updates = 0;
const product = { id: 17, sku: 610, descricao: 'Filtro combustivel', preco: 83.71, custo: 30, ativo: true, ncm: 84212300, origemMercadoria: '0', unidadeComercial: 'UN' };
const fixtures = { '/api/auth/session': session, '/api/clientes': [summary], '/api/enderecos': [address], '/api/clientes/7': detail, '/api/fiscal/ambiente': { environment: 'producao', seriePadrao: 1 }, '/api/produtos': { produtos: [product], total: 1 } };
const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  if (url.pathname.startsWith('/api/')) {
    if (req.method === 'GET' && url.pathname === '/api/fiscal/destinatario/7') {
      res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' }).end(JSON.stringify({ inscricaoEstadual: detail.dados.inscricaoEstadual, indicadorIe: detail.dados.indicadorIe, consumidorFinal: detail.dados.consumidorFinal, logradouro: address.logradouro, numero: address.numero, complemento: address.complemento, bairro: address.bairro, municipio: address.municipio, codigoMunicipio: address.codigoIbgeMunicipio, uf: address.uf, cep: address.cep })); return;
    }
    if (req.method === 'PUT' && url.pathname === '/api/clientes/7') {
      let body = ''; req.on('data', chunk => body += chunk); req.on('end', () => {
        const payload = JSON.parse(body); assert.equal(payload.inscricaoEstadual, '0298888888'); assert.equal(payload.enderecoId, 6); assert.equal(payload.contatos.length, 2); updates++;
        detail.dados = payload; summary.nomeRazaoSocial = payload.nomeRazaoSocial;
        res.writeHead(200, { 'Content-Type': 'application/json' }).end(JSON.stringify(summary));
      }); return;
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
    await send('Emulation.setDeviceMetricsOverride', { width: 1360, height: 1000, deviceScaleFactor: 1, mobile: false });
    await send('Page.navigate', { url: origin + '/app/clientes' });
    await waitFor("!!document.querySelector('.client-result-card')");
    assert.equal(await evaluate("!!document.querySelector('.client-detail-layout')"), false, 'Inline details remain');
    await evaluate("document.querySelector('.client-result-card').click(); true");
    await waitFor("document.querySelector('.customer-detail-modal').open && document.querySelector('.customer-detail-body')?.textContent.includes('0290123456')");
    assert.equal(await evaluate("document.querySelectorAll('.detail-tabs button').length"), 5);
    await evaluate("document.querySelectorAll('.detail-tabs button')[1].click(); true");
    assert.equal(await evaluate("document.querySelectorAll('.detail-tabs button')[1].getAttribute('aria-current')"), 'location');
    await evaluate("document.querySelectorAll('.detail-tabs button')[0].click(); true");
    await evaluate(`(() => {
      const original = document.createElement;
      document.createElement = function(tag, ...args) {
        const node = original.call(document, tag, ...args);
        if (tag === 'iframe') node.addEventListener('load', () => { node.contentWindow.print = () => { window.__customerPrintText = node.contentDocument.body.textContent; }; }, { once: true });
        return node;
      };
      document.querySelector('.detail-print').click(); document.createElement = original; return true;
    })()`);
    await waitFor("window.__customerPrintText?.includes('0290123456')");
    await evaluate("document.querySelector('iframe[aria-hidden]')?.remove(); true");
    const details = await send('Page.captureScreenshot', { format: 'png' }); fs.writeFileSync(path.join(output, 'customer-detail-desktop.png'), Buffer.from(details.data, 'base64'));
    await evaluate("document.querySelector('.edit-customer').click(); true");
    await waitFor("document.querySelector('.customer-modal-large').open && document.querySelector('[formControlName=inscricaoEstadual]')?.value === '0290123456'");
    const editor = await send('Page.captureScreenshot', { format: 'png' }); fs.writeFileSync(path.join(output, 'customer-edit-desktop.png'), Buffer.from(editor.data, 'base64'));
    await evaluate("(() => { const input = document.querySelector('[formControlName=inscricaoEstadual]'); input.value = '0298888888'; input.dispatchEvent(new Event('input', { bubbles: true })); return true; })()");
    await evaluate("document.querySelector('.customer-form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })); true");
    await waitFor("document.querySelector('.customer-detail-modal').open && document.querySelector('.customer-detail-body')?.textContent.includes('0298888888')");
    assert.equal(updates, 1, 'Edition did not persist exactly once');
    await send('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: true }); await pause(150);
    assert.equal(await evaluate("document.querySelector('.customer-detail-modal').scrollWidth <= document.querySelector('.customer-detail-modal').clientWidth + 1"), true);
    const mobileDetails = await send('Page.captureScreenshot', { format: 'png' }); fs.writeFileSync(path.join(output, 'customer-detail-mobile.png'), Buffer.from(mobileDetails.data, 'base64'));

    assert.equal(await evaluate("document.querySelectorAll('.client-result-card').length"), 1, 'Edition duplicated the customer');
    await evaluate("document.querySelector('.edit-customer').click(); true");
    await send('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: true }); await pause(200);
    assert.equal(await evaluate("document.querySelector('.customer-modal-large').scrollWidth <= document.querySelector('.customer-modal-large').clientWidth + 1"), true, 'Editor overflow on mobile');
    const mobile = await send('Page.captureScreenshot', { format: 'png' }); fs.writeFileSync(path.join(output, 'customer-edit-mobile.png'), Buffer.from(mobile.data, 'base64'));
    const draft = { cliente: summary, itens: [{ produto: product, quantidade: 1, preco: 83.71, desconto: 0 }], pagamentos: [{ id: 1, forma: 'Pix', valor: 83.71 }], tipo: 'Venda', vendedor: '', emitir: true, modelo: '55', finalidade: 'Normal', natureza: 'Venda de mercadoria', cfop: '5102', serie: '1', desconto: 0, frete: 0, despesas: 0, observacoes: '', destinatarioClienteId: 7, destinatario: { inscricaoEstadual: detail.dados.inscricaoEstadual, indicadorIe: 9, consumidorFinal: true, logradouro: address.logradouro, numero: address.numero, complemento: address.complemento, bairro: address.bairro, municipio: address.municipio, codigoMunicipio: address.codigoIbgeMunicipio, uf: address.uf, cep: address.cep } };
    await evaluate(`localStorage.setItem('uniflow:venda:rascunho:empresa-1:usuario-visual-1', ${JSON.stringify(JSON.stringify(draft))}); true`);
    await send('Page.navigate', { url: origin + '/app/vendas' });
    await waitFor("!!document.querySelector('.finish-button')");
    await evaluate("document.querySelector('.finish-button').click(); true");
    await waitFor("document.querySelector('.sale-review').open && document.querySelector('.destination-fields select')?.selectedOptions[0].textContent.includes('Contribuinte do ICMS')");
    assert.deepEqual(errors, [], 'Browser runtime errors');
    console.log('Customer browser checks passed: detail modal, existing IE, edit/save/reopen, preserved address and contacts, desktop/mobile layout, print view and current registry IE in a restored sales draft. Mock API only.');
  } finally { socket?.close(); edge.kill(); server.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; server.close(); });
