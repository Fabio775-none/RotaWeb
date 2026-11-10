// Ícone vetorial frontal de ônibus usado na marca RotaWeb.
export function BusIcon({ size = 24 }) {
  return (
    <svg
      aria-hidden="true"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {/* Carroceria principal do ônibus. */}
      <rect x="5" y="3" width="14" height="15" rx="2" />
      {/* Para-brisa. */}
      <path d="M7 7h10" />
      {/* Faixa inferior. */}
      <path d="M5 12h14" />
      {/* Rodas. */}
      <path d="M8 18v2M16 18v2" />
      {/* Faróis. */}
      <path d="M8 15h.01M16 15h.01" />
    </svg>
  )
}

// Ícone simples de cadeado para telas de autenticação.
export function LockIcon({ size = 22 }) {
  return (
    <svg aria-hidden="true" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
      {/* Arco do cadeado. */}
      <path d="M7 10V7a5 5 0 0 1 10 0v3" />
      {/* Corpo do cadeado. */}
      <rect x="5" y="10" width="14" height="10" rx="2" />
    </svg>
  )
}

// Ícone de usuário usado no cadastro e perfil.
export function UserIcon({ size = 22 }) {
  return (
    <svg aria-hidden="true" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
      {/* Cabeça. */}
      <circle cx="12" cy="8" r="4" />
      {/* Ombros. */}
      <path d="M4 21a8 8 0 0 1 16 0" />
    </svg>
  )
}
