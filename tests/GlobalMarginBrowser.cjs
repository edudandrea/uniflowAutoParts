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
let margin=null, writes=0;
const stock=[{ id: 17, codigo:'17', descricao:'Filtro combustível', marca:'Marca cadastrada', categoria:'Peças', estoque:2, minimo:0, custo:64.39, valor:83.71, margemPreco:30, fabricante:'', ncm:'84212300' }, { id:18, codigo:'18', descricao:'Filtro de óleo', marca:'Marca cadastrada', categoria:'Peças', estoque:1, minimo:0, custo:112.46, valor:0, margemPreco:null, fabricante:'', ncm:'84212300' }];
const fixtures={ '/api/auth/session': session, '/api/estoque': stock };
const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  if (url.pathname.startsWith('/api/')) {
    if (url.pathname==='/api/estoque/margem') {
      if(req.method==='GET') { res.writeHead(200, {'Content-Type':'application/json'}).end(JSON.stringify({ margemPreco:margin })); return; }
      let body=''; req.on('data',chunk=>body+=chunk); req.on('end',()=>{ writes++; margin=JSON.parse(body).margemPreco; for(const product of stock) { product.margemPreco=margin; product.valor=Math.round(product.custo*(1+margin/100)*100)/100; } res.writeHead(200,{'Content-Type':'application/json'}).end(JSON.stringify({ margemPreco:margin, produtosAtualizados:stock.length })); }); return;
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
    await send('Page.navigate', { url: origin + '/app/estoque' });
    await waitFor("!!document.querySelector('#global-margin') && !document.querySelector('#global-margin').disabled && document.querySelectorAll('.sale-price').length === 2");
    assert.equal(await evaluate("document.querySelectorAll('#global-margin').length"),1);
    assert.equal(await evaluate("document.querySelectorAll('.margin-editor').length"),0);
    await evaluate("const m=document.querySelector('#global-margin'); m.value='30'; m.dispatchEvent(new Event('input',{bubbles:true})); const f=document.querySelector('.search-control input'); f.value='combustível'; f.dispatchEvent(new Event('input',{bubbles:true})); true");
    await waitFor("document.querySelectorAll('.sale-price').length === 1");
    await evaluate("document.querySelector('.global-pricing .primary-action').click(); true");
    await waitFor("!!document.querySelector('.swal2-cancel')");
    assert.equal(writes,0);
    await evaluate("document.querySelector('.swal2-confirm').click(); true");
    await waitFor("!!document.querySelector('.swal2-success')");
    assert.equal(writes,1); assert.equal(stock[1].valor,146.20,'Filtered-out product must receive the global margin');
    await evaluate("document.querySelector('.swal2-confirm').click(); true");
    await waitFor("!document.querySelector('.swal2-container')");
    await evaluate("const resetFilter=document.querySelector('.search-control input'); resetFilter.value=''; resetFilter.dispatchEvent(new Event('input',{bubbles:true})); true");
    await waitFor("document.querySelectorAll('.sale-price').length === 2");
    assert.ok(await evaluate("document.querySelector('tbody').textContent.includes('146,20')"));
    const desktop=await send('Page.captureScreenshot',{format:'png'}); fs.writeFileSync(path.join(output,'stock-global-margin-desktop.png'),Buffer.from(desktop.data,'base64'));
    await send('Page.reload');
    await waitFor("document.querySelector('#global-margin')?.value === '30' && document.querySelectorAll('.sale-price').length === 2");
    await send('Emulation.setDeviceMetricsOverride', { width:390, height:844, deviceScaleFactor:1, mobile:true }); await pause(200);
    assert.ok(await evaluate("document.querySelector('.global-pricing').scrollWidth <= document.querySelector('.global-pricing').clientWidth + 1"),'Margin panel overflows on mobile');
    await evaluate("document.querySelector('.global-pricing').scrollIntoView(); true");
    const mobile=await send('Page.captureScreenshot',{format:'png'}); fs.writeFileSync(path.join(output,'stock-global-margin-mobile.png'),Buffer.from(mobile.data,'base64'));
    assert.deepEqual(errors, [], 'Browser runtime errors');
    console.log('Global margin browser checks passed: one configuration, entire stock despite filters, persisted after reload, SweetAlert2 and responsive panel. Mock API only.');
  } finally { socket?.close(); edge.kill(); server.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; server.close(); });
