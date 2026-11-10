import { useEffect, useState } from 'react'
import { Navigate } from 'react-router-dom'
import { apiFetch } from '../api/client.js'

// Protege a área administrativa usando exclusivamente a sessão administrativa própria.
export default function AdminProtectedRoute({ admin, onAdminChange, children }) {
  // Estado inicial depende de já existir administrador carregado em memória.
  const [loading, setLoading] = useState(!admin)

  // Consulta a sessão administrativa quando necessário.
  useEffect(() => {
    let active = true

    async function loadAdmin() {
      // Evita chamada duplicada quando o estado global já está pronto.
      if (admin) {
        setLoading(false)
        return
      }

      try {
        // /admin/auth/me só aceita o esquema administrativo completo.
        const { response, data } = await apiFetch('/admin/auth/me')
        if (active && response.ok) {
          onAdminChange?.(data)
        }
      } finally {
        // Libera renderização ao final da tentativa.
        if (active) {
          setLoading(false)
        }
      }
    }

    loadAdmin()

    // Marca efeito como inativo ao desmontar.
    return () => {
      active = false
    }
  }, [admin, onAdminChange])

  // Feedback durante consulta.
  if (loading) {
    return <div className="page-state">Verificando acesso administrativo...</div>
  }

  // Sem sessão administrativa volta ao login específico.
  if (!admin) {
    return <Navigate to="/admin/login" replace />
  }

  // Sessão administrativa válida libera os componentes de gestão.
  return children
}
