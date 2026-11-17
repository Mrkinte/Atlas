<script lang="ts" setup>
import { computed, reactive, ref } from "vue";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import ArrowDown24Regular from "@vicons/fluent/es/ArrowDown24Regular.js";
import ArrowUp24Regular from "@vicons/fluent/es/ArrowUp24Regular.js";
import ArrowUpload24Regular from "@vicons/fluent/es/ArrowUpload24Regular.js";
import Delete24Regular from "@vicons/fluent/es/Delete24Regular.js";
import ImageMultiple24Regular from "@vicons/fluent/es/ImageMultiple24Regular.js";
import type { CardEditorExpose } from "@/cards/types.ts";
import { openImageLibrary } from "@/composables/useImageLibraryHost.ts";
import { uploadImageWithProgress } from "@/apis/modules/images.ts";
import { IMAGE_UPLOAD_LIMITS } from "@/composables/useImageUpload.ts";
import SlideCard from "@/cards/SlideCard/SlideCard.vue";
import type { SlideCardConfig, SlideItem, SlideObjectFit } from "@/cards/SlideCard/config.ts";

const message = useAppMessage();

/** 历史数据可能缺字段，全部可选 */
const props = defineProps<Partial<SlideCardConfig>>();

/**
 * 补齐编辑态默认值
 * slides 逐项复制，避免共享引用污染其它卡片的初始值
 */
const editValues = reactive<SlideCardConfig>({
  slides: (props.slides ?? [])
    .filter((item) => Boolean(item?.url))
    .map((item) => ({ url: item.url, title: item.title ?? "" })),
  autoplay: props.autoplay ?? true,
  interval: props.interval ?? 5,
  objectFit: props.objectFit === "contain" ? "contain" : "cover",
  showTitle: props.showTitle ?? true,
  showDots: props.showDots ?? true,
  showArrow: props.showArrow ?? true,
  titleColor: props.titleColor ?? "",
});

const objectFitOptions: Array<{ label: string; value: SlideObjectFit }> = [
  { label: "裁剪填满", value: "cover" },
  { label: "完整显示", value: "contain" },
];

/** 空值表示使用默认白色，选择器用 null 表示 */
const titleColorValue = computed({
  get: () => editValues.titleColor || null,
  set: (value: string | null) => {
    editValues.titleColor = value ?? "";
  },
});

const handleObjectFitUpdate = (value: string | number) => {
  editValues.objectFit = value as SlideObjectFit;
};

/** 从图库追加，图库内也可上传 */
async function handleAddFromLibrary() {
  const item = await openImageLibrary("image");
  if (!item) return;
  editValues.slides.push({ url: item.url, title: "" });
}

/** 替换图片，标题保留在卡片配置里 */
async function handleReplaceSlide(index: number) {
  const item = await openImageLibrary("image");
  if (!item) return;
  const slide = editValues.slides[index];
  if (slide) slide.url = item.url;
}

/** 轮播按数组顺序展示 */
function moveSlide(index: number, offset: number) {
  const target = index + offset;
  if (target < 0 || target >= editValues.slides.length) return;
  const [moved] = editValues.slides.splice(index, 1);
  editValues.slides.splice(target, 0, moved);
}

function removeSlide(index: number) {
  editValues.slides.splice(index, 1);
}

const fileInputRef = ref<HTMLInputElement | null>(null);
const uploading = ref(false);
const uploadPercent = ref(0);
const uploadName = ref("");

function handlePickFile() {
  if (uploading.value) return;
  fileInputRef.value?.click();
}

async function handleFileChange(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = "";
  if (!file) return;

  const { label, maxSize } = IMAGE_UPLOAD_LIMITS.image;
  if (file.size > maxSize) {
    message.error(`${label}文件不能超过 ${Math.round(maxSize / 1024 / 1024)} MB`);
    return;
  }

  uploading.value = true;
  uploadPercent.value = 0;
  uploadName.value = file.name;
  try {
    const data = await uploadImageWithProgress(file, "image", (percent) => {
      uploadPercent.value = percent;
    });
    editValues.slides.push({ url: data.url, title: "" });
    message.success("图片上传成功");
  } catch (err) {
    message.error((err as Error).message);
  } finally {
    uploading.value = false;
    uploadPercent.value = 0;
    uploadName.value = "";
  }
}

async function validate(): Promise<boolean> {
  if (!editValues.slides.length) {
    message.warning("请至少添加一张图片");
    return false;
  }

  const interval = Number(editValues.interval);
  if (!Number.isFinite(interval) || interval < 1) {
    message.warning("轮播间隔请填写 1-60 秒");
    return false;
  }

  return true;
}

function getConfig() {
  // slides 深拷贝一层，避免保存后编辑器数据与卡片共享引用
  return {
    ...editValues,
    slides: editValues.slides.map((item): SlideItem => ({
      url: item.url,
      title: item.title,
    })),
  };
}

defineExpose({ validate, getConfig } satisfies CardEditorExpose);
</script>

<template>
  <n-form :model="editValues" label-placement="top">
    <n-form-item label="预览">
      <div class="slide-editor__preview">
        <SlideCard v-bind="editValues" />
      </div>
    </n-form-item>

    <n-grid :cols="2" :x-gap="12">
      <n-gi>
        <n-form-item label="自动轮播">
          <n-flex :size="12" align="center">
            <n-switch v-model:value="editValues.autoplay" />
            <n-input-number
              v-model:value="editValues.interval"
              :disabled="!editValues.autoplay"
              :max="60"
              :min="1"
              :show-button="false"
              placeholder="间隔"
              style="width: 92px"
            >
              <template #suffix> 秒 </template>
            </n-input-number>
          </n-flex>
        </n-form-item>
      </n-gi>
      <n-gi>
        <n-form-item label="图片填充">
          <n-radio-group :value="editValues.objectFit" @update:value="handleObjectFitUpdate">
            <n-radio-button
              v-for="option in objectFitOptions"
              :key="option.value"
              :value="option.value"
            >
              {{ option.label }}
            </n-radio-button>
          </n-radio-group>
        </n-form-item>
      </n-gi>
    </n-grid>

    <n-form-item label="标题颜色">
      <n-flex :size="8" :wrap="false" align="center">
        <n-color-picker
          v-model:value="titleColorValue"
          :disabled="!editValues.showTitle"
          :show-alpha="false"
          style="width: 160px"
        />
        <n-button :disabled="!editValues.showTitle" quaternary @click="editValues.titleColor = ''">
          默认白色
        </n-button>
      </n-flex>
    </n-form-item>

    <n-form-item label="显示内容">
      <n-flex :size="24" align="center">
        <n-checkbox v-model:checked="editValues.showTitle"> 标题 </n-checkbox>
        <n-checkbox v-model:checked="editValues.showDots"> 指示点 </n-checkbox>
        <n-checkbox v-model:checked="editValues.showArrow"> 左右箭头 </n-checkbox>
      </n-flex>
    </n-form-item>

    <n-divider style="margin: 4px 0 12px" title-placement="left">
      图片（{{ editValues.slides.length }}）
    </n-divider>

    <div class="slide-editor__list">
      <div v-for="(item, index) in editValues.slides" :key="index" class="slide-editor__item">
        <n-tooltip placement="left" trigger="hover">
          <template #trigger>
            <div class="slide-editor__thumb" @click="handleReplaceSlide(index)">
              <img :src="item.url" alt="" />
              <span class="slide-editor__badge">{{ index + 1 }}</span>
            </div>
          </template>
          点击更换图片
        </n-tooltip>

        <div class="slide-editor__title">
          <n-input
            v-model:value="item.title"
            :maxlength="32"
            clearable
            placeholder="图片标题（可选，仅保存在本卡片）"
            size="small"
          />
        </div>

        <n-flex :size="0" :wrap="false">
          <n-tooltip trigger="hover">
            <template #trigger>
              <n-button
                :disabled="index === 0"
                quaternary
                size="small"
                @click="moveSlide(index, -1)"
              >
                <template #icon>
                  <n-icon><ArrowUp24Regular /></n-icon>
                </template>
              </n-button>
            </template>
            上移
          </n-tooltip>
          <n-tooltip trigger="hover">
            <template #trigger>
              <n-button
                :disabled="index === editValues.slides.length - 1"
                quaternary
                size="small"
                @click="moveSlide(index, 1)"
              >
                <template #icon>
                  <n-icon><ArrowDown24Regular /></n-icon>
                </template>
              </n-button>
            </template>
            下移
          </n-tooltip>
          <n-tooltip trigger="hover">
            <template #trigger>
              <n-button quaternary size="small" type="error" @click="removeSlide(index)">
                <template #icon>
                  <n-icon><Delete24Regular /></n-icon>
                </template>
              </n-button>
            </template>
            移除
          </n-tooltip>
        </n-flex>
      </div>

      <div v-if="!editValues.slides.length" class="slide-editor__empty">
        还没有图片，从图库选择或直接上传
      </div>
    </div>

    <n-flex :size="12" align="center" style="margin-top: 12px">
      <n-button :disabled="uploading" @click="handleAddFromLibrary">
        <template #icon>
          <n-icon><ImageMultiple24Regular /></n-icon>
        </template>
        从图库选择
      </n-button>
      <n-button :loading="uploading" @click="handlePickFile">
        <template #icon>
          <n-icon><ArrowUpload24Regular /></n-icon>
        </template>
        上传图片
      </n-button>
      <n-text v-if="uploading" class="slide-editor__upload-name" depth="3">
        正在上传 {{ uploadName }}（{{ uploadPercent }}%）
      </n-text>
    </n-flex>

    <!-- 上传后写入图库，卡片只记录地址 -->
    <input
      ref="fileInputRef"
      :accept="IMAGE_UPLOAD_LIMITS.image.accept"
      hidden
      type="file"
      @change="handleFileChange"
    />
  </n-form>
</template>

<style scoped>
.slide-editor__preview {
  width: 100%;
  height: 148px;
}

.slide-editor__list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  /* 图片较多时列表内滚动，避免弹窗无限变高 */
  max-height: 258px;
  overflow-y: auto;
  padding-right: 4px;
}

.slide-editor__item {
  display: flex;
  flex: none;
  gap: 10px;
  align-items: center;
  padding: 6px;
  border: 1px solid rgba(128, 128, 128, 0.16);
  border-radius: 10px;
  background-color: rgba(128, 128, 128, 0.05);
  transition:
    border-color 0.2s ease,
    background-color 0.2s ease;
}

.slide-editor__item:hover {
  border-color: rgba(128, 128, 128, 0.32);
}

.slide-editor__thumb {
  position: relative;
  flex: none;
  width: 44px;
  height: 44px;
  overflow: hidden;
  border-radius: 8px;
  background-color: rgba(128, 128, 128, 0.12);
  cursor: pointer;
}

.slide-editor__thumb img {
  display: block;
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.slide-editor__thumb:hover img {
  opacity: 0.7;
}

.slide-editor__badge {
  position: absolute;
  top: 2px;
  left: 2px;
  padding: 0 5px;
  border-radius: 6px;
  color: #fff;
  background-color: rgba(0, 0, 0, 0.5);
  font-size: 11px;
  line-height: 16px;
}

.slide-editor__title {
  flex: 1 1 auto;
  min-width: 0;
}

.slide-editor__empty {
  padding: 20px 0;
  border: 1px dashed rgba(128, 128, 128, 0.32);
  border-radius: 10px;
  font-size: 12px;
  text-align: center;
  opacity: 0.55;
}

.slide-editor__upload-name {
  overflow: hidden;
  font-size: 12px;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
