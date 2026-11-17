<script lang="ts" setup>
import { computed, defineAsyncComponent, onMounted, ref, watch, watchEffect } from "vue";
import MainWindow from "@/components/MainWindow.vue";
import { dateZhCN, type GlobalThemeOverrides, zhCN } from "naive-ui";
import CardEditorHost from "@/components/hosts/CardEditorHost.vue";
import { appTheme } from "@/stores/theme.ts";
import { settings, whenSettingsLoaded } from "@/stores/settings.ts";
import ImageLibraryHost from "@/components/hosts/ImageLibraryHost.vue";
import { watchDevicePlatform } from "@/config/platform.ts";
import { buildPrimaryThemeColors } from "@/utils/colorConverter.ts";
import { routeLoading } from "@/router";

/** 受保护页面跳转需等待接口确认会话，较慢时用整屏 loading 给出反馈 */
const showRouteLoading = ref(false);
let routeLoadingTimer: number | undefined;

watch(routeLoading, (loading) => {
  window.clearTimeout(routeLoadingTimer);
  if (!loading) {
    showRouteLoading.value = false;
    return;
  }

  // 延迟显示，避免快速跳转时遮罩只闪一下
  routeLoadingTimer = window.setTimeout(() => {
    showRouteLoading.value = true;
  }, 150);
});

/** 按需加载看板娘：禁用时连组件代码一起跳过 */
const Live2DStage = defineAsyncComponent(() => import("@/components/live2d/Live2DStage.vue"));

/** 配置到达后才决定是否挂载看板娘，避免禁用时先加载相关代码 */
const settingsReady = ref(false);

onMounted(async () => {
  await Promise.race([
    whenSettingsLoaded(),
    new Promise<void>((resolve) => setTimeout(resolve, 3000)),
  ]);
  settingsReady.value = true;
});

watchDevicePlatform();

/** 网站标题随配置实时生效 */
watchEffect(() => {
  document.title = settings.siteTitle;
});

/** 站点图标随配置实时生效；留空时使用内置图标 */
watchEffect(() => {
  const url = settings.favicon || "/favicon.svg";

  let link = document.querySelector<HTMLLinkElement>('link[rel="icon"]');
  if (!link) {
    link = document.createElement("link");
    link.rel = "icon";
    document.head.appendChild(link);
  }

  link.href = url;
  if (url.endsWith(".svg")) {
    link.type = "image/svg+xml";
  } else {
    link.removeAttribute("type");
  }
});

/** 背景图样式；模糊时向外扩展，避免边缘出现透明渐隐 */
const backgroundStyle = computed(() => {
  const blur = settings.backgroundBlur;
  return {
    backgroundImage: `url("${settings.backgroundImage}")`,
    filter: blur > 0 ? `blur(${blur}px)` : "none",
    inset: blur > 0 ? `${-blur * 2}px` : "0",
    "--background-mask-opacity": `${settings.backgroundMask / 100}`,
  };
});

/** 背景图存在时让应用容器透明，露出底层背景 */
watchEffect(() => {
  document.documentElement.classList.toggle("has-background", Boolean(settings.backgroundImage));
});

/**
 * 主题变量：主色留空时完全沿用 Naive UI 官方默认；
 * 配置后按当前明暗主题生成 hover / pressed / suppl，避免组件残留默认色
 */
const themeOverrides = computed<GlobalThemeOverrides>(() => ({
  common: {
    borderRadius: "8px",
    ...(settings.primaryColor
      ? buildPrimaryThemeColors(settings.primaryColor, appTheme.value.name === "dark")
      : {}),
  },
}));
</script>

<template>
  <n-config-provider
    :date-locale="dateZhCN"
    :locale="zhCN"
    :theme="appTheme"
    :theme-overrides="themeOverrides"
  >
    <div
      v-if="settings.backgroundImage"
      :class="{ 'app-background--light': appTheme.name === 'light' }"
      :style="backgroundStyle"
      class="app-background"
    />
    <n-dialog-provider>
      <n-message-provider>
        <ImageLibraryHost />
        <CardEditorHost />
        <MainWindow />
        <Live2DStage v-if="settingsReady && settings.live2DEnabled" />
      </n-message-provider>
    </n-dialog-provider>
    <div
      v-if="showRouteLoading"
      :class="{ 'route-loading--dark': appTheme.name === 'dark' }"
      class="route-loading"
    >
      <n-spin size="large" />
    </div>
  </n-config-provider>
</template>

<style>
/* 路由切换整屏遮罩：覆盖页面与顶栏，同时阻止重复点击 */
.route-loading {
  position: fixed;
  inset: 0;
  z-index: 3000;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: rgba(255, 255, 255, 0.66);
  -webkit-backdrop-filter: blur(2px);
  backdrop-filter: blur(2px);
}

.route-loading--dark {
  background-color: rgba(16, 16, 20, 0.66);
}

/* 有背景图时，面状容器统一使用半透明背景与模糊；侧边栏在全局样式中单独配置 */
html.has-background
  :is(
    .n-card,
    .n-dialog,
    .n-drawer,
    .n-notification,
    .n-message,
    .n-popover,
    .n-dropdown-menu,
    .n-base-select-menu
  ) {
  background-color: color-mix(in srgb, var(--n-color) 72%, transparent);
  -webkit-backdrop-filter: blur(12px);
  backdrop-filter: blur(12px);
}
</style>
