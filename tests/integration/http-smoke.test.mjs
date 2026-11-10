// Testes HTTP end-to-end contra três PostgreSQL descartáveis no CI.
// Executar somente com ROTA_WEB_SMOKE_URL e credenciais efêmeras de teste.
import test from 'node:test'
import assert from 'node:assert/strict'

const base = process.env.ROTA_WEB_SMOKE_URL
const email = process.env.ROTA_WEB_SMOKE_ADMIN_EMAIL
const password = process.env.ROTA_WEB_SMOKE_ADMIN_PASSWORD
if (process.env.ROTA_WEB_REQUIRE_SMOKE === '1' && (!base || !email || !password)) {
  throw new Error('Integração CI sem URL ou credenciais efêmeras: teste não pode ser ignorado.')
}

const parseCookies = (response) => {
  const entries = response.headers.getSetCookie?.() || []
  return entries.map((value) => value.split(';', 1)[0]).filter(Boolean)
}

const mergeCookies = (...sets) => [...new Map(sets.flat().map((x) => {
  const equals = x.indexOf('=')
  return [x.slice(0, equals), x]
})).values()].join('; ')

const request = (path, { method = 'GET', cookie = '', token = '', json } = {}) => fetch(`${base}${path}`, {
  method,
  redirect: 'manual',
  headers: {
    Accept: 'application/json',
    ...(cookie ? { Cookie: cookie } : {}),
    ...(token ? { 'X-CSRF-TOKEN': token } : {}),
    ...(json ? { 'Content-Type': 'application/json' } : {}),
  },
  ...(json ? { body: JSON.stringify(json) } : {}),
})

test('API integrada: status, isolamento administrativo e sessão revogável', { skip: !base || !email || !password ? 'Somente banco descartável de integração' : false }, async () => {
  let response = await request('/api/security/health')
  assert.equal(response.status, 200)
  assert.equal((await response.json()).status, 'ok')

  response = await request('/api/security/ready')
  assert.equal(response.status, 200)
  assert.equal((await response.json()).status, 'ready')

  response = await request('/api/admin/auth/me')
  assert.equal(response.status, 401, 'Admin precisa ser protegido sem autenticação')

  response = await request('/api/admin/auth/login', {
    method: 'POST', json: { email, senha: password },
  })
  assert.equal(response.status, 400, 'Login sem token CSRF deve ser rejeitado')

  response = await request('/api/security/csrf')
  assert.equal(response.status, 200)
  const csrfCookie = parseCookies(response)
  const csrfToken = (await response.json()).token
  assert.ok(csrfToken && csrfCookie.length, 'Token CSRF deve possuir cookie associado')

  response = await request('/api/admin/auth/login', {
    method: 'POST', cookie: mergeCookies(csrfCookie), token: csrfToken,
    json: { email, senha: password },
  })
  assert.equal(response.status, 200, 'Login com senha válida e CSRF deve funcionar')
  const sessionCookie = parseCookies(response)
  assert.ok(sessionCookie.some((cookie) => cookie.startsWith('RotaWeb.Admin=')), 'Sessão administrativa HttpOnly esperada')
  const authenticatedCookie = mergeCookies(csrfCookie, sessionCookie)

  response = await request('/api/admin/auth/me', { cookie: authenticatedCookie })
  assert.equal(response.status, 200, 'Sessão deve permitir consulta do administrador')
  assert.equal((await response.json()).email.toLowerCase(), email.toLowerCase())

  response = await request('/api/security/admin-csrf', { cookie: authenticatedCookie })
  assert.equal(response.status, 200)
  const adminToken = (await response.json()).token
  const adminCsrfCookie = parseCookies(response)
  const allCookies = mergeCookies(csrfCookie, sessionCookie, adminCsrfCookie)

  response = await request('/api/admin/auth/logout', {
    method: 'POST', cookie: allCookies, token: adminToken,
  })
  assert.equal(response.status, 204)
  response = await request('/api/admin/auth/me', { cookie: authenticatedCookie })
  assert.equal(response.status, 401, 'Sessão revogada não pode autenticar')

  response = await request('/api/trips')
  assert.equal(response.status, 200)
  assert.ok(Array.isArray(await response.json()))
})
