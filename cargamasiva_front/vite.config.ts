import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Mismo origen (constitución §11, Feature 011 Q2): en desarrollo, Vite reenvía /auth y /cargas al Gateway.
// La URL del Gateway es configuración (GATEWAY_URL); el código de la SPA nunca la contiene.
const gateway = process.env.GATEWAY_URL ?? 'http://localhost:8080'
console.info(`[frontend] /auth y /cargas se reenvían a ${gateway}`)

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      '/auth': { target: gateway, changeOrigin: true },
      '/cargas': { target: gateway, changeOrigin: true },
    },
  },
})
