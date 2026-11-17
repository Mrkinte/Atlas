<script lang="ts" setup>
import { settings } from "@/stores/settings.ts";
import { useSettingsSave } from "@/composables/useSettingsSave.ts";
import { normalizeHexColor } from "@/utils/colorConverter.ts";
import { SETTINGS_LIMITS } from "@/config/default.ts";
import { openImageLibrary } from "@/composables/useImageLibraryHost.ts";
import { useCommitSlider } from "@/composables/useCommitSlider.ts";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import { ref } from "vue";
import { setThemeMode, themeMode, type ThemeMode } from "@/stores/theme.ts";
import { uploadImage } from "@/apis/modules/images.ts";
import { extractErrorMessage } from "@/apis/errors.ts";

const PRESET_COLORS = [
  "#18a058",
  "#2080f0",
  "#7c3aed",
  "#d03050",
  "#f0a020",
  "#13c2c2",
  "#eb2f96",
  "#fa541c",
];

const message = useAppMessage();
const saveSiteSettings = useSettingsSave();

/** 主题是浏览器本地偏好，只保存在 localStorage */
function handleThemeModeChange(value: string | number | boolean) {
  setThemeMode(value as ThemeMode);
}

let colorChanged = false;

/** 与后端 /api/images 的限制保持一致 */
const BACKGROUND_MAX_SIZE = 10 * 1024 * 1024;
const BACKGROUND_ACCEPT = "image/png,image/jpeg,image/webp,image/gif,image/bmp";

const backgroundInputRef = ref<HTMLInputElement | null>(null);
const uploadingBackground = ref(false);

/** 取色时频繁触发，仅更新本地预览，防抖后再写入服务端 */
function handlePreview(value: string | null) {
  settings.primaryColor = normalizeHexColor(value);
  colorChanged = true;
}

function handleReset() {
  settings.primaryColor = "";
  void saveSiteSettings({ primaryColor: "" });
}

function handleSaveColor() {
  if (colorChanged) {
    saveSiteSettings({ primaryColor: settings.primaryColor });
  }
  colorChanged = false;
}

function handlePickBackground() {
  if (uploadingBackground.value) return;
  backgroundInputRef.value?.click();
}

async function handleBackgroundFileChange(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = "";
  if (!file) return;

  if (file.size > BACKGROUND_MAX_SIZE) {
    message.error("背景图不能超过 10 MB");
    return;
  }

  uploadingBackground.value = true;
  try {
    const { data, error, response } = await uploadImage(file, {
      kind: "image",
    });

    if (!response.ok) {
      // 401 提示登录，其余优先展示后端返回的具体失败原因
      message.error(extractErrorMessage(error, "背景图上传失败", response.status));
      return;
    }
    if (!data) return;

    if (await saveSiteSettings({ backgroundImage: data.url })) return;
    message.success("背景图已更新");
  } catch {
    message.error("网络异常，请稍后重试");
  } finally {
    uploadingBackground.value = false;
  }
}

async function handleChooseBackground() {
  const item = await openImageLibrary("image");
  if (!item) return;

  if (await saveSiteSettings({ backgroundImage: item.url })) return;
  message.success("背景图已更新");
}

async function handleClearBackground() {
  if (await saveSiteSettings({ backgroundImage: "" })) return;
  message.success("背景图已清除");
}

const blurSlider = useCommitSlider("backgroundBlur");
const maskSlider = useCommitSlider("backgroundMask");
</script>

<template>
  <h2 class="settings__heading">外观</h2>

  <n-card size="small">
    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">主题</div>
        <div class="settings__row-desc">跟随系统时随操作系统的明暗外观自动切换</div>
      </div>
      <div class="settings__row-control settings__theme-mode">
        <n-radio-group :value="themeMode" @update:value="handleThemeModeChange">
          <n-radio-button value="system">跟随系统</n-radio-button>
          <n-radio-button value="light">浅色</n-radio-button>
          <n-radio-button value="dark">深色</n-radio-button>
        </n-radio-group>
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">主题色</div>
        <div class="settings__row-desc">
          应用于按钮、开关、选中态等 Naive UI 组件，清空后使用官方默认主色
        </div>
      </div>
      <div class="settings__row-control settings__theme-control">
        <n-color-picker
          :modes="['hex']"
          :show-alpha="false"
          :swatches="PRESET_COLORS"
          :value="settings.primaryColor || null"
          style="width: 140px"
          @update:value="handlePreview"
          @update:show="handleSaveColor"
        />
        <n-button :disabled="!settings.primaryColor" quaternary @click="handleReset">
          恢复默认
        </n-button>
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div class="settings__row settings__row--stack">
      <div class="settings__row-main">
        <div class="settings__row-label">背景图</div>
        <div class="settings__row-desc">显示在所有页面最底层，可从图库选择或直接上传</div>
      </div>
      <div class="settings__background">
        <n-image
          v-if="settings.backgroundImage"
          :src="settings.backgroundImage"
          alt="当前背景图"
          class="settings__background-preview"
          object-fit="cover"
        />
        <div v-else class="settings__background-empty">未设置背景图</div>
        <n-flex :size="8">
          <n-button :loading="uploadingBackground" @click="handlePickBackground">
            上传图片
          </n-button>
          <n-button @click="handleChooseBackground"> 从图库选择 </n-button>
          <n-button
            v-if="settings.backgroundImage"
            quaternary
            type="error"
            @click="handleClearBackground"
          >
            清除
          </n-button>
        </n-flex>
        <input
          ref="backgroundInputRef"
          :accept="BACKGROUND_ACCEPT"
          hidden
          type="file"
          @change="handleBackgroundFileChange"
        />
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">背景模糊</div>
        <div class="settings__row-desc">背景图的模糊半径（px）</div>
      </div>
      <div
        class="settings__row-control"
        @keyup="blurSlider.handleCommit"
        @pointerdown="blurSlider.handlePointerDown"
      >
        <n-slider
          :disabled="!settings.backgroundImage"
          :max="SETTINGS_LIMITS.backgroundBlur.max"
          :min="SETTINGS_LIMITS.backgroundBlur.min"
          :step="1"
          :value="settings.backgroundBlur"
          @update:value="blurSlider.handleInput"
        />
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">遮罩强度</div>
        <div class="settings__row-desc">背景图上层遮罩的不透明度（%）</div>
      </div>
      <div
        class="settings__row-control"
        @keyup="maskSlider.handleCommit"
        @pointerdown="maskSlider.handlePointerDown"
      >
        <n-slider
          :disabled="!settings.backgroundImage"
          :max="SETTINGS_LIMITS.backgroundMask.max"
          :min="SETTINGS_LIMITS.backgroundMask.min"
          :step="1"
          :value="settings.backgroundMask"
          @update:value="maskSlider.handleInput"
        />
      </div>
    </div>
  </n-card>
</template>

<style scoped>
/* 按内容宽度展示，避免被固定控件宽度裁切 */
.settings__theme-mode {
  flex: 0 0 auto;
  width: auto;
  max-width: none;
}

.settings__theme-control {
  align-items: center;
  gap: 16px;
}

.settings__background {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.settings__background-preview {
  width: 100%;
  height: 140px;
  overflow: hidden;
  border-radius: 8px;
}

.settings__background-preview :deep(img) {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.settings__background-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 140px;
  border-radius: 8px;
  background: rgba(127, 127, 127, 0.1);
  font-size: 0.85rem;
  opacity: 0.6;
}
</style>
