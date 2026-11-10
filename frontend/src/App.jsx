import { useEffect, useState } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { apiFetch } from './api/client.js'
import AdminProtectedRoute from './components/AdminProtectedRoute.jsx'
import ProtectedRoute from './components/ProtectedRoute.jsx'
import AdminDashboard from './pages/AdminDashboard.jsx'
import AdminLogin from './pages/AdminLogin.jsx'
import Cadastro from './pages/Cadastro.jsx'
import Compra from './pages/Compra.jsx'
import CompraConcluida from './pages/CompraConcluida.jsx'
import Home from './pages/Home.jsx'
import Login from './pages/Login.jsx'
import MinhasPassagens from './pages/MinhasPassagens.jsx'
import NotFound from './pages/NotFound.jsx'
import PassagemDetalhe from './pages/PassagemDetalhe.jsx'
import Perfil from './pages/Perfil.jsx'

// Componente raiz responsável por rotas e identidades carregadas em memória.
export default function App() {
  // Passageiro autenticado é mantido apenas em memória; a credencial verdadeira permanece no cookie HttpOnly.
  const [user, setUser] = useState(null)

  // Administrador autenticado é mantido em estado separado.
  const [admin, setAdmin] = useState(null)

  // Carrega uma sessão de passageiro existente quando a aplicação abre ou é atualizada.
  useEffect(() => {
    let active = true

    async function restorePassenger() {
      try {
        // O navegador envia o cookie HttpOnly automaticamente.
        const { response, data } = await apiFetch('/auth/me')
        if (active && response.ok) {
          setUser(data)
        }
      } catch {
        // Visitante sem backend disponível continua como não autenticado; telas exibem seus próprios erros de rede.
      }
    }

    restorePassenger()

    // Evita atualizar estado após desmontagem.
    return () => {
      active = false
    }
  }, [])

  // Define todas as rotas documentadas do projeto.
  return (
    <Routes>
      {/* Busca e resultados são públicos. */}
      <Route path="/" element={<Home user={user} onUserChange={setUser} />} />

      {/* Login e cadastro são públicos. */}
      <Route path="/login" element={<Login onUserChange={setUser} />} />
      <Route path="/cadastro" element={<Cadastro />} />

      {/* Compra exige passageiro autenticado. */}
      <Route
        path="/comprar/:id"
        element={
          <ProtectedRoute user={user} onUserChange={setUser}>
            <Compra user={user} onUserChange={setUser} />
          </ProtectedRoute>
        }
      />


      {/* Fechamento da compra mostra comprovante e cartões de embarque. */}
      <Route
        path="/compra/concluida"
        element={
          <ProtectedRoute user={user} onUserChange={setUser}>
            <CompraConcluida user={user} onUserChange={setUser} />
          </ProtectedRoute>
        }
      />

      {/* Histórico do usuário autenticado. */}
      <Route
        path="/minhas-passagens"
        element={
          <ProtectedRoute user={user} onUserChange={setUser}>
            <MinhasPassagens user={user} onUserChange={setUser} />
          </ProtectedRoute>
        }
      />

      {/* Comprovante individual protegido contra acesso de terceiros. */}
      <Route
        path="/passagens/:id"
        element={
          <ProtectedRoute user={user} onUserChange={setUser}>
            <PassagemDetalhe user={user} onUserChange={setUser} />
          </ProtectedRoute>
        }
      />

      {/* Perfil e senha do passageiro. */}
      <Route
        path="/perfil"
        element={
          <ProtectedRoute user={user} onUserChange={setUser}>
            <Perfil user={user} onUserChange={setUser} />
          </ProtectedRoute>
        }
      />

      {/* Administração usa conta e sessão próprias. */}
      <Route path="/admin/login" element={<AdminLogin />} />
      {/* Link antigo redireciona para o login normal, sem mostrar autenticador. */}
      <Route path="/admin/mfa" element={<Navigate to="/admin/login" replace />} />
      <Route
        path="/admin"
        element={
          <AdminProtectedRoute admin={admin} onAdminChange={setAdmin}>
            <AdminDashboard admin={admin} onAdminChange={setAdmin} />
          </AdminProtectedRoute>
        }
      />

      {/* Compatibilidade com rota /viagens usada durante as aulas: redireciona para a busca principal. */}
      <Route path="/viagens" element={<Navigate to="/" replace />} />

      {/* Qualquer outra URL mostra página 404 controlada. */}
      <Route path="*" element={<NotFound />} />
    </Routes>
  )
}
