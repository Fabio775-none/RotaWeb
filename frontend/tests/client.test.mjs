import test from 'node:test'
import assert from 'node:assert/strict'
import { apiFetch, apiJson, apiErrorMessage, resetCsrfToken } from '../src/api/client.js'

function mockFetch(responses) {
  const calls = []
  globalThis.fetch = async (url, options = {}) => {
    calls.push({ url, options })
    const next = responses.shift()
    assert.ok(next, `Unexpected request: ${url}`)
    const headers = new Headers({ 'content-type': 'application/json' })
    return new Response(next.body === null ? null : JSON.stringify(next.body), {
      status: next.status || 200, headers,
    })
  }
  return calls
}

test('GET never requests CSRF and always sends browser cookies', async () => {
  resetCsrfToken()
  const calls = mockFetch([{ body: { status: 'ok' } }])
  const { response, data } = await apiFetch('/security/health')
  assert.equal(response.status, 200)
  assert.deepEqual(data, { status: 'ok' })
  assert.equal(calls.length, 1)
  assert.equal(calls[0].url, '/api/security/health')
  assert.equal(calls[0].options.credentials, 'include')
  assert.equal(new Headers(calls[0].options.headers).get('Accept'), 'application/json')
})

test('passenger POST fetches and forwards a request CSRF token', async () => {
  resetCsrfToken()
  const calls = mockFetch([{ body: { token: 'random-csrf' } }, { status: 201, body: { id: 8 } }])
  const { response, data } = await apiJson('/auth/register', 'POST', { nome: 'Ada' })
  assert.equal(response.status, 201)
  assert.deepEqual(data, { id: 8 })
  assert.equal(calls[0].url, '/api/security/csrf')
  assert.equal(calls[1].url, '/api/auth/register')
  assert.equal(new Headers(calls[1].options.headers).get('X-CSRF-TOKEN'), 'random-csrf')
  assert.equal(new Headers(calls[1].options.headers).get('Content-Type'), 'application/json')
  assert.deepEqual(JSON.parse(calls[1].options.body), { nome: 'Ada' })
})

test('authenticated admin mutation uses admin-CSRF, not public CSRF', async () => {
  resetCsrfToken()
  const calls = mockFetch([{ body: { token: 'admin-token' } }, { body: { ok: true } }])
  await apiFetch('/admin/buses', { method: 'DELETE' })
  assert.equal(calls[0].url, '/api/security/admin-csrf')
  assert.equal(new Headers(calls[1].options.headers).get('X-CSRF-TOKEN'), 'admin-token')
})

test('admin login uses public CSRF while identity is unauthenticated', async () => {
  resetCsrfToken()
  const calls = mockFetch([{ body: { token: 'public-token' } }, { body: { requiresMfa: true } }])
  const { data } = await apiJson('/admin/auth/login', 'POST', { email: 'admin@example.org', senha: 'secret' })
  assert.equal(calls[0].url, '/api/security/csrf')
  assert.equal(data.requiresMfa, true)
})

test('API validation errors prefer field errors over generic fallback', async () => {
  resetCsrfToken()
  mockFetch([{ status: 400, body: { title: 'Bad Request', errors: { email: ['Inválido'] } } }])
  const { response, data } = await apiFetch('/auth/me')
  assert.equal(response.ok, false)
  assert.equal(apiErrorMessage(data), 'Inválido')
})

test('204 body is null and error fallback is stable', async () => {
  resetCsrfToken()
  mockFetch([{ status: 204, body: null }])
  const { data } = await apiFetch('/auth/me')
  assert.equal(data, null)
  assert.equal(apiErrorMessage(data, 'Falhou'), 'Falhou')
})

test('mutations refresh CSRF rather than reusing an old identity token', async () => {
  resetCsrfToken()
  const calls = mockFetch([
    { body: { token: 'token-1' } }, { body: {} },
    { body: { token: 'token-2' } }, { body: {} },
  ])
  await apiJson('/auth/login', 'POST', { email: 'one' })
  await apiJson('/auth/logout', 'POST', {})
  assert.equal(calls.length, 4)
  assert.equal(new Headers(calls[1].options.headers).get('X-CSRF-TOKEN'), 'token-1')
  assert.equal(new Headers(calls[3].options.headers).get('X-CSRF-TOKEN'), 'token-2')
})
