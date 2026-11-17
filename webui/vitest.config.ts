import { fileURLToPath, URL } from "node:url";
import { defineConfig } from "vitest/config";

// 测试专用配置：与主 vite.config.ts 保持相同的 @ 别名；
// 不复用主配置以避免引入 unplugin-vue-components 等构建期插件。
export default defineConfig({
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  test: {
    environment: "node",
    include: ["src/__tests__/**/*.spec.ts"],
  },
});
