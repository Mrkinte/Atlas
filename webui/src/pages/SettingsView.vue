<script lang="ts" setup>
import { onMounted, ref } from "vue";
import { type MenuOption } from "naive-ui";
import Color24Regular from "@vicons/fluent/es/Color24Regular.js";
import Teddy24Regular from "@vicons/fluent/es/Teddy24Regular.js";
import ArrowDownload24Regular from "@vicons/fluent/es/ArrowDownload24Regular.js";
import Desktop24Regular from "@vicons/fluent/es/Desktop24Regular.js";
import Grid24Regular from "@vicons/fluent/es/Grid24Regular.js";
import Options24Regular from "@vicons/fluent/es/Options24Regular.js";
import Person24Regular from "@vicons/fluent/es/Person24Regular.js";
import ShieldLock24Regular from "@vicons/fluent/es/ShieldLock24Regular.js";
import ErrorCircle24Regular from "@vicons/fluent/es/ErrorCircle24Regular.js";
import { loadSettings } from "@/stores/settings.ts";
import AboutSettings from "@/components/settings/AboutSettings.vue";
import AccountSettings from "@/components/settings/AccountSettings.vue";
import AppearanceSettings from "@/components/settings/AppearanceSettings.vue";
import BackupSettings from "@/components/settings/BackupSettings.vue";
import HttpsSettings from "@/components/settings/HttpsSettings.vue";
import Live2dSettings from "@/components/settings/Live2dSettings.vue";
import SystemStatusSettings from "@/components/settings/SystemStatusSettings.vue";
import { platform } from "@/config/platform.ts";
import { iconConverter } from "@/utils/iconConverter.ts";
import LayoutSettings from "@/components/settings/LayoutSettings.vue";
import BasicSettings from "@/components/settings/BasicSettings.vue";

type GroupKey =
  "general" | "appearance" | "layout" | "live2d" | "system" | "backup" | "https" | "account";

const collapsed = ref(platform.value === "mobile");
const activeGroup = ref<GroupKey>("general");

const menuOptions: MenuOption[] = [
  {
    label: "基础",
    key: "general",
    icon: iconConverter(Options24Regular),
  },
  {
    label: "外观",
    key: "appearance",
    icon: iconConverter(Color24Regular),
  },
  {
    label: "布局",
    key: "layout",
    icon: iconConverter(Grid24Regular),
  },
  {
    label: "看板娘",
    key: "live2d",
    icon: iconConverter(Teddy24Regular),
  },
  {
    label: "系统状态",
    key: "system",
    icon: iconConverter(Desktop24Regular),
  },
  {
    label: "备份",
    key: "backup",
    icon: iconConverter(ArrowDownload24Regular),
  },
  {
    label: "HTTPS",
    key: "https",
    icon: iconConverter(ShieldLock24Regular),
  },
  {
    label: "账户安全",
    key: "account",
    icon: iconConverter(Person24Regular),
  },
  {
    label: "关于",
    key: "about",
    icon: iconConverter(ErrorCircle24Regular),
  },
];

function handleGroupSelect(key: string) {
  activeGroup.value = key as GroupKey;
}

// 进入设置页时拉取服务端配置（启动时可能尚未登录）
onMounted(() => {
  void loadSettings();
});
</script>

<template>
  <n-layout class="settings" has-sider>
    <n-layout-sider
      :collapsed="collapsed"
      :collapsed-width="64"
      :width="220"
      bordered
      collapse-mode="width"
      show-trigger="bar"
      @update:collapsed="collapsed = $event"
    >
      <n-menu
        :collapsed="collapsed"
        :collapsed-icon-size="22"
        :collapsed-width="64"
        :options="menuOptions"
        :value="activeGroup"
        @update:value="handleGroupSelect"
      />
    </n-layout-sider>

    <n-layout-content :native-scrollbar="false" class="settings__content">
      <div class="settings__panel">
        <BasicSettings v-if="activeGroup === 'general'" />

        <AppearanceSettings v-else-if="activeGroup === 'appearance'" />

        <LayoutSettings v-else-if="activeGroup === 'layout'" />

        <Live2dSettings v-else-if="activeGroup === 'live2d'" />

        <SystemStatusSettings v-else-if="activeGroup === 'system'" />

        <HttpsSettings v-else-if="activeGroup === 'https'" />

        <BackupSettings v-else-if="activeGroup === 'backup'" />

        <AccountSettings v-else-if="activeGroup === 'account'" />

        <AboutSettings v-else />
      </div>
    </n-layout-content>
  </n-layout>
</template>

<style src="@/components/settings/settings-shared.css"></style>

<style scoped>
.settings {
  height: 100%;
}

.settings__content {
  height: 100%;
}

.settings__panel {
  max-width: 720px;
  margin: 0 auto;
  padding: 24px;
}

/* 缩短 Naive UI 默认 72px 的折叠触发条 */
.settings :deep(.n-layout-toggle-bar) {
  height: 48px;
  top: calc(50% - 24px);
}

.settings :deep(.n-layout-toggle-bar__top),
.settings :deep(.n-layout-toggle-bar__bottom) {
  height: 26px;
}

.settings :deep(.n-layout-toggle-bar__bottom) {
  top: 22px;
}
</style>
