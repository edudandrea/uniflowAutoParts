// O navegador usa /api no proprio dominio. O cookie de sessao permanece HttpOnly
// e nao depende de cookies de terceiros entre Railway e o dominio personalizado.
const target = process.env.API_PROXY_TARGET || (process.env.RAILWAY_ENVIRONMENT_NAME ? null : 'http://localhost:5031');
if (!target) throw new Error('Configure API_PROXY_TARGET no servico do frontend com a URL do backend.');
const origin = new URL(target);
if (!['http:', 'https:'].includes(origin.protocol)) throw new Error('API_PROXY_TARGET invalido.');
module.exports = {
  '/api/**': { target: origin.origin, changeOrigin: true, secure: true },
};
