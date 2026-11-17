<script lang="ts" setup>
import { ref, watch } from "vue";
import { settings } from "@/stores/settings.ts";
import { useSettingsSave } from "@/composables/useSettingsSave.ts";
import { IMAGE_UPLOAD_LIMITS } from "@/composables/useImageUpload.ts";
import { uploadImageWithProgress } from "@/apis/modules/images.ts";
import { openImageLibrary } from "@/composables/useImageLibraryHost.ts";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import {
  FOOTER_TEXT_MAX_LENGTH,
  SITE_SUBTITLE_MAX_LENGTH,
  SITE_TITLE_MAX_LENGTH,
} from "@/config/default.ts";

const message = useAppMessage();

/** 站点配置编辑态，提交后以服务端结果为准 */
const siteTitle = ref(settings.siteTitle);
const siteSubtitle = ref(settings.siteSubtitle);
const footerText = ref(settings.footerText);

watch(
  () => settings.siteTitle,
  (value) => {
    siteTitle.value = value;
  },
);

watch(
  () => settings.siteSubtitle,
  (value) => {
    siteSubtitle.value = value;
  },
);

watch(
  () => settings.footerText,
  (value) => {
    footerText.value = value;
  },
);

/** 保存站点配置；失败时提示并回退编辑态 */
const saveSiteSettings = useSettingsSave(() => {
  siteTitle.value = settings.siteTitle;
  siteSubtitle.value = settings.siteSubtitle;
  footerText.value = settings.footerText;
});

/** 与图库的图标分类共用限制 */
const faviconInputRef = ref<HTMLInputElement | null>(null);
const uploadingFavicon = ref(false);

function handlePickFavicon() {
  if (uploadingFavicon.value) return;
  faviconInputRef.value?.click();
}

async function handleFaviconFileChange(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = "";
  if (!file) return;

  const { label, maxSize } = IMAGE_UPLOAD_LIMITS.icon;
  if (file.size > maxSize) {
    message.error(`${label}文件不能超过 ${Math.round(maxSize / 1024 / 1024)} MB`);
    return;
  }

  uploadingFavicon.value = true;
  try {
    const image = await uploadImageWithProgress(file, "icon");
    if (await saveSiteSettings({ favicon: image.url })) return;
    message.success("站点图标已更新");
  } catch (error) {
    message.error(error instanceof Error ? error.message : "图标上传失败");
  } finally {
    uploadingFavicon.value = false;
  }
}

async function handleChooseFavicon() {
  const item = await openImageLibrary("icon");
  if (!item) return;

  if (await saveSiteSettings({ favicon: item.url })) return;
  message.success("站点图标已更新");
}

async function handleClearFavicon() {
  if (await saveSiteSettings({ favicon: "" })) return;
  message.success("站点图标已恢复为内置图标");
}

function handleShowHeaderLogo(value: boolean) {
  void saveSiteSettings({ showHeaderLogo: value });
}
</script>

<template>
  <h2 class="settings__heading">基础</h2>
  <n-card size="small">
    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">网站标题</div>
        <div class="settings__row-desc">显示在浏览器标签页与登录页</div>
      </div>
      <div class="settings__row-control">
        <n-input
          v-model:value="siteTitle"
          :maxlength="SITE_TITLE_MAX_LENGTH"
          placeholder="网站标题"
          show-count
          @blur="saveSiteSettings({ siteTitle })"
          @keydown.enter="saveSiteSettings({ siteTitle })"
        />
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">副标题</div>
        <div class="settings__row-desc">显示在登录页标题下方，留空则不显示</div>
      </div>
      <div class="settings__row-control">
        <n-input
          v-model:value="siteSubtitle"
          :maxlength="SITE_SUBTITLE_MAX_LENGTH"
          placeholder="例如：Powered by marjin"
          show-count
          @blur="saveSiteSettings({ siteSubtitle })"
          @keydown.enter="saveSiteSettings({ siteSubtitle })"
        />
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">页脚文字</div>
        <div class="settings__row-desc">显示在页面底部，留空则不显示</div>
      </div>
      <div class="settings__row-control">
        <n-input
          v-model:value="footerText"
          :maxlength="FOOTER_TEXT_MAX_LENGTH"
          placeholder="例如：Powered by marjin"
          show-count
          @blur="saveSiteSettings({ footerText })"
          @keydown.enter="saveSiteSettings({ footerText })"
        />
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div class="settings__row settings__row--stack">
      <div class="settings__row-main">
        <div class="settings__row-label">站点图标</div>
        <div class="settings__row-desc">浏览器标签页与登录页显示的图标，留空使用内置图标</div>
      </div>
      <div class="settings__favicon">
        <img
          :src="settings.favicon || '/favicon.svg'"
          alt="站点图标"
          class="settings__favicon-preview"
        />
        <n-flex :size="8">
          <n-button :loading="uploadingFavicon" @click="handlePickFavicon"> 上传图标 </n-button>
          <n-button @click="handleChooseFavicon">从图库选择</n-button>
          <n-button v-if="settings.favicon" quaternary type="error" @click="handleClearFavicon">
            清除
          </n-button>
        </n-flex>
        <input
          ref="faviconInputRef"
          :accept="IMAGE_UPLOAD_LIMITS.icon.accept"
          hidden
          type="file"
          @change="handleFaviconFileChange"
        />
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">显示页头图标</div>
        <div class="settings__row-desc">在页头标题左侧显示站点图标</div>
      </div>
      <div class="settings__row-control">
        <n-switch :value="settings.showHeaderLogo" @update:value="handleShowHeaderLogo" />
      </div>
    </div>
  </n-card>
</template>

<style scoped>
.settings__favicon {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 12px;
}

.settings__favicon-preview {
  box-sizing: border-box;
  flex: 0 0 auto;
  width: 56px;
  height: 56px;
  padding: 4px;
  border-radius: 8px;
  background: rgba(127, 127, 127, 0.08);
  object-fit: contain;
}
</style>
