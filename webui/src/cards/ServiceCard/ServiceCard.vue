<script lang="ts" setup>
import { computed, inject, ref } from "vue";
import type { DropdownOption } from "naive-ui";
import Image24Regular from "@vicons/fluent/es/Image24Regular.js";
import { SERVICE_PROBE_KEY } from "@/composables/serviceProbeContext.ts";
import type { ServiceCardConfig } from "@/cards/ServiceCard/config.ts";
import { isValidHttpUrl, resolveAutoServiceUrl } from "@/cards/ServiceCard/target.ts";

const props = withDefaults(defineProps<Partial<ServiceCardConfig>>(), {
  name: "",
  description: "",
  hostMode: "auto",
  protocol: "auto",
  port: 8080,
  path: "",
  url1: "",
  url2: "",
  url3: "",
  icon: "",
  openMethod: "_blank",
});

/** 首页注入的探测结果；编辑器预览等没有注入时为 null，不展示状态 */
const probe = inject(SERVICE_PROBE_KEY, null);

/** 有探测结果时才渲染状态点，避免未参与探测的卡片出现空占位 */
const hasProbe = computed(() => probe?.value !== undefined);

/** 状态文案：探测中显示省略号，在线显示耗时，离线区分超时与不可达 */
const probeLabel = computed(() => {
  const entry = probe?.value;
  if (!entry) return "";
  if (entry.state === "probing") return "…";
  if (entry.state === "online") return `${entry.latencyMs}ms`;
  return entry.reason === "timeout" ? "超时" : "不可达";
});

const probeClass = computed(() => {
  const state = probe?.value?.state;
  return state ? `service-card__probe--${state}` : "";
});

/** 宽度不足时补充状态含义 */
const probeTitle = computed(() => {
  const entry = probe?.value;
  if (!entry) return "";
  if (entry.state === "probing") return "正在探测连通性";
  if (entry.state === "online") return `可达，延迟 ${entry.latencyMs}ms`;
  return entry.reason === "timeout" ? "探测超时" : "无法连接";
});

/** 手动模式候选网址，过滤掉非法值 */
const manualUrls = computed(() =>
  [props.url1, props.url2, props.url3]
    .map((url) => url?.trim() ?? "")
    .filter((url) => isValidHttpUrl(url)),
);

const autoUrl = computed(() => resolveAutoServiceUrl(props.port, props.path, props.protocol));

const showUrlMenu = ref(false);
const urlOptions = computed<DropdownOption[]>(() =>
  manualUrls.value.map((url, index) => ({
    label: url,
    key: String(index),
  })),
);

function openUrl(url: string) {
  if (!url) return;
  if (props.openMethod === "_self") {
    window.location.href = url;
  } else {
    window.open(url, "_blank", "noopener,noreferrer");
  }
}

function handleCardClick() {
  if (props.hostMode === "manual") {
    if (manualUrls.value.length === 1) {
      openUrl(manualUrls.value[0]);
      return;
    }
    // 手动模式：先在上方列出待选网址，选择后再跳转
    showUrlMenu.value = manualUrls.value.length > 0 && !showUrlMenu.value;
    return;
  }
  openUrl(autoUrl.value);
}

function handleSelectUrl(key: string) {
  showUrlMenu.value = false;
  const url = manualUrls.value[Number(key)];
  if (url) openUrl(url);
}
</script>

<template>
  <n-dropdown
    :on-clickoutside="() => (showUrlMenu = false)"
    :on-select="handleSelectUrl"
    :options="urlOptions"
    :show="showUrlMenu"
    placement="top"
    trigger="manual"
  >
    <div class="service-card__trigger" @click="handleCardClick">
      <!-- 状态角标浮在右上角，不占正文空间 -->
      <div v-if="hasProbe" :class="probeClass" :title="probeTitle" class="service-card__probe">
        <span class="service-card__probe-dot" />
        <span class="service-card__probe-text">{{ probeLabel }}</span>
      </div>
      <n-tooltip placement="bottom">
        <template #trigger>
          <n-card
            :content-style="{ padding: '7px', overflow: 'hidden' }"
            class="service-card"
            hoverable
            size="small"
          >
            <n-flex :wrap="false" align="center" class="service-card__body">
              <n-icon :size="48">
                <Image24Regular v-if="!props.icon" />
                <img v-else :src="props.icon" alt="" width="48px" />
              </n-icon>
              <n-flex :size="0" class="service-card__meta" vertical>
                <div class="service-card__name">{{ props.name }}</div>
                <div class="service-card__desc">{{ props.description }}</div>
              </n-flex>
            </n-flex>
          </n-card>
        </template>
        {{ props.name }}
      </n-tooltip>
    </div>
  </n-dropdown>
</template>

<style scoped>
.service-card__trigger {
  position: relative;
  width: 100%;
  height: 100%;
}

.service-card {
  width: 100%;
  height: 100%;
  cursor: pointer;
}

.service-card__body {
  width: 100%;
  height: 100%;
}

/* 文字列占满图标右侧空间，窄卡片才能正确省略而不是撑出卡片 */
.service-card__meta {
  flex: 1;
  min-width: 0;
}

.service-card__name {
  font-size: 1rem;
  font-weight: bold;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* 浮在右上角，不改变正文排版 */
.service-card__probe {
  position: absolute;
  top: 3px;
  right: 5px;
  z-index: 2;
  display: inline-flex;
  align-items: center;
  gap: 3px;
  padding: 1px 5px;
  border-radius: 8px;
  font-size: 10px;
  line-height: 14px;
  white-space: nowrap;
  background-color: rgba(127, 127, 127, 0.18);
}

.service-card__probe-dot {
  flex: none;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background-color: #909399;
}

.service-card__probe--probing .service-card__probe-dot {
  animation: service-probe-pulse 1.2s ease-in-out infinite;
}

.service-card__probe--online .service-card__probe-dot {
  background-color: #18a058;
}

.service-card__probe--offline .service-card__probe-dot {
  background-color: #d03050;
}

.service-card__probe-text {
  max-width: 4.5em;
  overflow: hidden;
  text-overflow: ellipsis;
}

@keyframes service-probe-pulse {
  50% {
    opacity: 0.35;
  }
}

.service-card__desc {
  font-size: 0.8rem;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
</style>
