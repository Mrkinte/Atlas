<script lang="ts" setup>
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import Settings24Regular from "@vicons/fluent/es/Settings24Regular.js";
import AppRecent24Regular from "@vicons/fluent/es/AppRecent24Regular.js";
import Desktop24Regular from "@vicons/fluent/es/Desktop24Regular.js";
import WeatherMoon24Filled from "@vicons/fluent/es/WeatherMoon24Filled.js";
import WeatherSunny24Regular from "@vicons/fluent/es/WeatherSunny24Regular.js";
import { setThemeMode, themeMode, type ThemeMode } from "@/stores/theme.ts";
import { demoMode } from "@/stores/demo.ts";
import { settings } from "@/stores/settings.ts";

const THEME_MODE_CYCLE: ThemeMode[] = ["system", "light", "dark"];

const THEME_MODE_LABELS: Record<ThemeMode, string> = {
  system: "跟随系统",
  light: "浅色",
  dark: "深色",
};

const route = useRoute();
const router = useRouter();

const isHomeRoute = computed(() => route.path === "/");

function handleGotoHome() {
  router.push("/");
}

/** 登录校验由 /settings 的路由守卫处理 */
function handleGotoSettings() {
  router.push("/settings");
}

function handleHeaderAction() {
  if (isHomeRoute.value) {
    handleGotoSettings();
  } else {
    handleGotoHome();
  }
}

function handleCycleTheme() {
  const index = THEME_MODE_CYCLE.indexOf(themeMode.value);
  setThemeMode(THEME_MODE_CYCLE[(index + 1) % THEME_MODE_CYCLE.length]);
}
</script>

<template>
  <div class="main-shell">
    <n-layout>
      <n-layout-header class="main-header">
        <n-flex vertical>
          <n-flex :size="12" align="center" @click="handleGotoHome">
            <img
              v-if="settings.showHeaderLogo"
              :src="settings.favicon || '/favicon.svg'"
              :alt="settings.siteTitle"
              class="main-header__logo"
            />
            <div class="main-header__brand">
              {{ settings.siteTitle }}
            </div>
          </n-flex>
        </n-flex>
        <n-flex align="center">
          <n-tag v-if="demoMode" :bordered="false" size="small" type="warning">
            演示模式 · 修改不会保存
          </n-tag>
          <n-button quaternary @click="handleHeaderAction">
            <template #icon>
              <n-icon :size="24">
                <Settings24Regular v-if="isHomeRoute" />
                <AppRecent24Regular v-else />
              </n-icon>
            </template>
          </n-button>
          <n-tooltip>
            <template #trigger>
              <n-button quaternary @click="handleCycleTheme">
                <template #icon>
                  <n-icon :size="24">
                    <Desktop24Regular v-if="themeMode === 'system'" />
                    <WeatherSunny24Regular v-else-if="themeMode === 'light'" />
                    <WeatherMoon24Filled v-else />
                  </n-icon>
                </template>
              </n-button>
            </template>
            主题：{{ THEME_MODE_LABELS[themeMode] }}
          </n-tooltip>
        </n-flex>
      </n-layout-header>
      <n-layout-content :class="{ 'main--with-footer': settings.footerText }" class="main">
        <router-view v-slot="{ Component }">
          <keep-alive :include="['HomeView']">
            <component :is="Component" />
          </keep-alive>
        </router-view>
      </n-layout-content>
      <!-- 页脚放在内容区之外，避免挤占页面自身的高度计算 -->
      <footer v-if="settings.footerText" class="main__footer">
        {{ settings.footerText }}
      </footer>
    </n-layout>
  </div>
</template>

<style scoped>
.main-shell {
  width: 100vw;
  height: 100vh;
  display: flex;
}

.main-header {
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 24px;
}

.main-header__logo {
  flex: none;
  width: 32px;
  height: 32px;
  object-fit: contain;
  cursor: pointer;
}

.main-header__brand {
  font-size: 2rem;
  cursor: pointer;
}

.main {
  height: calc(100% - 48px);
}

/* 显示页脚时让出页脚高度，页面内部的高度计算不受影响 */
.main--with-footer {
  height: calc(100% - 48px - 34px);
}

.main__footer {
  box-sizing: border-box;
  height: 34px;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0 24px env(safe-area-inset-bottom);
  font-size: 0.78rem;
  line-height: 1;
  opacity: 0.55;
}
</style>
