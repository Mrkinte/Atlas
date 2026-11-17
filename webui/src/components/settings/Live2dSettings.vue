<script lang="ts" setup>
import { onMounted, ref } from "vue";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import { useSettingsSave } from "@/composables/useSettingsSave.ts";
import { extractErrorMessage } from "@/apis/errors.ts";
import { deleteLive2dModel, listLive2dModels, uploadLive2dModel } from "@/apis/modules/live2d.ts";
import { LIVE2D_MODEL_MAX_SIZE_BYTES } from "@/config/default.ts";
import {
  BUILT_IN_MODEL_PATH,
  createEmptyLive2dModelConfig,
  type Live2dModelConfig,
  loadLive2dModelConfig,
  saveLive2dModelConfig,
} from "@/services/live2dModelConfig.ts";
import {
  type Live2dManifestWardrobeGroup,
  loadLive2dManifest,
  loadLive2dRuntimeOptions,
} from "@/services/live2dManifest.ts";
import { settings } from "@/stores/settings.ts";
import type { components } from "@/apis/schema";

/** 与后端 Live2DModelService.MaxArchiveSize 保持一致 */
const MODEL_MAX_SIZE = LIVE2D_MODEL_MAX_SIZE_BYTES;

type Live2dModel = components["schemas"]["Live2DModelResponse"];

const message = useAppMessage();
const saveSiteSettings = useSettingsSave();

const models = ref<Live2dModel[]>([]);
const loading = ref(false);
const uploading = ref(false);
const savingEnabled = ref(false);

const uploadInputRef = ref<HTMLInputElement | null>(null);

interface SelectOption {
  label: string;
  value: string;
}

const configVisible = ref(false);
const configLoading = ref(false);
const configSaving = ref(false);
const configTargetName = ref("");
const config = ref<Live2dModelConfig>(createEmptyLive2dModelConfig(BUILT_IN_MODEL_PATH));
const configMotions = ref<SelectOption[]>([]);
const configExpressions = ref<SelectOption[]>([]);
const configWardrobes = ref<Live2dManifestWardrobeGroup[]>([]);

/** 打开单个模型的行为设置：动作/表情优先取展示清单，没有清单时回退 model3.json */
async function openConfig(modelPath: string, name: string) {
  configVisible.value = true;
  configLoading.value = true;
  configTargetName.value = name;
  config.value = createEmptyLive2dModelConfig(modelPath);
  configMotions.value = [];
  configExpressions.value = [];
  configWardrobes.value = [];

  try {
    const [loaded, manifest, runtime] = await Promise.all([
      loadLive2dModelConfig(modelPath),
      loadLive2dManifest(modelPath),
      loadLive2dRuntimeOptions(modelPath),
    ]);
    config.value = loaded;

    if (manifest && manifest.motions.length > 0) {
      configMotions.value = manifest.motions.map((motion) => ({
        value: motion.id,
        label: motion.group ? `${motion.name}（${motion.group}）` : motion.name,
      }));
    } else {
      configMotions.value = runtime.motions.map((motion) => ({
        value: motion.file,
        label: `${motion.name}（${motion.group}）`,
      }));
    }

    if (manifest?.expressionsDeclared) {
      configExpressions.value = manifest.expressions.map((expression) => ({
        value: expression.runtimeName,
        label: expression.name,
      }));
    } else {
      configExpressions.value = runtime.expressions.map((expression) => ({
        value: expression.runtimeName,
        label: expression.name,
      }));
    }

    configWardrobes.value = manifest?.wardrobe ?? [];
  } finally {
    configLoading.value = false;
  }
}

function outfitValue(groupId: string): string {
  return config.value.outfits.find((item) => item.groupId === groupId)?.optionId ?? "";
}

function handleOutfitChange(groupId: string, value: unknown) {
  const optionId = typeof value === "string" ? value : "";
  const rest = config.value.outfits.filter((item) => item.groupId !== groupId);
  config.value.outfits = optionId ? [...rest, { groupId, optionId }] : rest;
}

async function saveConfig() {
  if (configSaving.value) return;

  configSaving.value = true;
  try {
    const error = await saveLive2dModelConfig(config.value);
    if (error) {
      message.error(error);
      return;
    }

    message.success("模型配置已保存");
    configVisible.value = false;
  } finally {
    configSaving.value = false;
  }
}

function formatSize(value: number | string): string {
  const bytes = Number(value);
  if (bytes < 1024 * 1024) return `${Math.ceil(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

async function loadModels() {
  loading.value = true;
  try {
    const { data, response } = await listLive2dModels();
    if (!response.ok || !data) return;
    models.value = data;
  } catch {
    // 接口异常时保持当前列表
  } finally {
    loading.value = false;
  }
}

/** 关闭后首页不再加载相关代码 */
async function handleEnabled(value: boolean) {
  if (savingEnabled.value) return;

  savingEnabled.value = true;
  try {
    const error = await saveSiteSettings({ live2DEnabled: value });
    if (error) return;
    message.success(value ? "已启用看板娘" : "已禁用看板娘");
  } finally {
    savingEnabled.value = false;
  }
}

/** 内置模型传空字符串 */
async function handleUse(path: string) {
  if (await saveSiteSettings({ live2DModelPath: path })) return;
  message.success("已切换看板娘模型");
}

function handlePickUpload() {
  if (uploading.value) return;
  uploadInputRef.value?.click();
}

function handleUploadChange(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = "";
  if (!file) return;

  if (!file.name.toLowerCase().endsWith(".zip")) {
    message.error("请选择 ZIP 压缩包");
    return;
  }
  if (file.size > MODEL_MAX_SIZE) {
    message.error("模型压缩包不能超过 100 MB");
    return;
  }

  void saveModel(file);
}

async function saveModel(file: File) {
  uploading.value = true;
  try {
    const { data, error, response } = await uploadLive2dModel(file);

    if (!response.ok) {
      // 401 提示登录，其余优先展示后端返回的具体失败原因（名称不合法、缺少 .model3.json 等）
      message.error(extractErrorMessage(error, "上传失败", response.status));
      return;
    }

    if (!data) return;
    await loadModels();
    message.success(`已上传模型 ${data.name}`);
  } catch {
    message.error("网络异常，请稍后重试");
  } finally {
    uploading.value = false;
  }
}

async function handleDelete(model: Live2dModel) {
  try {
    const { error, response } = await deleteLive2dModel(model.id);

    if (!response.ok) {
      message.error(extractErrorMessage(error, "删除失败", response.status));
      return;
    }

    // 删除的是当前使用的模型时回退到内置模型
    if (settings.live2DModelPath.startsWith(`/uploads/live2d/${model.id}/`)) {
      await saveSiteSettings({ live2DModelPath: "" });
    }

    await loadModels();
    message.success("模型已删除");
  } catch {
    message.error("网络异常，请稍后重试");
  }
}

onMounted(() => {
  void loadModels();
});
</script>

<template>
  <h2 class="settings__heading">看板娘</h2>

  <n-card class="live2d__models" size="small">
    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">启用看板娘</div>
        <div class="settings__row-desc">关闭后首页不再显示看板娘，也不会加载 Live2D 相关代码</div>
      </div>
      <div class="settings__row-control">
        <n-switch
          :loading="savingEnabled"
          :value="settings.live2DEnabled"
          @update:value="handleEnabled"
        />
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">内置模型</div>
        <div class="settings__row-desc">默认启用的模型</div>
      </div>
      <div class="settings__row-control live2d__actions">
        <n-button @click="openConfig(BUILT_IN_MODEL_PATH, '内置模型')"> 设置 </n-button>
        <n-button
          :disabled="!settings.live2DModelPath"
          :type="settings.live2DModelPath ? 'default' : 'primary'"
          @click="handleUse('')"
        >
          {{ settings.live2DModelPath ? "使用" : "使用中" }}
        </n-button>
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">上传模型</div>
        <div class="settings__row-desc">ZIP 包内需包含正确的Live2D模型，解压后不超过 200 MB</div>
      </div>
      <div class="settings__row-control">
        <n-button :loading="uploading" @click="handlePickUpload"> 上传 ZIP </n-button>
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div v-if="loading" class="live2d__empty">模型列表加载中…</div>
    <div v-else-if="models.length === 0" class="live2d__empty">还没有上传，仅使用内置模型</div>

    <div v-for="model in models" :key="model.id" class="settings__row live2d__model">
      <div class="settings__row-main">
        <div class="settings__row-label">{{ model.name }}</div>
        <div class="settings__row-desc">{{ formatSize(model.sizeBytes) }}</div>
      </div>
      <div class="settings__row-control live2d__actions">
        <n-button @click="openConfig(model.path, model.name)">设置</n-button>
        <n-button
          :disabled="settings.live2DModelPath === model.path"
          :type="settings.live2DModelPath === model.path ? 'primary' : 'default'"
          style="width: 70px"
          @click="handleUse(model.path)"
        >
          {{ settings.live2DModelPath === model.path ? "使用中" : "使用" }}
        </n-button>
        <n-popconfirm
          negative-text="取消"
          positive-text="删除"
          @positive-click="handleDelete(model)"
        >
          <template #trigger>
            <n-button :disabled="uploading" quaternary type="error"> 删除 </n-button>
          </template>
          删除后该模型文件将无法恢复，确定继续？
        </n-popconfirm>
      </div>
    </div>

    <input
      ref="uploadInputRef"
      accept=".zip,application/zip"
      hidden
      type="file"
      @change="handleUploadChange"
    />
  </n-card>

  <n-modal
    v-model:show="configVisible"
    :bordered="false"
    :title="`${configTargetName} · 行为设置`"
    preset="card"
    style="width: var(--editor-max-width)"
  >
    <n-spin :show="configLoading">
      <div class="live2d__config">
        <div class="live2d__config-field">
          <div class="live2d__config-label">开机执行动作</div>
          <n-select
            v-model:value="config.entryMotionId"
            :options="[{ label: '使用清单默认', value: '' }, ...configMotions]"
            filterable
          />
        </div>

        <div class="live2d__config-field">
          <div class="live2d__config-label">待机循环动作</div>
          <n-select
            v-model:value="config.idleMotionId"
            :options="[{ label: '不设置', value: '' }, ...configMotions]"
            filterable
          />
          <div class="live2d__config-desc">
            其他动作结束后会循环播放该动作，点击看板娘或右键操作可打断
          </div>
        </div>

        <div class="live2d__config-field">
          <div class="live2d__config-label">默认表情</div>
          <n-select
            v-model:value="config.expression"
            :options="[{ label: '使用清单默认', value: '' }, ...configExpressions]"
            filterable
          />
        </div>

        <div v-for="group in configWardrobes" :key="group.id" class="live2d__config-field">
          <div class="live2d__config-label">{{ group.name }}</div>
          <n-select
            :options="[
              { label: '使用清单默认', value: '' },
              ...group.options.map((option) => ({
                label: option.name,
                value: option.id,
              })),
            ]"
            :value="outfitValue(group.id)"
            @update:value="handleOutfitChange(group.id, $event)"
          />
        </div>

        <div class="settings__row live2d__config-switch">
          <div class="settings__row-main">
            <div class="settings__row-label">播放语音</div>
            <div class="settings__row-desc">关闭后不播放动作关联的音效，不影响气泡台词</div>
          </div>
          <div class="settings__row-control">
            <n-switch v-model:value="config.soundEnabled" />
          </div>
        </div>

        <div
          v-if="
            !configLoading &&
            configMotions.length === 0 &&
            configExpressions.length === 0 &&
            configWardrobes.length === 0
          "
          class="live2d__config-desc"
        >
          该模型没有可用的展示清单，仅能配置语音开关
        </div>
      </div>
    </n-spin>

    <template #footer>
      <div class="live2d__config-footer">
        <n-text type="error"> 保存设置，刷新网页后生效。 </n-text>
        <n-button @click="configVisible = false">取消</n-button>
        <n-button
          :disabled="configLoading"
          :loading="configSaving"
          type="primary"
          @click="saveConfig"
        >
          保存
        </n-button>
      </div>
    </template>
  </n-modal>

  <n-alert :bordered="false" class="live2d__alert" type="info">
    看板娘默认显示在页面左下角，可直接拖动；鼠标移到看板娘上单击鼠标右键会显示菜单，
    点「调整尺寸」后拖动右下角手柄即可改变大小。位置与尺寸会按模型分别保存。
  </n-alert>

  <n-alert :bordered="false" class="live2d__alert" type="info">
    更多 Live2D 模型信息，请前往 https://mrkinte.github.io/Atlas-Docs/docs/live2d.html 查看。
  </n-alert>
</template>

<style scoped>
.live2d__models {
  margin-top: 16px;
}

.live2d__empty {
  padding: 16px 0;
  font-size: 0.85rem;
  text-align: center;
  opacity: 0.6;
}

.live2d__actions {
  gap: 8px;
  width: auto;
  max-width: none;
  flex: 0 0 auto;
}

.live2d__alert {
  margin-top: 16px;
}

.live2d__config {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.live2d__config-field {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.live2d__config-label {
  font-size: 0.875rem;
}

.live2d__config-desc {
  font-size: 0.75rem;
  opacity: 0.6;
}

.live2d__config-switch {
  padding-top: 4px;
}

.live2d__config-footer {
  display: flex;
  gap: 8px;
  justify-content: flex-end;
  align-items: center;
}
</style>
