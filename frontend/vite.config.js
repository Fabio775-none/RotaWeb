import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Configuração do Vite usada no desenvolvimento local.
export default defineConfig({
  // Habilita transformação JSX e Fast Refresh do React.
  plugins: [react()],

  // Mantém o frontend na porta usada durante as aulas.
  server: {
    port: 5173,

    // O proxy faz o navegador enxergar /api como mesma origem.
    // Isso simplifica cookies HttpOnly, SameSite e proteção CSRF no desenvolvimento.
    proxy: {
      '/api': {
        target: 'http://localhost:5263',
        changeOrigin: false,
      },
    },
  },
})
