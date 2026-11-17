import "@/cards";
import { createApp } from "vue";
import "@/style.css";
import App from "@/App.vue";
import router from "@/router";
import { startAuthSync } from "@/stores/auth.ts";
import { bootstrapDemo } from "@/stores/demo.ts";
import { loadSettings } from "@/stores/settings.ts";

async function bootstrap() {
  // 演示站点先完成自动登录，路由守卫才能放行设置页
  await bootstrapDemo();
  startAuthSync();
  // 站点配置以后端为准，接口未实现时保持默认值
  void loadSettings();

  createApp(App).use(router).mount("#app");
}

void bootstrap();
