<script lang="ts" setup>
import { computed } from "vue";
import { useSystemStatus } from "@/composables/useSystemStatus.ts";
import type { SystemStatusCardConfig } from "@/cards/SystemStatusCard/config.ts";

/** 历史数据可能缺字段，全部可选并由默认值补齐 */
const props = withDefaults(defineProps<Partial<SystemStatusCardConfig>>(), {
  title: "系统状态",
  variant: "classic",
  showMemory: true,
  showDisk: true,
  showLoad: true,
  showProcess: true,
  showUptime: true,
});

const { state, devices } = useSystemStatus();

const device = computed(() => devices.value[0] ?? null);

const cpuPercent = computed(() => clampPercent(device.value?.cpuUsagePercent));
const memoryPercent = computed(() =>
  clampPercent(ratio(device.value?.memoryUsedBytes, device.value?.memoryTotalBytes)),
);
const diskPercent = computed(() =>
  clampPercent(
    ratio(
      device.value && device.value.diskTotalBytes !== null && device.value.diskFreeBytes !== null
        ? device.value.diskTotalBytes - device.value.diskFreeBytes
        : null,
      device.value?.diskTotalBytes,
    ),
  ),
);

type BarMetric = {
  key: string;
  label: string;
  percent: number;
};

const barMetrics = computed<BarMetric[]>(() => {
  const items: BarMetric[] = [{ key: "cpu", label: "CPU", percent: cpuPercent.value }];

  if (props.showMemory) {
    items.push({ key: "memory", label: "内存", percent: memoryPercent.value });
  }
  if (props.showDisk) {
    items.push({ key: "disk", label: "磁盘", percent: diskPercent.value });
  }

  return items;
});

/** 极简样式只展示 CPU 以外的指标 */
const detailMetrics = computed(() => barMetrics.value.filter((item) => item.key !== "cpu"));

const RING_CIRCUMFERENCE = 2 * Math.PI * 20;
const SEGMENT_COUNT = 20;

function ratio(used: number | null | undefined, total: number | null | undefined) {
  if (used == null || total == null || total <= 0) return null;
  return (used / total) * 100;
}

function clampPercent(value: number | null | undefined) {
  if (value == null || !Number.isFinite(value)) return 0;
  return Math.min(100, Math.max(0, value));
}

/** 占用率分档，供进度条、环形和分段条配色 */
function levelOf(percent: number) {
  if (percent >= 85) return "high";
  if (percent >= 60) return "mid";
  return "low";
}

function ringOffset(percent: number) {
  return RING_CIRCUMFERENCE * (1 - clampPercent(percent) / 100);
}

function filledSegments(percent: number) {
  return Math.round((clampPercent(percent) / 100) * SEGMENT_COUNT);
}

function formatPercent(value: number | null | undefined) {
  if (value == null || !Number.isFinite(value)) return "—";
  return `${value.toFixed(0)}%`;
}

function formatBytes(value: number | null | undefined) {
  if (value == null || !Number.isFinite(value)) return "—";

  const units = ["B", "KB", "MB", "GB", "TB"];
  let result = value;
  let unitIndex = 0;

  while (result >= 1024 && unitIndex < units.length - 1) {
    result /= 1024;
    unitIndex += 1;
  }

  return `${result.toFixed(result >= 100 ? 0 : 1)} ${units[unitIndex]}`;
}

function formatUptime(seconds: number | null | undefined) {
  if (seconds == null || !Number.isFinite(seconds)) return "—";

  const days = Math.floor(seconds / 86400);
  const hours = Math.floor((seconds % 86400) / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);

  if (days > 0) return `${days} 天 ${hours} 小时`;
  if (hours > 0) return `${hours} 小时 ${minutes} 分`;
  return `${minutes} 分`;
}

function formatLoad(value: number | null | undefined) {
  return value == null || !Number.isFinite(value) ? "—" : value.toFixed(2);
}
</script>

<template>
  <div :class="`system-status--${props.variant}`" class="system-status">
    <div class="system-status__header">
      <span class="system-status__title">{{ props.title }}</span>
      <span v-if="device?.hardwareName" class="system-status__host">
        {{ device.hardwareName }}
      </span>
      <span :class="`system-status__dot--${state}`" class="system-status__dot" />
    </div>

    <div v-if="state === 'disabled'" class="system-status__hint">已禁用，可在设置中开启</div>
    <div v-else-if="state === 'denied'" class="system-status__hint">
      未公开，登录后查看或在设置中公开本机
    </div>
    <div v-else-if="!device" class="system-status__hint">
      {{ state === "offline" ? "连接中断，正在重试…" : "正在连接…" }}
    </div>

    <template v-else>
      <div v-if="props.variant === 'classic'" class="system-status__grid">
        <div v-for="metric in barMetrics" :key="metric.key" class="system-status__metric">
          <div class="system-status__metric-head">
            <span>{{ metric.label }}</span>
            <span
              :class="`system-status__tone--${levelOf(metric.percent)}`"
              class="system-status__value"
            >
              {{ formatPercent(metric.percent) }}
            </span>
          </div>
          <div class="system-status__bar">
            <i
              :class="`system-status__tone--${levelOf(metric.percent)}`"
              :style="{ width: `${metric.percent}%` }"
              class="system-status__fill"
            />
          </div>
        </div>

        <div v-if="props.showLoad" class="system-status__metric">
          <div class="system-status__metric-head">
            <span>负载</span>
            <span class="system-status__value">
              {{ formatLoad(device?.loadAverage1m) }}
            </span>
          </div>
        </div>
      </div>

      <div v-else-if="props.variant === 'minimal'" class="system-status__minimal">
        <div class="system-status__minimal-hero">
          <div
            :class="`system-status__tone--${levelOf(cpuPercent)}`"
            class="system-status__minimal-cpu"
          >
            {{ formatPercent(device?.cpuUsagePercent) }}
          </div>
          <div class="system-status__minimal-caption">CPU 使用率</div>
          <div class="system-status__bar">
            <i
              :class="`system-status__tone--${levelOf(cpuPercent)}`"
              :style="{ width: `${cpuPercent}%` }"
              class="system-status__fill"
            />
          </div>
        </div>

        <div class="system-status__minimal-stats">
          <div
            v-for="metric in detailMetrics"
            :key="metric.key"
            class="system-status__minimal-stat"
          >
            <span class="system-status__minimal-label">{{ metric.label }}</span>
            <div class="system-status__minimal-bar">
              <i
                :class="`system-status__tone--${levelOf(metric.percent)}`"
                :style="{ width: `${metric.percent}%` }"
              />
            </div>
            <span class="system-status__minimal-value">
              {{ formatPercent(metric.percent) }}
            </span>
          </div>

          <div v-if="props.showLoad" class="system-status__minimal-stat">
            <span class="system-status__minimal-label">负载</span>
            <div class="system-status__minimal-bar system-status__minimal-bar--empty" />
            <span class="system-status__minimal-value">
              {{ formatLoad(device?.loadAverage1m) }}
            </span>
          </div>
        </div>
      </div>

      <div v-else-if="props.variant === 'ring'" class="system-status__ring">
        <div v-for="metric in barMetrics" :key="metric.key" class="system-status__ring-item">
          <div class="system-status__ring-wrap">
            <svg aria-hidden="true" class="system-status__ring-svg" viewBox="0 0 48 48">
              <circle class="system-status__ring-track" cx="24" cy="24" r="20" />
              <circle
                :class="`system-status__tone--${levelOf(metric.percent)}`"
                :stroke-dasharray="RING_CIRCUMFERENCE"
                :stroke-dashoffset="ringOffset(metric.percent)"
                class="system-status__ring-fill"
                cx="24"
                cy="24"
                r="20"
              />
            </svg>
            <span class="system-status__ring-center">
              {{ formatPercent(metric.percent) }}
            </span>
          </div>
          <span class="system-status__ring-label">{{ metric.label }}</span>
        </div>

        <div v-if="props.showLoad" class="system-status__ring-load">
          负载 {{ formatLoad(device?.loadAverage1m) }}
        </div>
      </div>

      <div v-else class="system-status__segments">
        <div v-for="metric in barMetrics" :key="metric.key" class="system-status__segment">
          <span class="system-status__segment-label">{{ metric.label }}</span>
          <div
            :class="`system-status__tone--${levelOf(metric.percent)}`"
            class="system-status__segment-track"
          >
            <i
              v-for="index in SEGMENT_COUNT"
              :key="index"
              :class="{ 'is-on': index <= filledSegments(metric.percent) }"
            />
          </div>
          <span class="system-status__segment-value">
            {{ formatPercent(metric.percent) }}
          </span>
        </div>

        <div v-if="props.showLoad" class="system-status__segment">
          <span class="system-status__segment-label">负载</span>
          <div class="system-status__segment-track system-status__segment-track--empty" />
          <span class="system-status__segment-value">
            {{ formatLoad(device?.loadAverage1m) }}
          </span>
        </div>
      </div>

      <div v-if="props.showProcess || props.showUptime" class="system-status__footer">
        <span v-if="props.showProcess">
          进程 {{ formatBytes(device?.processWorkingSetBytes) }}
        </span>
        <span v-if="props.showUptime"> 已运行 {{ formatUptime(device?.uptimeSeconds) }} </span>
      </div>
    </template>
  </div>
</template>

<style scoped>
.system-status {
  container-type: size;
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
  min-height: 64px;
  overflow: hidden;
  color: inherit;
}

.system-status__header {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
  font-size: 0.95rem;
  font-weight: 600;
}

.system-status__title {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.system-status__host {
  margin-left: auto;
  overflow: hidden;
  font-size: 0.7rem;
  font-weight: 400;
  opacity: 0.55;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.system-status__dot {
  flex: 0 0 auto;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #999;
}

.system-status__dot--online {
  background: #18a058;
}

.system-status__dot--connecting {
  background: #f0a020;
}

.system-status__dot--offline {
  background: #d03050;
}

.system-status__dot--denied,
.system-status__dot--disabled {
  background: #999;
}

.system-status__hint {
  display: flex;
  flex: 1;
  align-items: center;
  justify-content: center;
  padding: 8px;
  font-size: 0.78rem;
  opacity: 0.65;
  text-align: center;
}

.system-status__tone--low {
  color: #18a058;
}

.system-status__tone--mid {
  color: #f0a020;
}

.system-status__tone--high {
  color: #d03050;
}

.system-status__grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px 14px;
  margin-top: 8px;
}

.system-status__metric {
  min-width: 0;
}

.system-status__metric-head {
  display: flex;
  justify-content: space-between;
  gap: 4px;
  font-size: 0.7rem;
  opacity: 0.8;
}

.system-status__value {
  font-variant-numeric: tabular-nums;
}

.system-status__bar {
  height: 5px;
  margin-top: 4px;
  overflow: hidden;
  border-radius: 3px;
  background: rgba(128, 128, 128, 0.22);
}

.system-status__fill {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: currentColor;
  opacity: 0.85;
  transition: width 0.35s ease;
}

.system-status__minimal {
  display: flex;
  flex: 1;
  align-items: stretch;
  gap: 16px;
  min-height: 0;
  margin-top: 6px;
}

.system-status__minimal-hero {
  display: flex;
  flex: 1 1 52%;
  flex-direction: column;
  justify-content: center;
  min-width: 0;
}

.system-status__minimal-cpu {
  font-size: clamp(1.6rem, 14cqw, 2.8rem);
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  line-height: 1.05;
}

.system-status__minimal-caption {
  margin-top: 2px;
  font-size: 0.62rem;
  letter-spacing: 0.08em;
  opacity: 0.55;
  text-transform: uppercase;
}

.system-status__minimal-stats {
  display: flex;
  flex: 1 1 48%;
  flex-direction: column;
  justify-content: center;
  gap: 7px;
  min-width: 0;
}

.system-status__minimal-stat {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 0.7rem;
}

.system-status__minimal-label {
  flex: 0 0 auto;
  width: 2.4em;
  opacity: 0.65;
}

.system-status__minimal-bar {
  flex: 1;
  height: 3px;
  overflow: hidden;
  border-radius: 2px;
  background: rgba(128, 128, 128, 0.22);
}

.system-status__minimal-bar > i {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: currentColor;
  opacity: 0.85;
  transition: width 0.35s ease;
}

.system-status__minimal-bar--empty {
  background: transparent;
}

.system-status__minimal-value {
  flex: 0 0 auto;
  width: 2.6em;
  font-variant-numeric: tabular-nums;
  text-align: right;
}

.system-status__ring {
  display: flex;
  flex: 1;
  flex-wrap: wrap;
  align-items: center;
  justify-content: center;
  gap: 10px 18px;
  min-height: 0;
  margin-top: 6px;
}

.system-status__ring-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
}

.system-status__ring-wrap {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
}

.system-status__ring-svg {
  display: block;
  width: 56px;
  height: 56px;
}

.system-status__ring-svg circle {
  fill: none;
  stroke-width: 4.5;
}

.system-status__ring-track {
  stroke: rgba(128, 128, 128, 0.22);
}

.system-status__ring-fill {
  stroke: currentColor;
  stroke-linecap: round;
  transition: stroke-dashoffset 0.35s ease;
}

.system-status__ring-center {
  position: absolute;
  font-size: 0.72rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.system-status__ring-label {
  font-size: 0.68rem;
  opacity: 0.7;
}

.system-status__ring-load {
  flex-basis: 100%;
  font-size: 0.68rem;
  opacity: 0.7;
  text-align: center;
}

.system-status__segments {
  display: flex;
  flex: 1;
  flex-direction: column;
  justify-content: center;
  gap: 9px;
  min-height: 0;
  margin-top: 8px;
}

.system-status__segment {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
  font-size: 0.7rem;
}

.system-status__segment-label {
  flex: 0 0 auto;
  width: 2.6em;
  opacity: 0.7;
}

.system-status__segment-track {
  display: grid;
  flex: 1;
  grid-template-columns: repeat(20, minmax(0, 1fr));
  gap: 2px;
  min-width: 0;
}

.system-status__segment-track--empty {
  visibility: hidden;
}

.system-status__segment-track > i {
  height: 6px;
  border-radius: 2px;
  background: rgba(128, 128, 128, 0.18);
  transition: background-color 0.35s ease;
}

.system-status__segment-track > i.is-on {
  background: currentColor;
  opacity: 0.9;
}

.system-status__segment-value {
  flex: 0 0 auto;
  width: 2.8em;
  font-variant-numeric: tabular-nums;
  text-align: right;
}

.system-status__footer {
  display: flex;
  flex: 0 0 auto;
  gap: 10px;
  margin-top: auto;
  padding-top: 6px;
  overflow: hidden;
  font-size: 0.65rem;
  opacity: 0.55;
  white-space: nowrap;
}

.system-status__footer > span {
  overflow: hidden;
  text-overflow: ellipsis;
}

@container (max-width: 230px) {
  .system-status__grid {
    grid-template-columns: 1fr;
  }

  .system-status__minimal {
    flex-direction: column;
    gap: 8px;
  }

  .system-status__minimal-cpu {
    font-size: clamp(1.3rem, 20cqw, 2rem);
  }

  .system-status__ring {
    gap: 6px 10px;
  }

  .system-status__ring-svg {
    width: 44px;
    height: 44px;
  }
}

@container (max-height: 110px) {
  .system-status__footer {
    display: none;
  }
}
</style>
