import { useEffect, useState } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { apiFetch } from '../api/client.js'

// Protege rotas de passageiro consultando a sessão HttpOnly no servidor.
export default function ProtectedRoute({ user, onUserChange, children }) {
  // Estado de carregamento evita redirecionamento antes da verificação de sessão.
  const [loading, setLoading] = useState(!user)

  // Guarda a rota solicitada para retornar ao mesmo ponto após login.
  const location = useLocation()

  // Quando não há usuário em memória, consulta /auth/me.
  useEffect(() => {
    let active = true

    async function loadUser() {
      // Usuário já carregado não exige nova chamada.
      if (user) {
        setLoading(false)
        return
      }

      try {
        // Cookie HttpOnly é enviado automaticamente por apiFetch.
        const { response, data } = await apiFetch('/auth/me')

        // Atualiza o estado global somente enquanto o componente continua montado.
        if (active && response.ok) {
          onUserChange?.(data)
        }
      } finally {
        // Sempre encerra o estado de carregamento.
        if (active) {
          setLoading(false)
        }
      }
    }

    loadUser()

    // Evita atualização após desmontagem.
    return () => {
      active = false
    }
  }, [user, onUserChange])

  // Mostra feedback textual durante a validação da sessão.
  if (loading) {
    return <div className="page-state">Verificando sessão...</div>
  }

  // Visitante é redirecionado e a rota original é preservada em state.
  if (!user) {
    return <Navigate to="/login" replace state={{ returnTo: location.pathname + location.search }} />
  }

  // Sessão válida libera a tela protegida.
  return children
}
