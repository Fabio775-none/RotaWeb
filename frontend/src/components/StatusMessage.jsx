// Componente visual reutilizável para sucesso, erro e informação.
export default function StatusMessage({ type = 'info', children }) {
  // Não renderiza caixa vazia.
  if (!children) {
    return null
  }

  // A classe define cor e borda sem depender apenas da cor para comunicar estado.
  return (
    <div className={`status-message status-${type}`} role={type === 'error' ? 'alert' : 'status'}>
      {children}
    </div>
  )
}
