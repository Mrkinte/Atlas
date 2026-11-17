<script lang="ts" setup>
import { computed, reactive } from "vue";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import type { CardEditorExpose } from "@/cards/types.ts";
import { openImageLibrary } from "@/composables/useImageLibraryHost.ts";
import CountdownCard from "@/cards/CountdownCard/CountdownCard.vue";
import type { CountdownCardConfig } from "@/cards/CountdownCard/config.ts";
import { formatDateValue, parseDateValue } from "@/cards/CountdownCard/date.ts";

const message = useAppMessage();

/** 历史数据可能缺字段，全部可选 */
const props = defineProps<Partial<CountdownCardConfig>>();

/** 补齐编辑态默认值 */
const editValues = reactive<CountdownCardConfig>({
  title: props.title ?? "",
  date: props.date ?? "",
  repeatYearly: props.repeatYearly ?? false,
  backgroundColor: props.backgroundColor ?? "",
  backgroundImage: props.backgroundImage ?? "",
});

/** 日期选择器使用时间戳，配置中保存 YYYY-MM-DD */
const dateValue = computed<number | null>({
  get() {
    const parsed = parseDateValue(editValues.date);
    return parsed ? parsed.getTime() : null;
  },
  set(value) {
    editValues.date = value ? formatDateValue(new Date(value)) : "";
  },
});

/** 空值表示透明，选择器用 null 表示 */
const backgroundColorValue = computed<string | null>({
  get: () => editValues.backgroundColor || null,
  set: (value) => {
    editValues.backgroundColor = value ?? "";
  },
});

/** 图库内也可直接上传 */
async function handleSelectBackgroundImage() {
  const item = await openImageLibrary("image");
  if (!item) return;
  editValues.backgroundImage = item.url;
}

async function validate(): Promise<boolean> {
  if (!editValues.title.trim()) {
    message.warning("请输入事件名称");
    return false;
  }

  if (!parseDateValue(editValues.date)) {
    message.warning("请选择目标日期");
    return false;
  }

  return true;
}

function getConfig() {
  return { ...editValues, title: editValues.title.trim() };
}

defineExpose({ validate, getConfig } satisfies CardEditorExpose);
</script>

<template>
  <n-form :model="editValues" label-placement="top">
    <n-form-item label="预览">
      <div class="countdown-editor__preview">
        <CountdownCard v-bind="editValues" />
      </div>
    </n-form-item>

    <n-grid :cols="2" :x-gap="12">
      <n-gi>
        <n-form-item label="事件名称">
          <n-input
            v-model:value="editValues.title"
            :maxlength="20"
            placeholder="例如：春节"
            show-count
          />
        </n-form-item>
      </n-gi>
      <n-gi>
        <n-form-item label="目标日期">
          <n-date-picker
            v-model:value="dateValue"
            placeholder="请选择日期"
            style="width: 100%"
            type="date"
          />
        </n-form-item>
      </n-gi>
    </n-grid>

    <n-form-item label="每年重复">
      <div class="countdown-editor__option">
        <n-switch v-model:value="editValues.repeatYearly" />
        <span class="countdown-editor__hint"> 生日、纪念日等每年发生的日期会取下一次的日期 </span>
      </div>
    </n-form-item>

    <n-grid :cols="2" :x-gap="12">
      <n-gi>
        <n-form-item label="背景色">
          <n-flex :size="8" :wrap="false" align="center">
            <n-color-picker
              v-model:value="backgroundColorValue"
              :show-alpha="true"
              style="min-width: 160px"
            />
            <n-button quaternary @click="editValues.backgroundColor = ''"> 透明 </n-button>
          </n-flex>
        </n-form-item>
      </n-gi>
      <n-gi>
        <n-form-item label="背景图">
          <n-flex :size="8" :wrap="false" align="center">
            <n-image
              v-if="editValues.backgroundImage"
              :src="editValues.backgroundImage"
              alt="背景图预览"
              class="countdown-editor__thumb"
              object-fit="cover"
            />
            <n-button @click="handleSelectBackgroundImage"> 选择图片 </n-button>
            <n-button
              v-if="editValues.backgroundImage"
              quaternary
              size="small"
              type="error"
              @click="editValues.backgroundImage = ''"
            >
              清除
            </n-button>
          </n-flex>
        </n-form-item>
      </n-gi>
    </n-grid>
  </n-form>
</template>

<style scoped>
.countdown-editor__preview {
  width: 100%;
  height: 96px;
}

.countdown-editor__option {
  display: flex;
  align-items: center;
  gap: 10px;
}

.countdown-editor__hint {
  font-size: 0.8rem;
  opacity: 0.6;
}

.countdown-editor__thumb {
  flex: 0 0 auto;
  width: 34px;
  height: 34px;
  overflow: hidden;
  border-radius: 6px;
}

.countdown-editor__thumb :deep(img) {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
</style>
