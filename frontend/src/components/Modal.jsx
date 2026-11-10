// Janela modal simples usada nos formulários administrativos.
export default function Modal({ title, onClose, children }) {
  // Fecha ao clicar no fundo, mas impede o clique interno de propagar.
  return (
    <div className="modal-backdrop" role="presentation" onMouseDown={onClose}>
      <section className="modal-panel" role="dialog" aria-modal="true" aria-label={title} onMouseDown={(event) => event.stopPropagation()}>
        {/* Cabeçalho com título e ação explícita de fechar. */}
        <div className="modal-header">
          <h2>{title}</h2>
          <button type="button" className="button-secondary button-small" onClick={onClose}>
            Fechar
          </button>
        </div>

        {/* Conteúdo fornecido pela tela chamadora. */}
        {children}
      </section>
    </div>
  )
}
