<script lang="ts" setup>
import { computed, ref } from "vue";
import { useThemeVars } from "naive-ui";
import ArrowClockwise16Regular from "@vicons/fluent/es/ArrowClockwise16Regular.js";
import Cube24Regular from "@vicons/fluent/es/Cube24Regular.js";
import DataUsage24Regular from "@vicons/fluent/es/DataUsage24Regular.js";
import Layer24Regular from "@vicons/fluent/es/Layer24Regular.js";
import Play24Regular from "@vicons/fluent/es/Play24Regular.js";
import Stop24Regular from "@vicons/fluent/es/Stop24Regular.js";
import TopSpeed24Regular from "@vicons/fluent/es/TopSpeed24Regular.js";
import { useDockerStatus } from "@/composables/useDockerStatus.ts";
import type { DockerCardConfig } from "@/cards/DockerCard/config.ts";

type TagType = "default" | "success" | "warning";

const STATE_LABELS: Record<string, string> = {
  running: "运行",
  paused: "暂停",
  restarting: "重启中",
  exited: "已停止",
  created: "已创建",
  dead: "异常",
};

const props = withDefaults(defineProps<Partial<DockerCardConfig>>(), {
  title: "Docker",
});

const { status, loading, failed, refresh } = useDockerStatus();
const themeVars = useThemeVars();

const showContainers = ref(false);

const primaryColor = computed(() => themeVars.value.primaryColor);
const successColor = computed(() => themeVars.value.successColor);
const errorColor = computed(() => themeVars.value.errorColor);
const infoColor = computed(() => themeVars.value.infoColor);

const iconTileStyle = computed(() => ({
  width: "40px",
  height: "40px",
  borderRadius: "10px",
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
  cursor: "pointer",
  background: `linear-gradient(135deg, ${primaryColor.value}26, ${infoColor.value}1f)`,
}));

const available = computed(() => {
  const value = status.value;
  return !failed.value && value?.enabled === true && value.available === true;
});

const detailsVisible = computed(() => status.value?.detailsVisible === true);

const counts = computed(() => ({
  total: status.value?.total ?? 0,
  running: status.value?.running ?? 0,
  stopped: status.value?.stopped ?? 0,
  images: status.value?.images ?? 0,
}));

const containers = computed(() => status.value?.containers ?? []);

const headerText = computed(() => {
  if (status.value?.enabled === false) return "已禁用";
  if (!available.value) return status.value || failed.value ? "未连接" : "读取中…";
  if (!showContainers.value) return `${counts.value.running}/${counts.value.total} 运行中`;
  return detailsVisible.value ? `${containers.value.length} 个容器` : "容器状态";
});

const stateText = computed(() => {
  if (status.value?.enabled === false) return "Docker 状态已禁用";
  if (status.value || failed.value) return "Docker 未连接";
  return "读取中…";
});

const cpuText = computed(() => {
  const raw = status.value?.cpuPercent;
  const value = raw == null
    ? (counts.value.running === 0 ? 0 : null)
    : Number(raw);
  return value == null || !Number.isFinite(value) ? "—" : `${value.toFixed(1)}%`;
});

const memoryText = computed(() => {
  const raw = status.value?.memoryBytes;
  const value = raw == null
    ? (counts.value.running === 0 ? 0 : null)
    : Number(raw);
  return formatBytes(value);
});

function toggleView() {
  showContainers.value = !showContainers.value;
}

function stateLabel(state: string) {
  return STATE_LABELS[state] ?? state;
}

function stateType(state: string): TagType {
  if (state === "running") return "success";
  if (state === "paused") return "warning";
  return "default";
}

function formatBytes(value: number | null | undefined) {
  if (value == null || !Number.isFinite(value)) return "—";
  if (value === 0) return "0 B";

  const units = ["B", "KB", "MB", "GB", "TB"];
  let result = value;
  let unitIndex = 0;

  while (result >= 1024 && unitIndex < units.length - 1) {
    result /= 1024;
    unitIndex += 1;
  }

  return `${result.toFixed(result >= 100 ? 0 : 1)} ${units[unitIndex]}`;
}
</script>

<template>
  <n-card :content-style="{ height: '100%' }" size="small" style="width: 100%; height: 100%">
    <n-flex :size="10" style="height: 100%" vertical>
      <n-flex :size="12" :wrap="false" align="center">
        <n-tooltip>
          <template #trigger>
            <div
              :aria-label="showContainers ? '返回概览' : '查看容器详情'"
              :style="iconTileStyle"
              role="button"
              tabindex="0"
              @click="toggleView"
              @keydown.enter="toggleView"
              @keydown.space.prevent="toggleView"
            >
              <n-icon :color="primaryColor" :component="Cube24Regular" :size="24" />
            </div>
          </template>
          {{ showContainers ? "返回概览" : "查看容器详情" }}
        </n-tooltip>

        <n-flex :size="0" style="flex: 1; min-width: 0" vertical>
          <n-text :depth="3" style="font-size: 11px; font-weight: 600">
            {{ props.title }}
          </n-text>
          <n-text strong style="font-size: 15px">{{ headerText }}</n-text>
        </n-flex>

        <n-tooltip>
          <template #trigger>
            <n-button aria-label="刷新 Docker 状态" :disabled="loading" quaternary @click="refresh">
              <template #icon>
                <n-icon
                    :class="{ 'ip-card__spin': loading }"
                    :component="ArrowClockwise16Regular"
                    :size="18"
                />
              </template>
            </n-button>
          </template>
          刷新
        </n-tooltip>
      </n-flex>

      <template v-if="showContainers">
        <n-flex v-if="!available" align="center" justify="center" style="flex: 1">
          <n-text :depth="3" style="font-size: 12px">{{ stateText }}</n-text>
        </n-flex>

        <n-flex v-else-if="!detailsVisible" align="center" justify="center" style="flex: 1">
          <n-text :depth="3" style="font-size: 12px">登录后查看容器状态</n-text>
        </n-flex>

        <n-flex v-else-if="containers.length === 0" align="center" justify="center" style="flex: 1">
          <n-empty description="没有容器" size="small" />
        </n-flex>

        <n-scrollbar v-else style="flex: 1 1 auto; min-height: 0; width: 100%">
          <n-flex :size="6" style="width: 100%" vertical>
            <n-tooltip
              v-for="item in containers"
              :key="item.id"
              :disabled="!item.status"
              placement="top"
            >
              <template #trigger>
                <n-card
                  :content-style="{ padding: '6px 10px' }"
                  embedded
                  size="small"
                  style="width: 100%"
                >
                  <n-flex :size="6" :wrap="false" align="center" style="width: 100%">
                    <n-tag :bordered="false" size="small" :type="stateType(item.state)">
                      {{ stateLabel(item.state) }}
                    </n-tag>
                    <n-ellipsis style="flex: 1; min-width: 0">
                      <n-text style="font-size: 12px">{{ item.name }}</n-text>
                    </n-ellipsis>
                    <n-ellipsis style="max-width: 40%; flex: 0 1 40%">
                      <n-text :depth="3" style="font-size: 11px">{{ item.image }}</n-text>
                    </n-ellipsis>
                  </n-flex>
                </n-card>
              </template>
              {{ item.status }}
            </n-tooltip>
          </n-flex>
        </n-scrollbar>
      </template>

      <template v-else>
        <n-flex v-if="!available" align="center" justify="center" style="flex: 1">
          <n-text :depth="3" style="font-size: 12px">{{ stateText }}</n-text>
        </n-flex>

        <template v-else>
          <n-grid :cols="3" :x-gap="8" style="flex: 1; grid-auto-rows: minmax(0, 1fr)">
            <n-gi>
              <n-card
                :content-style="{
                  padding: '8px 10px',
                  height: '100%',
                  display: 'flex',
                  alignItems: 'center',
                }"
                embedded
                size="small"
                style="height: 100%"
              >
                <n-flex :size="6" :wrap="false" align="center" style="width: 100%">
                  <n-icon :color="successColor" :component="Play24Regular" :size="16" />
                  <n-flex :size="0" align="center" style="flex: 1; min-width: 0" vertical>
                    <n-text :depth="3" style="font-size: 11px">运行中</n-text>
                    <n-text :style="{ color: successColor, fontSize: '16px' }" strong>
                      {{ counts.running }}
                    </n-text>
                  </n-flex>
                </n-flex>
              </n-card>
            </n-gi>

            <n-gi>
              <n-card
                :content-style="{
                  padding: '8px 10px',
                  height: '100%',
                  display: 'flex',
                  alignItems: 'center',
                }"
                embedded
                size="small"
                style="height: 100%"
              >
                <n-flex :size="6" :wrap="false" align="center" style="width: 100%">
                  <n-icon :color="errorColor" :component="Stop24Regular" :size="16" />
                  <n-flex :size="0" align="center" style="flex: 1; min-width: 0" vertical>
                    <n-text :depth="3" style="font-size: 11px">已停止</n-text>
                    <n-text :style="{ color: errorColor, fontSize: '16px' }" strong>
                      {{ counts.stopped }}
                    </n-text>
                  </n-flex>
                </n-flex>
              </n-card>
            </n-gi>

            <n-gi>
              <n-card
                :content-style="{
                  padding: '8px 10px',
                  height: '100%',
                  display: 'flex',
                  alignItems: 'center',
                }"
                embedded
                size="small"
                style="height: 100%"
              >
                <n-flex :size="6" :wrap="false" align="center" style="width: 100%">
                  <n-icon :color="infoColor" :component="Layer24Regular" :size="16" />
                  <n-flex :size="0" align="center" style="flex: 1; min-width: 0" vertical>
                    <n-text :depth="3" style="font-size: 11px">镜像</n-text>
                    <n-text :style="{ color: infoColor, fontSize: '16px' }" strong>
                      {{ counts.images }}
                    </n-text>
                  </n-flex>
                </n-flex>
              </n-card>
            </n-gi>
          </n-grid>

          <n-grid :cols="2" :x-gap="8" style="flex: 1; grid-auto-rows: minmax(0, 1fr)">
            <n-gi>
              <n-card
                :content-style="{
                  padding: '8px 10px',
                  height: '100%',
                  display: 'flex',
                  flexDirection: 'column',
                  justifyContent: 'center',
                }"
                embedded
                size="small"
                style="height: 100%"
              >
                <n-flex :size="2" style="width: 100%" vertical>
                  <n-flex :size="4" align="center">
                    <n-icon :color="infoColor" :component="TopSpeed24Regular" :size="14" />
                    <n-text :depth="3" style="font-size: 11px">CPU</n-text>
                  </n-flex>
                  <n-text strong style="font-size: 15px">{{ cpuText }}</n-text>
                </n-flex>
              </n-card>
            </n-gi>

            <n-gi>
              <n-card
                :content-style="{
                  padding: '8px 10px',
                  height: '100%',
                  display: 'flex',
                  flexDirection: 'column',
                  justifyContent: 'center',
                }"
                embedded
                size="small"
                style="height: 100%"
              >
                <n-flex :size="2" style="width: 100%" vertical>
                  <n-flex :size="4" align="center">
                    <n-icon :color="primaryColor" :component="DataUsage24Regular" :size="14" />
                    <n-text :depth="3" style="font-size: 11px">内存</n-text>
                  </n-flex>
                  <n-text strong style="font-size: 15px">{{ memoryText }}</n-text>
                </n-flex>
              </n-card>
            </n-gi>
          </n-grid>
        </template>
      </template>
    </n-flex>
  </n-card>
</template>

<style scoped>
.ip-card__spin {
  animation: ip-card-spin 0.9s linear infinite;
}

@keyframes ip-card-spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
