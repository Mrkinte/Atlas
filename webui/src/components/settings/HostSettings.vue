<script lang="ts" setup>
import { computed, onMounted, ref, watch } from "vue";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import { settings } from "@/stores/settings.ts";
import { useSettingsSave } from "@/composables/useSettingsSave.ts";
import { getHardware } from "@/apis/modules/system-status.ts";
import type { components } from "@/apis/schema";

type HardwareInfo = components["schemas"]["SystemStatusHardware"];

const message = useAppMessage();
const saveSiteSettings = useSettingsSave();

const hardware = ref<HardwareInfo | null>(null);
const loading = ref(true);
const loadFailed = ref(false);
const savingEnabled = ref(false);
const savingDocker = ref(false);
const savingSocket = ref(false);
const dockerSocket = ref(settings.dockerSocket);

watch(
  () => settings.dockerSocket,
  (value) => {
    dockerSocket.value = value;
  },
);

const socketChanged = computed(
  () => dockerSocket.value.trim() !== settings.dockerSocket,
);

const isPublic = computed(() =>
  hardware.value ? settings.publicHardware.includes(hardware.value.id) : false,
);

async function loadHardware() {
  loading.value = true;
  loadFailed.value = false;

  try {
    const { data, response } = await getHardware();
    if (!response.ok || !data) {
      loadFailed.value = true;
      return;
    }

    hardware.value = data;
  } catch {
    loadFailed.value = true;
  } finally {
    loading.value = false;
  }
}

/** 启用/禁用系统状态：关闭后所有连接（含管理员）都会断开 */
async function handleEnabled(value: boolean) {
  if (savingEnabled.value) return;

  savingEnabled.value = true;
  try {
    const error = await saveSiteSettings({ systemStatusEnabled: value });
    if (error) return;
    message.success(value ? "已启用系统状态" : "已禁用系统状态");
  } finally {
    savingEnabled.value = false;
  }
}

/** 只切换当前硬件的白名单，保留已有的其他硬件条目 */
async function handleToggle(value: boolean) {
  if (!hardware.value) return;

  const id = hardware.value.id;
  const next = value
    ? [...new Set([...settings.publicHardware, id])]
    : settings.publicHardware.filter((item) => item !== id);

  const error = await saveSiteSettings({ publicHardware: next });
  if (error) return;

  message.success(value ? "已公开本机状态" : "已取消公开本机状态");
}

async function handleDockerEnabled(value: boolean) {
  if (savingDocker.value) return;

  savingDocker.value = true;
  try {
    const error = await saveSiteSettings({ dockerEnabled: value });
    if (error) return;
    message.success(value ? "已启用 Docker 状态" : "已禁用 Docker 状态");
  } finally {
    savingDocker.value = false;
  }
}

async function saveDockerSocket() {
  if (savingSocket.value || !socketChanged.value) return;

  savingSocket.value = true;
  try {
    const error = await saveSiteSettings({ dockerSocket: dockerSocket.value.trim() });
    if (error) {
      dockerSocket.value = settings.dockerSocket;
      return;
    }
    message.success("已保存 Docker Socket 路径");
  } finally {
    savingSocket.value = false;
  }
}

async function handleProbeEnabled(value: boolean) {
  if (savingEnabled.value) return;

  savingEnabled.value = true;
  try {
    const error = await saveSiteSettings({ serviceProbeEnabled: value });
    if (error) return;
    message.success(value ? "已启用服务探测" : "已关闭服务探测");
  } finally {
    savingEnabled.value = false;
  }
}

onMounted(() => {
  void loadHardware();
});
</script>

<template>
  <h2 class="settings__heading">系统状态</h2>

  <n-card size="small">
    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">启用系统状态</div>
        <div class="settings__row-desc">关闭后停止推送状态信息</div>
      </div>
      <div class="settings__row-control">
        <n-switch
          :loading="savingEnabled"
          :value="settings.systemStatusEnabled"
          @update:value="handleEnabled"
        />
      </div>
    </div>

    <n-divider class="settings__divider" />

    <n-spin :show="loading">
      <div class="settings__row">
        <div class="settings__row-main">
          <div class="settings__row-label">当前硬件</div>
          <div class="settings__row-desc">
            {{
              loadFailed ? "无法读取硬件信息，请确认已登录且服务正常" : hardware?.name || "读取中…"
            }}
          </div>
        </div>
        <div class="settings__row-control">
          <span v-if="hardware" class="hardware-id">{{ hardware.id }}</span>
        </div>
      </div>

      <n-divider class="settings__divider" />

      <div class="settings__row">
        <div class="settings__row-main">
          <div class="settings__row-label">允许匿名查看本机状态</div>
          <div class="settings__row-desc">
            开启后，未登录的 WebSocket 连接只会收到本机的 CPU、内存、磁盘等聚合指标，不会包含硬件 ID
            或真实主机名
          </div>
        </div>
        <div class="settings__row-control">
          <n-switch
            :disabled="loading || !hardware"
            :value="isPublic"
            @update:value="handleToggle"
          />
        </div>
      </div>
    </n-spin>
  </n-card>

  <n-alert :bordered="false" class="status-note" type="warning">
    公开的系统状态属于匿名可读数据。只应在确认指标不含敏感信息时开启。
  </n-alert>

  <h2 class="settings__heading">探测</h2>

  <n-card size="small">
    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">服务可达性探测</div>
        <div class="settings__row-desc">
          打开首页时探测服务卡片的目标地址，在卡片右上角显示连通状态与延迟
        </div>
      </div>
      <div class="settings__row-control">
        <n-switch
          :loading="savingEnabled"
          :value="settings.serviceProbeEnabled"
          @update:value="handleProbeEnabled"
        />
      </div>
    </div>
  </n-card>

  <h2 class="settings__heading" style="margin-top: 24px;">Docker</h2>

  <n-card size="small">
    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">启用 Docker 状态</div>
        <div class="settings__row-desc">首页 Docker 卡片读取容器、镜像与资源用量</div>
      </div>
      <div class="settings__row-control">
        <n-switch
          :loading="savingDocker"
          :value="settings.dockerEnabled"
          @update:value="handleDockerEnabled"
        />
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div class="settings__row settings__row--stack">
      <div class="settings__row-main">
        <div class="settings__row-label">Socket 路径</div>
        <div class="settings__row-desc">
          留空使用默认路径。Linux：unix:///var/run/docker.sock；Windows：npipe:////./pipe/docker_engine
        </div>
      </div>
      <n-input-group>
        <n-input
          v-model:value="dockerSocket"
          :disabled="savingSocket"
          :maxlength="512"
          placeholder="unix:///var/run/docker.sock"
        />
        <n-button
          :disabled="!socketChanged"
          :loading="savingSocket"
          type="primary"
          @click="saveDockerSocket"
        >
          保存
        </n-button>
      </n-input-group>
    </div>
  </n-card>
</template>

<style scoped>
.hardware-id {
  overflow: hidden;
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: 0.78rem;
  opacity: 0.55;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.status-note {
  margin-top: 16px;
  margin-bottom: 16px;
}
</style>
