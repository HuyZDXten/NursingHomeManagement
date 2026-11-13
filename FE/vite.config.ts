import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
// Cổng 5173 khớp Cors:AllowedOrigins và PayOs:ReturnUrl trong appsettings của API.
export default defineConfig({
  plugins: [react()],
  server: { port: 5173, strictPort: true },
})
