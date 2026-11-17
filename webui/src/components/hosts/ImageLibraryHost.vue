<script lang="ts" setup>
import { computed, ref, watch } from "vue";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import Checkmark24Regular from "@vicons/fluent/es/Checkmark24Regular.js";
import Delete24Regular from "@vicons/fluent/es/Delete24Regular.js";
import { deleteImage, listImages, uploadImageWithProgress } from "@/apis/modules/images.ts";
import { NETWORK_ERROR } from "@/config/messages.ts";
import type { components } from "@/apis/schema";
import { fetchSiteFavicon } from "@/services/favicon.ts";
import { isValidHttpUrl } from "@/cards/ServiceCard/target.ts";
import {
  closeImageLibrary,
  type LibraryItem,
  libraryKind,
  selectLibraryItem,
  showLibrary,
} from "@/composables/useImageLibraryHost.ts";
import { IMAGE_UPLOAD_LIMITS } from "@/composables/useImageUpload.ts";

type ImageResponse = components["schemas"]["ImageResponse"];

/** 上传限制复用 useImageUpload，避免与卡片编辑器各写一份 */
const KIND_CONFIG = IMAGE_UPLOAD_LIMITS;

const kindOptions = [
  { label: "图标", value: "icon" },
  { label: "图片", value: "image" },
];

const message = useAppMessage();

const config = computed(() => KIND_CONFIG[libraryKind.value]);

const items = ref<LibraryItem[]>([]);
const loading = ref(false);

const uploading = ref(false);
const uploadPercent = ref(0);
const uploadName = ref("");
const uploadCurrent = ref(0);
const uploadTotal = ref(0);

const uploadHint = computed(() =>
  uploadTotal.value > 1
    ? `正在上传 ${uploadCurrent.value}/${uploadTotal.value} · ${uploadName.value}`
    : `正在上传 ${uploadName.value}`,
);

const fileInputRef = ref<HTMLInputElement | null>(null);

/** 由服务端抓取目标网址图标并入库 */
const faviconModalShow = ref(false);
const faviconUrl = ref("");
const fetchingFavicon = ref(false);

const faviconUrlValid = computed(() => isValidHttpUrl(faviconUrl.value));

function openFaviconModal() {
  faviconUrl.value = "";
  faviconModalShow.value = true;
}

/** 抓取站点图标后刷新列表，失败时对话框保持打开 */
async function handleFetchFavicon(): Promise<boolean> {
  if (fetchingFavicon.value) return false;
  if (!faviconUrlValid.value) {
    message.warning("请输入完整网址（含 http:// 或 https://）");
    return false;
  }

  fetchingFavicon.value = true;
  // 失败提示按目标网址是否可访问给出，不透出接口错误文案
  const result = await fetchSiteFavicon(faviconUrl.value.trim());
  fetchingFavicon.value = false;

  if (!result.ok) {
    message.error("获取图标失败，请确认目标网址可访问");
    return false;
  }

  message.success("已自动获取网站图标");
  faviconModalShow.value = false;
  await fetchItems();
  return true;
}

function handlePickFile() {
  if (uploading.value) return;
  fileInputRef.value?.click();
}

async function handleFileChange(event: Event) {
  const input = event.target as HTMLInputElement;
  const files = Array.from(input.files ?? []);
  input.value = "";
  if (!files.length) return;

  const { label, maxSize } = config.value;
  const sizeLimit = Math.round(maxSize / 1024 / 1024);
  const kind = libraryKind.value;

  // 超限文件先剔除，避免逐个弹提示打断批量流程
  const tooLarge = files.filter((file) => file.size > maxSize);
  const pending = files.filter((file) => file.size <= maxSize);

  if (files.length === 1) {
    if (!pending.length) {
      message.error(`${label}文件不能超过 ${sizeLimit} MB`);
      return;
    }
  }

  uploading.value = true;
  uploadTotal.value = pending.length;
  uploadCurrent.value = 0;

  let succeeded = 0;
  let firstError = "";

  for (const file of pending) {
    uploadCurrent.value += 1;
    uploadName.value = file.name;
    uploadPercent.value = 0;
    try {
      await uploadImageWithProgress(file, kind, (percent) => {
        uploadPercent.value = percent;
      });
      succeeded += 1;
    } catch (err) {
      if (!firstError) firstError = (err as Error).message;
    }
  }

  uploading.value = false;
  uploadPercent.value = 0;
  uploadName.value = "";
  uploadCurrent.value = 0;
  uploadTotal.value = 0;

  const failed = pending.length - succeeded;

  if (files.length === 1) {
    if (succeeded) message.success(`${label}上传成功`);
    else message.error(firstError);
  } else {
    const parts: string[] = [];
    if (succeeded) parts.push(`成功 ${succeeded} 个`);
    if (tooLarge.length) {
      parts.push(`${tooLarge.length} 个超过 ${sizeLimit} MB 已跳过`);
    }
    if (failed) {
      parts.push(`失败 ${failed} 个${firstError ? `（${firstError}）` : ""}`);
    }
    const text = `已上传${label}：${parts.join("，")}`;
    if (!succeeded) message.error(text);
    else if (failed || tooLarge.length) message.warning(text);
    else message.success(text);
  }

  if (succeeded) await fetchItems();
}

/** 请求序号，避免快速切换分类时旧请求覆盖新列表 */
let fetchToken = 0;

async function fetchItems() {
  const token = ++fetchToken;
  const kind = libraryKind.value;
  loading.value = true;
  try {
    const { data, response } = await listImages({ kind });
    if (token !== fetchToken) return;
    if (!response.ok) {
      message.error("图片加载失败");
      return;
    }
    if (!data) return;

    items.value = data.map((image: ImageResponse): LibraryItem => ({
      id: image.id,
      originalName: image.originalName,
      url: image.url,
    }));
  } catch {
    if (token === fetchToken) message.error(NETWORK_ERROR);
  } finally {
    if (token === fetchToken) loading.value = false;
  }
}

/** 切换分类时先清空列表，避免短暂显示上一分类的素材 */
watch(libraryKind, () => {
  items.value = [];
  if (showLibrary.value) void fetchItems();
});

// 每次打开都重新拉取，能拿到其他会话上传的素材
watch(showLibrary, (visible) => {
  if (visible) void fetchItems();
});

function handleSelect(item: LibraryItem) {
  selectLibraryItem(item);
}

async function handleDelete(item: LibraryItem) {
  try {
    const { response } = await deleteImage(item.id);
    if (response.status === 404) {
      message.error("删除失败，数据库找不到相应的内容");
      return;
    }
  } catch {
    message.error(NETWORK_ERROR);
    return;
  }

  items.value = items.value.filter((i) => i.id !== item.id);
  message.success("删除成功");
}

/** 关闭弹窗视为取消 */
function handleShowChange(visible: boolean) {
  if (!visible && !uploading.value) closeImageLibrary();
}
</script>

<template>
  <Teleport to="body">
    <n-modal
      :closable="!uploading"
      :close-on-esc="!uploading"
      :mask-closable="!uploading"
      :show="showLibrary"
      preset="card"
      style="max-width: var(--editor-max-width)"
      title="图库"
      @update:show="handleShowChange"
    >
      <div class="library">
        <div v-if="uploading" class="library__progress">
          <n-flex align="center" justify="space-between">
            <n-text class="library__progress-name" depth="3">
              {{ uploadHint }}
            </n-text>
            <n-text depth="3">{{ uploadPercent }}%</n-text>
          </n-flex>
          <n-progress :percentage="uploadPercent" :show-indicator="false" processing type="line" />
        </div>

        <n-spin :show="loading">
          <div class="library__body">
            <div v-if="items.length" class="library__grid">
              <div v-for="item in items" :key="item.id" class="library__item">
                <div class="library__preview">
                  <n-image :alt="item.originalName" :src="item.url" object-fit="contain" />
                </div>
                <n-ellipsis :tooltip="true" class="library__name">
                  {{ item.originalName }}
                </n-ellipsis>
                <div class="library__actions">
                  <n-tooltip trigger="hover">
                    <template #trigger>
                      <n-button quaternary size="small" type="primary" @click="handleSelect(item)">
                        <template #icon>
                          <n-icon><Checkmark24Regular /></n-icon>
                        </template>
                      </n-button>
                    </template>
                    选择
                  </n-tooltip>
                  <n-tooltip trigger="hover">
                    <template #trigger>
                      <n-button quaternary size="small" type="error" @click="handleDelete(item)">
                        <template #icon>
                          <n-icon><Delete24Regular /></n-icon>
                        </template>
                      </n-button>
                    </template>
                    删除
                  </n-tooltip>
                </div>
              </div>
            </div>
            <n-empty v-else-if="!loading" :description="`暂无已上传的${config.label}`" />
          </div>
        </n-spin>
      </div>

      <input
        ref="fileInputRef"
        :accept="config.accept"
        hidden
        multiple
        type="file"
        @change="handleFileChange"
      />

      <template #footer>
        <div class="library__footer">
          <n-flex align="center">
            <span> 类别 </span>
            <n-select
              v-model:value="libraryKind"
              :disabled="uploading"
              :options="kindOptions"
              style="width: 112px"
            />
          </n-flex>
          <n-flex align="center">
            <n-button v-if="libraryKind === 'icon'" :disabled="uploading" @click="openFaviconModal">
              获取网站图标
            </n-button>
            <n-button :disabled="uploading" type="primary" @click="handlePickFile">
              上传{{ config.label }}
            </n-button>
          </n-flex>
        </div>
      </template>
    </n-modal>

    <n-modal
      v-model:show="faviconModalShow"
      :closable="!fetchingFavicon"
      :close-on-esc="!fetchingFavicon"
      :loading="fetchingFavicon"
      :mask-closable="!fetchingFavicon"
      :positive-button-props="{ disabled: !faviconUrlValid }"
      negative-text="取消"
      positive-text="获取"
      preset="dialog"
      title="获取网站图标"
      @positive-click="handleFetchFavicon"
    >
      <n-input
        v-model:value="faviconUrl"
        :disabled="fetchingFavicon"
        placeholder="https://example.com"
        @keyup.enter="handleFetchFavicon"
      />
      <n-text class="favicon-dialog__tip" depth="3">
        抓取该网站声明的站点图标，成功后保存到图标库
      </n-text>
    </n-modal>
  </Teleport>
</template>

<style scoped>
.library__progress {
  margin-bottom: 12px;
}

.library__progress-name {
  max-width: 70%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.library__body {
  min-height: 160px;
}

.library__grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(104px, 1fr));
  gap: 12px;
  padding: 4px 2px;
}

.library__item {
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid rgba(128, 128, 128, 0.16);
  border-radius: 10px;
  background: rgba(128, 128, 128, 0.05);
  transition:
    border-color 0.2s ease,
    box-shadow 0.2s ease,
    transform 0.2s ease;
}

.library__item:hover {
  border-color: rgba(128, 128, 128, 0.32);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
  transform: translateY(-1px);
}

.library__preview {
  display: flex;
  align-items: center;
  justify-content: center;
  aspect-ratio: 1 / 1;
  padding: 10px;
  cursor: zoom-in;
  /* 透明背景的棋盘格，便于辨认图标透明区域 */
  background-color: rgba(128, 128, 128, 0.04);
  background-image:
    linear-gradient(45deg, rgba(128, 128, 128, 0.08) 25%, transparent 25%),
    linear-gradient(-45deg, rgba(128, 128, 128, 0.08) 25%, transparent 25%),
    linear-gradient(45deg, transparent 75%, rgba(128, 128, 128, 0.08) 75%),
    linear-gradient(-45deg, transparent 75%, rgba(128, 128, 128, 0.08) 75%);
  background-position:
    0 0,
    0 6px,
    6px -6px,
    -6px 0;
  background-size: 12px 12px;
}

.library__preview :deep(.n-image) {
  width: 100%;
  height: 100%;
}

.library__preview :deep(.n-image img) {
  width: 100%;
  height: 100%;
  object-fit: contain;
}

.library__name {
  margin-top: 6px;
  padding: 0 8px;
  font-size: 12px;
  line-height: 1.4;
  text-align: center;
}

.library__actions {
  display: flex;
  justify-content: center;
  gap: 12px;
  padding: 2px 8px 8px;
}

.library__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  width: 100%;
}

.favicon-dialog__tip {
  display: block;
  margin-top: 8px;
  font-size: 12px;
}
</style>
