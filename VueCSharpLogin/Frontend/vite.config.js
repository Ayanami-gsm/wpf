import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    // 开发环境代理：/api 开头的请求转发到 C# 后端
    proxy: {
      '/api': {
        target: 'http://localhost:5161',
        changeOrigin: true
      }
    }
  }
})
