<script lang="ts" setup>
import { computed, onMounted, ref, type Component } from "vue";
import { useThemeVars } from "naive-ui";
import ArrowClockwise16Regular from "@vicons/fluent/es/ArrowClockwise16Regular.js";
import Building16Regular from "@vicons/fluent/es/Building16Regular.js";
import Desktop16Regular from "@vicons/fluent/es/Desktop16Regular.js";
import Globe24Regular from "@vicons/fluent/es/Globe24Regular.js";
import Location16Regular from "@vicons/fluent/es/Location16Regular.js";
import Timer16Regular from "@vicons/fluent/es/Timer16Regular.js";
import { client } from "@/apis/client.ts";
import type { components } from "@/apis/schema";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import type { IpCardConfig } from "@/cards/IpCard/config.ts";

const props = withDefaults(defineProps<Partial<IpCardConfig>>(), {
  showLocalIp: true,
  showIsp: true,
  showLocation: true,
  showLatency: true,
});

type IpInfo = components["schemas"]["IpInfoResponse"];
type PublicIp = components["schemas"]["PublicIpEntry"];

type IpCell = {
  key: string;
  label: string;
  value: string;
  hint?: string;
  icon: Component;
};

const message = useAppMessage();

const info = ref<IpInfo | null>(null);
const loading = ref(false);
const failed = ref(false);

/** 当前展示第几个公网地址；点击地球图标切换 */
const currentIndex = ref(0);
const publicIps = computed<PublicIp[]>(() => info.value?.publicIps ?? []);

/** 未登录时服务端打码，地址不能复制 */
const masked = computed(() => info.value?.masked === true);

const currentIp = computed<PublicIp | null>(() => publicIps.value[currentIndex.value] ?? null);

const versionLabel = computed(() => (currentIp.value ? `IPv${currentIp.value.version}` : "公网"));

const switchHint = computed(() => {
  if (publicIps.value.length < 2) return "只有 1 个公网 IP";
  return `切换公网 IP（${currentIndex.value + 1}/${publicIps.value.length}）`;
});

const ipText = computed(() => {
  if (currentIp.value) return currentIp.value.address;
  if (loading.value) return "查询中…";
  return failed.value ? "获取失败" : "—";
});

/** 内网地址需要登录后服务端才会返回 */
const localIpText = computed(() => info.value?.localIp ?? "—");

const locationText = computed(() => {
  if (!currentIp.value) return "—";
  return currentIp.value.city || currentIp.value.region || currentIp.value.country || "—";
});

const locationHint = computed(() => {
  const item = currentIp.value;
  if (!item) return undefined;

  const parts = [item.country, item.region, item.city].filter((part): part is string =>
    Boolean(part),
  );
  // 省市同名时只留一份
  return [...new Set(parts)].join(" · ") || undefined;
});

const latencyText = computed(() => {
  const item = info.value;
  if (!item) return "—";
  if (item.latencyNodes === 0) return "—";
  return `${item.latencyMs}ms / ${item.latencyNodes} 节点`;
});

const ispText = computed(() => {
  if (currentIp.value?.isp) return currentIp.value.isp;
  return masked.value ? "登录后查看" : "—";
});

const themeVars = useThemeVars();
const primaryColor = computed(() => themeVars.value.primaryColor);
/** 主题色 20% 透明度做图标底 */
const badgeColor = computed(() => `${primaryColor.value}33`);

const copyHint = computed(() => {
  if (masked.value) return "登录后查看完整 IP";
  return currentIp.value ? "点击复制" : "";
});

const cells = computed<IpCell[]>(() => {
  const items: IpCell[] = [];

  if (props.showLocalIp) {
    items.push({
      key: "local",
      label: "内网 IP",
      value: localIpText.value,
      icon: Desktop16Regular,
    });
  }
  if (props.showIsp) {
    items.push({
      key: "isp",
      label: "运营商",
      value: ispText.value,
      hint: currentIp.value?.asn ?? undefined,
      icon: Building16Regular,
    });
  }
  if (props.showLocation) {
    items.push({
      key: "location",
      label: "位置",
      value: locationText.value,
      hint: locationHint.value,
      icon: Location16Regular,
    });
  }
  if (props.showLatency) {
    items.push({
      key: "latency",
      label: "平均延迟",
      value: latencyText.value,
      icon: Timer16Regular,
    });
  }

  return items;
});

async function load(force = false) {
  if (loading.value) return;

  loading.value = true;
  failed.value = false;

  try {
    const { data, error } = await client.GET("/api/ip", {
      params: { query: force ? { refresh: true } : {} },
    });

    if (error || !data) {
      failed.value = true;
      return;
    }

    info.value = data;
    // 刷新后地址数量可能变少
    if (currentIndex.value >= data.publicIps.length) currentIndex.value = 0;
  } catch {
    failed.value = true;
  } finally {
    loading.value = false;
  }
}

function cycleIp() {
  if (publicIps.value.length < 2) return;
  currentIndex.value = (currentIndex.value + 1) % publicIps.value.length;
}

async function copyIp() {
  const ip = currentIp.value?.address;
  if (!ip || masked.value) return;

  try {
    await navigator.clipboard.writeText(ip);
    message.success("已复制 IP");
  } catch {
    message.warning("复制失败，请手动选择复制");
  }
}

onMounted(() => load());
</script>

<template>
  <n-card class="ip-card" size="small">
    <n-flex class="ip-card__body" vertical :size="10">
      <n-flex align="center" :size="12">
        <n-tooltip>
          <template #trigger>
            <n-avatar
                round
                :size="40"
                :color="badgeColor"
                :class="{ 'ip-card__badge': publicIps.length > 1 }"
                @click="cycleIp"
            >
              <n-icon :color="primaryColor" :component="Globe24Regular" :size="22" />
            </n-avatar>
          </template>
          {{ switchHint }}
        </n-tooltip>

        <n-flex vertical :size="2" style="flex: 1; min-width: 0">
          <n-flex align="center" :size="6" :wrap="false">
            <n-tag :bordered="false" size="small" type="primary">{{ versionLabel }}</n-tag>
            <n-ellipsis class="ip-card__title" :tooltip="false">
              <n-text depth="3">公网 IP</n-text>
            </n-ellipsis>
          </n-flex>

          <n-tooltip :disabled="!currentIp">
            <template #trigger>
              <n-ellipsis :tooltip="false" class="ip-card__ip" @click="copyIp">
                <n-text strong type="primary">{{ ipText }}</n-text>
              </n-ellipsis>
            </template>
            {{ copyHint }}
          </n-tooltip>
        </n-flex>

        <n-tooltip>
          <template #trigger>
            <n-button aria-label="刷新 IP" :disabled="loading" quaternary @click="load(true)">
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

      <n-grid class="ip-card__grid" :cols="2" :x-gap="8" :y-gap="8">
        <n-gi v-for="cell in cells" :key="cell.key" class="ip-card__gi">
          <n-card
              embedded
              size="small"
              class="ip-card__cell"
              :content-style="{ padding: '10px 12px' }"
          >
            <n-flex vertical :size="4">
              <n-flex align="center" :size="4">
                <n-icon :component="cell.icon" :size="14" />
                <n-text class="ip-card__cell-label" depth="3">{{ cell.label }}</n-text>
              </n-flex>
              <n-ellipsis :title="cell.hint" :tooltip="false">
                <n-text class="ip-card__cell-value" strong>{{ cell.value }}</n-text>
              </n-ellipsis>
            </n-flex>
          </n-card>
        </n-gi>
      </n-grid>
    </n-flex>
  </n-card>
</template>

<style scoped>
.ip-card {
  height: 100%;
}

.ip-card__body {
  height: 100%;
}

.ip-card__grid {
  flex: 1;
  grid-auto-rows: minmax(0, 1fr);
}

.ip-card__gi {
  display: grid;
}

.ip-card__cell {
  overflow: hidden;
}

.ip-card__badge {
  cursor: pointer;
}

.ip-card__title {
  flex: 1;
  min-width: 0;
  font-size: 0.8rem;
}

.ip-card__ip {
  font-size: 1.05rem;
  font-variant-numeric: tabular-nums;
  cursor: pointer;
}

.ip-card__cell-label {
  font-size: 0.75rem;
}

.ip-card__cell-value {
  font-size: 0.9rem;
  font-variant-numeric: tabular-nums;
}

.ip-card__spin {
  animation: ip-card-spin 0.9s linear infinite;
}

@keyframes ip-card-spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
