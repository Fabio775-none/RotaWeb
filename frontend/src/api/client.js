// Base da API. Na apresentação final frontend e backend usam a mesma origem.
const API_BASE = import.meta.env?.VITE_API_BASE_URL || '/api'

// Token antiforgery mantido somente em memória; não é credencial de login.
let csrfToken = null

// Obtém um token CSRF novo e o cookie associado quando necessário.
export async function ensureCsrfToken(forceRefresh = false, admin = false) {
  if (csrfToken && !forceRefresh) return csrfToken

  const csrfPath = admin ? '/security/admin-csrf' : '/security/csrf'
  const response = await fetch(`${API_BASE}${csrfPath}`, {
    method: 'GET',
    credentials: 'include',
    headers: { Accept: 'application/json' },
  })

  if (!response.ok) {
    throw new Error('Não foi possível preparar a proteção de segurança da requisição.')
  }

  const data = await response.json()
  csrfToken = data.token
  return csrfToken
}

// Converte respostas de erro do ASP.NET (mensagem, ProblemDetails ou validação automática)
// em uma estrutura simples que a interface consegue mostrar ao usuário.
async function readBody(response) {
  if (response.status === 204) return null

  const contentType = response.headers.get('content-type') || ''
  if (contentType.includes('application/json') || contentType.includes('application/problem+json')) {
    try {
      const data = await response.json()
      if (data && !data.mensagem && data.errors && typeof data.errors === 'object') {
        const first = Object.values(data.errors).flat().find(Boolean)
        if (first) data.mensagem = String(first)
      }
      if (data && !data.mensagem && data.detail) data.mensagem = data.detail
      if (data && !data.mensagem && data.title && !response.ok) data.mensagem = data.title
      return data
    } catch {
      return null
    }
  }

  try {
    const text = await response.text()
    return text ? { mensagem: text } : null
  } catch {
    return null
  }
}

// Faz uma chamada à API com cookies HttpOnly e proteção CSRF automática.
export async function apiFetch(path, options = {}) {
  const method = (options.method || 'GET').toUpperCase()
  const headers = new Headers(options.headers || {})

  if (!headers.has('Accept')) headers.set('Accept', 'application/json')

  if (!['GET', 'HEAD', 'OPTIONS'].includes(method)) {
    // O login usa antiforgery anônimo; demais operações admin usam token da sessão.
    const adminAuthenticatedMutation =
      path.startsWith('/admin/') && path !== '/admin/auth/login' 

    const token = await ensureCsrfToken(true, adminAuthenticatedMutation)
    headers.set('X-CSRF-TOKEN', token)
  }

  const response = await fetch(`${API_BASE}${path}`, {
    ...options,
    method,
    headers,
    credentials: 'include',
  })

  const data = await readBody(response)
  return { response, data }
}

// Atalho para JSON.
export async function apiJson(path, method, body) {
  return apiFetch(path, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
}

// Retorna a melhor mensagem disponível para erros de API.
export function apiErrorMessage(data, fallback = 'Não foi possível concluir a operação.') {
  return data?.mensagem || data?.detail || data?.title || fallback
}

export function resetCsrfToken() {
  csrfToken = null
}

export function apiUrl(path) {
  return `${API_BASE}${path}`
}
