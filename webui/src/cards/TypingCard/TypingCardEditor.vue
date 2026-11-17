<script lang="ts" setup>
import { computed, reactive } from "vue";
import type { CardEditorExpose } from "@/cards/types.ts";
import TypingCard from "@/cards/TypingCard/TypingCard.vue";
import { TYPING_CARD_FONT_OPTIONS, type TypingCardConfig } from "@/cards/TypingCard/config.ts";

/** 历史数据可能缺字段，全部可选 */
const props = defineProps<Partial<TypingCardConfig>>();

/** 补齐编辑态默认值 */
const editValues = reactive<TypingCardConfig>({
  text: props.text ?? "Hello, World!",
  color: props.color ?? "#4ade80",
  fontSize: props.fontSize ?? 32,
  fontFamily: props.fontFamily ?? "electrolize",
  typingSpeed: props.typingSpeed ?? 100,
  loop: props.loop ?? false,
  loopInterval: props.loopInterval ?? 2000,
});

const fontOptions = TYPING_CARD_FONT_OPTIONS.map((option) => ({ ...option }));

/** 空值表示跟随卡片文字颜色，选择器用 null 表示 */
const colorValue = computed({
  get: () => editValues.color || null,
  set: (value: string | null) => {
    editValues.color = value ?? "";
  },
});

const formatPxTooltip = (value: number) => `${value}px`;

const formatSpeedTooltip = (value: number) => `${value} 毫秒/字`;

const formatIntervalTooltip = (value: number) => `${value} 毫秒`;

async function validate(): Promise<boolean> {
  return true;
}

function getConfig() {
  return { ...editValues };
}

defineExpose({ validate, getConfig } satisfies CardEditorExpose);
</script>

<template>
  <n-form :model="editValues" label-placement="top">
    <n-form-item label="预览">
      <div class="typing-editor__preview">
        <TypingCard v-bind="editValues" />
      </div>
    </n-form-item>

    <n-form-item label="文字内容">
      <n-input
        v-model:value="editValues.text"
        :maxlength="200"
        :rows="2"
        placeholder="请输入要打出的文字"
        show-count
        type="textarea"
      />
    </n-form-item>

    <n-grid :cols="2" :x-gap="12">
      <n-gi>
        <n-form-item label="字体大小">
          <n-flex :size="12" align="center" style="width: 100%">
            <n-slider
              v-model:value="editValues.fontSize"
              :format-tooltip="formatPxTooltip"
              :max="96"
              :min="12"
              :step="1"
              style="flex: 1"
            />
            <n-input-number
              v-model:value="editValues.fontSize"
              :max="96"
              :min="12"
              :show-button="false"
              style="width: 92px"
            >
              <template #suffix>px</template>
            </n-input-number>
          </n-flex>
        </n-form-item>
      </n-gi>
      <n-gi>
        <n-form-item label="字体">
          <n-select v-model:value="editValues.fontFamily" :options="fontOptions" />
        </n-form-item>
      </n-gi>
    </n-grid>

    <n-grid :cols="2" :x-gap="12">
      <n-gi>
        <n-form-item label="文字颜色">
          <n-color-picker v-model:value="colorValue" :show-alpha="false" />
          <n-button quaternary style="margin-left: 12px" @click="editValues.color = ''">
            跟随卡片
          </n-button>
        </n-form-item>
      </n-gi>
      <n-gi>
        <n-form-item label="打字速度（越小越快）">
          <n-slider
            v-model:value="editValues.typingSpeed"
            :format-tooltip="formatSpeedTooltip"
            :max="300"
            :min="20"
            :step="10"
          />
        </n-form-item>
      </n-gi>
    </n-grid>

    <n-grid :cols="2" :x-gap="12">
      <n-gi>
        <n-form-item label="循环播放">
          <n-switch v-model:value="editValues.loop" />
        </n-form-item>
      </n-gi>
      <n-gi>
        <n-form-item label="循环间隔">
          <n-slider
            v-model:value="editValues.loopInterval"
            :disabled="!editValues.loop"
            :format-tooltip="formatIntervalTooltip"
            :max="10000"
            :min="0"
            :step="500"
          />
        </n-form-item>
      </n-gi>
    </n-grid>
  </n-form>
</template>

<style scoped>
.typing-editor__preview {
  width: 100%;
  height: 120px;
}
</style>
