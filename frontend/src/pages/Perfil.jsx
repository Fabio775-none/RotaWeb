import { useState } from 'react'
import { apiJson } from '../api/client.js'
import Layout from '../components/Layout.jsx'
import StatusMessage from '../components/StatusMessage.jsx'

// Tela de perfil e alteração segura de senha.
export default function Perfil({ user, onUserChange }) {
  const [name, setName] = useState(user?.nome || '')
  const [birthDate, setBirthDate] = useState(user?.dataNascimento || '')

  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')

  const [profileMessage, setProfileMessage] = useState('')
  const [passwordMessage, setPasswordMessage] = useState('')

  async function saveProfile(event) {
    event.preventDefault()
    const { response, data } = await apiJson('/auth/profile', 'PUT', {
      nome: name.trim(),
      dataNascimento: birthDate,
    })
    if (!response.ok) {
      setProfileMessage(data?.mensagem || 'Não foi possível atualizar o perfil.')
      return
    }
    onUserChange?.({ ...user, nome: data.usuario.nome, dataNascimento: data.usuario.dataNascimento })
    setProfileMessage('Perfil atualizado com sucesso.')
  }

  async function changePassword(event) {
    event.preventDefault()
    if (newPassword !== confirmPassword) {
      setPasswordMessage('A confirmação da nova senha não confere.')
      return
    }
    const { response, data } = await apiJson('/auth/change-password', 'POST', {
      senhaAtual: currentPassword,
      novaSenha: newPassword,
      confirmarNovaSenha: confirmPassword,
    })
    if (!response.ok) {
      const details = Array.isArray(data?.erros) ? ` ${data.erros.join(' ')}` : ''
      setPasswordMessage(`${data?.mensagem || 'Não foi possível alterar a senha.'}${details}`)
      return
    }
    setCurrentPassword('')
    setNewPassword('')
    setConfirmPassword('')
    setPasswordMessage('Senha alterada. Sessões antigas foram revogadas.')
  }

  return (
    <Layout user={user} onUserChange={onUserChange}>
      <section className="content-width section-space profile-grid">
        <div className="panel">
          <span className="eyebrow">PERFIL</span><h1>Dados da conta</h1><p className="muted">E-mail: {user?.email}</p>
          <form className="stack-form" onSubmit={saveProfile}>
            <label><span>Nome</span><input value={name} onChange={(event) => setName(event.target.value)} minLength={3} maxLength={100} /></label>
            <label><span>Data de nascimento</span><input type="date" value={birthDate} onChange={(event) => setBirthDate(event.target.value)} required /></label>
            <p className="field-help">Compras só podem ser realizadas por contas de pessoas com 18 anos ou mais.</p>
            <StatusMessage type={profileMessage.includes('sucesso') ? 'success' : 'error'}>{profileMessage}</StatusMessage>
            <button className="button-primary">Salvar perfil</button>
          </form>
        </div>
        <div className="panel">
          <span className="eyebrow">SEGURANÇA</span><h2>Alterar senha</h2>
          <form className="stack-form" onSubmit={changePassword}>
            <label><span>Senha atual</span><input type="password" autoComplete="current-password" value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} /></label>
            <label><span>Nova senha</span><input type="password" autoComplete="new-password" value={newPassword} onChange={(event) => setNewPassword(event.target.value)} /></label>
            <label><span>Confirmar nova senha</span><input type="password" autoComplete="new-password" value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} /></label>
            <p className="field-help">Mínimo 10 caracteres, maiúscula, minúscula, número e pelo menos quatro caracteres distintos.</p>
            <StatusMessage type={passwordMessage.includes('alterada') ? 'success' : 'error'}>{passwordMessage}</StatusMessage>
            <button className="button-primary">Alterar senha</button>
          </form>
        </div>
      </section>
    </Layout>
  )
}
