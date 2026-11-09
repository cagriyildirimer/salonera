import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Tauri geliştirme sunucusu için sabit port.
export default defineConfig({
  plugins: [react()],
  clearScreen: false,
  server: { port: 5173, strictPort: true },
  build: { target: "es2022", chunkSizeWarningLimit: 1200 },
});
