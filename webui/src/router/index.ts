import { ref } from "vue";
import { createRouter, createWebHistory } from "vue-router";
import HomeView from "@/pages/HomeView.vue";
import { ensureSession } from "@/stores/auth";
import { demoMode } from "@/stores/demo.ts";

/** 守卫确认会话与懒加载期间驱动整屏 loading */
export const routeLoading = ref(false);

const routes = [
  {
    path: "/",
    name: "HomeView",
    component: HomeView,
  },
  {
    path: "/login",
    name: "LoginView",
    // 非首页路由按需加载，避免把设置页、登录页的组件与依赖打进首屏
    component: () => import("@/pages/LoginView.vue"),
  },
  {
    path: "/settings",
    name: "SettingsView",
    component: () => import("@/pages/SettingsView.vue"),
    meta: { requiresAuth: true },
  },
];

const router = createRouter({
  history: createWebHistory(),
  routes,
});

router.beforeEach(async (to) => {
  // 演示站点没有可用的登录表单，直接回首页
  if (demoMode.value && to.path === "/login") return { path: "/" };

  if (!to.meta.requiresAuth) return true;

  // 网络慢时先给出整屏 loading 反馈
  routeLoading.value = true;

  // 访问令牌过期但刷新令牌有效时自动续期
  if (await ensureSession()) return true;

  return { path: "/login", query: { redirect: to.fullPath } };
});

router.afterEach(() => {
  routeLoading.value = false;
});

router.onError(() => {
  routeLoading.value = false;
});

// 刷新令牌过期或被吊销时退回登录页
window.addEventListener("auth:expired", () => {
  const current = router.currentRoute.value;
  if (current.meta.requiresAuth) {
    router.push({ path: "/login", query: { redirect: current.fullPath } });
  }
});

export default router;
