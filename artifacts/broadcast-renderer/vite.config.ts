import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

const captureProxy = {
  "/api": {
    target: "http://localhost:5000",
    changeOrigin: true,
  },
  "/hubs": {
    target: "http://localhost:5000",
    changeOrigin: true,
    ws: true,
  },
};

export default defineConfig({
  plugins: [react()],
  server: {
    host: "0.0.0.0",
    port: 5001,
    strictPort: true,
    proxy: captureProxy,
  },
  preview: {
    host: "0.0.0.0",
    port: 5001,
    strictPort: true,
    proxy: captureProxy,
  },
});
