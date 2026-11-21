<script lang="ts" setup>
import { computed, onMounted, onUnmounted, ref } from "vue";
import { getAbout } from "@/apis/modules/about.ts";
import { checkUpdate } from "@/apis/modules/update.ts";
import { extractErrorMessage } from "@/apis/errors.ts";
import { NETWORK_ERROR } from "@/config/messages.ts";
import type { components } from "@/apis/schema";

/** 运行时长定时刷新，避免页面停留时数值不动 */
const REFRESH_INTERVAL = 30_000;

/** 字节数字段后端可能返回 number 或 string */
type AboutInfo = components["schemas"]["AboutResponse"];
type UpdateInfo = components["schemas"]["UpdateCheckResponse"];

const info = ref<AboutInfo | null>(null);
const loading = ref(true);
const failed = ref(false);

const update = ref<UpdateInfo | null>(null);
const updateChecking = ref(false);
const updateError = ref<string | null>(null);

const updateStatus = computed(() => {
  if (updateChecking.value) return "正在检查…";
  if (updateError.value) return updateError.value;
  if (!update.value) return "尚未检查";

  if (update.value.updateAvailable) {
    const published = formatDate(update.value.publishedAt);
    return `发现新版本 ${update.value.latestVersion}${published ? ` · ${published}` : ""}`;
  }

  // 开发版基于最近一个标签，核心版本相同时并不落后于该 Release
  if (update.value.currentVersion.includes("-dev")) {
    return `当前为开发版，已包含最新发布 ${update.value.latestVersion}`;
  }

  return `已是最新版本 ${update.value.latestVersion}`;
});

function formatDate(value: string | null): string {
  if (!value) return "";

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "" : date.toLocaleDateString();
}

function formatBytes(value: number | string): string {
  const bytes = Number(value);
  if (!Number.isFinite(bytes) || bytes <= 0) return "0 B";
  const units = ["B", "KB", "MB", "GB"];
  let size = bytes;
  let index = 0;
  while (size >= 1024 && index < units.length - 1) {
    size /= 1024;
    index += 1;
  }
  return `${size.toFixed(index === 0 ? 0 : 1)} ${units[index]}`;
}

function formatUptime(value: number | string): string {
  const seconds = Number(value);
  if (!Number.isFinite(seconds) || seconds <= 0) return "刚刚启动";

  const days = Math.floor(seconds / 86400);
  const hours = Math.floor((seconds % 86400) / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);

  if (days > 0) return `${days} 天 ${hours} 小时`;
  if (hours > 0) return `${hours} 小时 ${minutes} 分钟`;
  if (minutes > 0) return `${minutes} 分钟`;
  return `${seconds} 秒`;
}

function formatTime(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : date.toLocaleString();
}

function handleGoDownload() {
  window.open(update.value?.releaseUrl)
}

/** 接口未实现或请求失败时保持占位，不影响其他设置 */
async function loadAbout(silent = false) {
  if (!silent) loading.value = true;
  failed.value = false;

  try {
    const { data, response } = await getAbout();
    if (!response.ok || !data) {
      failed.value = true;
      return;
    }

    info.value = data;
  } catch {
    failed.value = true;
  } finally {
    if (!silent) loading.value = false;
  }
}

/** 后端缓存 30 分钟，手动检查只是绕过缓存并保留一分钟最小间隔 */
async function runUpdateCheck(refresh = false) {
  if (updateChecking.value) return;

  updateChecking.value = true;
  updateError.value = null;

  try {
    const { data, error, response } = await checkUpdate(refresh);

    if (!response.ok || !data) {
      updateError.value = extractErrorMessage(error, "检查更新失败", response.status);
      return;
    }

    update.value = data;
  } catch {
    updateError.value = NETWORK_ERROR;
  } finally {
    updateChecking.value = false;
  }
}

let refreshTimer: number | undefined;

onMounted(() => {
  void loadAbout();
  void runUpdateCheck();
  refreshTimer = window.setInterval(() => void loadAbout(true), REFRESH_INTERVAL);
});

onUnmounted(() => {
  if (refreshTimer !== undefined) window.clearInterval(refreshTimer);
});
</script>

<template>
  <h2 class="settings__heading">关于</h2>

  <n-card size="small">
    <n-spin :show="loading">
      <div v-if="failed" class="about__empty">无法读取关于信息，请确认已登录且服务正常</div>
      <template v-else-if="info">
        <div class="settings__row">
          <div class="settings__row-main">
            <div class="settings__row-label">版本</div>
          </div>
          <div class="about__value">{{ info.version }}</div>
        </div>

        <n-divider class="settings__divider" />

        <div class="settings__row">
          <div class="settings__row-main">
            <div class="settings__row-label">检查更新</div>
            <div class="settings__row-desc" :class="{ 'about__desc--error': updateError }">
              {{ updateStatus }}
            </div>
            <div v-if="update && update.updateAvailable" class="about__release">
              <pre v-if="update.releaseNotes" class="about__release-notes">{{
                update.releaseNotes
              }}</pre>
            </div>
          </div>
          <div class="settings__row-control">
            <n-flex>
              <n-button v-if="update?.releaseUrl" type="primary" :loading="updateChecking" @click="handleGoDownload"> 查看发布页 </n-button>
              <n-button :loading="updateChecking" @click="runUpdateCheck(true)"> 检查更新 </n-button>
            </n-flex>
          </div>
        </div>

        <n-divider class="settings__divider" />

        <div class="settings__row">
          <div class="settings__row-main">
            <div class="settings__row-label">运行环境</div>
          </div>
          <div class="about__value">{{ info.runtime }}</div>
        </div>

        <n-divider class="settings__divider" />

        <div class="settings__row">
          <div class="settings__row-main">
            <div class="settings__row-label">启动时间</div>
          </div>
          <div class="about__value">{{ formatTime(info.startedAt) }}</div>
        </div>

        <n-divider class="settings__divider" />

        <div class="settings__row">
          <div class="settings__row-main">
            <div class="settings__row-label">已运行</div>
          </div>
          <div class="about__value">{{ formatUptime(info.uptimeSeconds) }}</div>
        </div>

        <n-divider class="settings__divider" />

        <div class="settings__row">
          <div class="settings__row-main">
            <div class="settings__row-label">数据库大小</div>
          </div>
          <div class="about__value">{{ formatBytes(info.databaseBytes) }}</div>
        </div>

        <n-divider class="settings__divider" />

        <div class="settings__row">
          <div class="settings__row-main">
            <div class="settings__row-label">进程内存</div>
            <div class="settings__row-desc">
              {{ info.memory.isServerGc ? "Server GC" : "Workstation GC"
              }}{{ info.memory.isConcurrentGc ? " · 并发" : "" }} · 托管堆
              {{ formatBytes(info.memory.managedHeapBytes) }}
            </div>
          </div>
          <div class="about__value">
            {{ formatBytes(info.memory.workingSetBytes) }}
          </div>
        </div>

        <n-divider class="settings__divider" />

        <div class="settings__row">
          <div class="settings__row-main">
            <div class="settings__row-label">数据目录</div>
            <div class="settings__row-desc">数据库、证书与上传文件所在的可写目录</div>
          </div>
          <div :title="info.dataDirectory" class="about__value about__value--path">
            {{ info.dataDirectory }}
          </div>
        </div>

        <n-divider class="settings__divider" />

        <div class="settings__row">
          <div class="settings__row-main">
            <div class="settings__row-label">内容统计</div>
          </div>
          <div class="about__value">{{ info.cardCount }} 张卡片 · {{ info.imageCount }} 个素材</div>
        </div>
      </template>
      <div v-else class="about__empty">读取中…</div>
    </n-spin>
  </n-card>
</template>

<style scoped>
.about__value {
  flex: 0 0 auto;
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: 0.85rem;
  opacity: 0.75;
  text-align: right;
}

/* 目录可能很长：限制宽度并允许在任意位置换行，避免撑破卡片 */
.about__value--path {
  flex: 0 1 auto;
  max-width: 60%;
  overflow-wrap: anywhere;
}

.about__empty {
  padding: 12px 0;
  font-size: 0.85rem;
  opacity: 0.6;
}

.about__desc--error {
  color: #d03050;
  opacity: 1;
}

.about__release {
  margin-top: 8px;
}

/* Release 说明常是较长的 markdown 源码，按纯文本展示并限制高度，避免撑破卡片 */
.about__release-notes {
  max-height: 160px;
  margin: 0 0 8px;
  padding: 8px 10px;
  overflow: auto;
  font-family: inherit;
  font-size: 0.8rem;
  line-height: 1.5;
  white-space: pre-wrap;
  word-break: break-word;
  opacity: 0.75;
  background: rgba(128, 128, 128, 0.12);
  border-radius: 6px;
}

.about__release-link {
  font-size: 0.8rem;
}
</style>
