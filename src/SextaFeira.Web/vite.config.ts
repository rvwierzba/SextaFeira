import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Em produção o HUD é servido pelo próprio backend (wwwroot), então tudo usa caminhos relativos.
// Em desenvolvimento (npm run dev) as chamadas /api e /hubs vão para o backend em localhost:5000.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../SextaFeira.WebApi/wwwroot',
    emptyOutDir: true,
    chunkSizeWarningLimit: 1500,
  },
  server: {
    port: 5173,
    host: true,
    proxy: {
      '/api': 'http://localhost:5000',
      '/hubs': { target: 'http://localhost:5000', ws: true },
    },
  },
});
