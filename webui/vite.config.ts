import { fileURLToPath, URL } from "node:url";
import vue from "@vitejs/plugin-vue";
import { defineConfig } from "vite";
import Components from "unplugin-vue-components/vite";
import { NaiveUiResolver } from "unplugin-vue-components/resolvers";

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    Components({
      resolvers: [NaiveUiResolver()],
    }),
  ],
  resolve: {
    alias: {
      // 源码根目录别名，import 可写作 @/xxx
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  build: {
    outDir: "../src/wwwroot",
    rolldownOptions: {
      output: {
        codeSplitting: {
          groups: [
            // 拖拽/缩放引擎体积较大且很少变动，独立成 chunk 便于长期缓存
            {
              name: "grid-vendor",
              test: /node_modules[\\/](grid-layout-plus|interactjs)[\\/]/,
            },
          ],
        },
      },
    },
  },
  server:
    process.env.NODE_ENV === "development"
      ? {
          host: "0.0.0.0",
          proxy: {
            "/api": {
              target: "http://127.0.0.1:5035",
              // 保留 Host 与 Origin 一致，系统状态 WebSocket 会校验 Origin
              changeOrigin: false,
              // 允许代理 WebSocket 升级请求
              ws: true,
              secure: false, // 禁用 SSL 验证
            },
            // 后端存放的上传图标
            "/uploads": {
              target: "http://127.0.0.1:5035",
              changeOrigin: true,
              secure: false, // 禁用 SSL 验证
            },
          },
        }
      : undefined,
});
