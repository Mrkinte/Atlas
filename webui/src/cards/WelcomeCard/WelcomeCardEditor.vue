<script lang="ts" setup>
import { computed, reactive } from "vue";
import type { CardEditorExpose } from "@/cards/types.ts";
import WelcomeCard from "@/cards/WelcomeCard/WelcomeCard.vue";
import type { WelcomeCardConfig } from "@/cards/WelcomeCard/config.ts";

/** 历史数据可能缺字段，全部可选 */
const props = defineProps<Partial<WelcomeCardConfig>>();

/** 补齐编辑态默认值 */
const editValues = reactive<WelcomeCardConfig>({
  title: props.title ?? "",
  subtitle: props.subtitle ?? "",
  titleColor: props.titleColor ?? "",
  subtitleColor: props.subtitleColor ?? "",
  align: props.align ?? "left",
});

/** 空值表示跟随主题默认色，选择器用 null 表示 */
const colorModel = (key: "titleColor" | "subtitleColor") =>
  computed({
    get: () => editValues[key] || null,
    set: (value: string | null) => {
      editValues[key] = value ?? "";
    },
  });

const titleColorValue = colorModel("titleColor");
const subtitleColorValue = colorModel("subtitleColor");

const alignOptions = [
  { label: "左对齐", value: "left" },
  { label: "居中", value: "center" },
  { label: "右对齐", value: "right" },
];

const handleAlignUpdate = (value: string | number) => {
  editValues.align = value as WelcomeCardConfig["align"];
};

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
      <div class="welcome-editor__preview">
        <WelcomeCard v-bind="editValues" />
      </div>
    </n-form-item>

    <n-grid :cols="2" x-gap="12">
      <n-gi>
        <n-form-item label="标题">
          <n-input
            v-model:value="editValues.title"
            :maxlength="32"
            placeholder="请输入标题"
            show-count
          />
        </n-form-item>
      </n-gi>
      <n-gi>
        <n-form-item label="标题颜色">
          <n-color-picker v-model:value="titleColorValue" :show-alpha="false" />
          <n-button quaternary style="margin-left: 12px" @click="editValues.titleColor = ''">
            跟随默认
          </n-button>
        </n-form-item>
      </n-gi>
    </n-grid>
    <n-grid :cols="2" x-gap="12">
      <n-gi>
        <n-form-item label="副标题">
          <n-input
            v-model:value="editValues.subtitle"
            :maxlength="64"
            placeholder="请输入副标题"
            show-count
          />
        </n-form-item>
      </n-gi>
      <n-gi>
        <n-form-item label="副标题颜色">
          <n-color-picker v-model:value="subtitleColorValue" :show-alpha="false" />
          <n-button quaternary style="margin-left: 12px" @click="editValues.subtitleColor = ''">
            跟随默认
          </n-button>
        </n-form-item>
      </n-gi>
    </n-grid>
    <n-form-item label="对齐方式">
      <n-radio-group :value="editValues.align" @update:value="handleAlignUpdate">
        <n-radio-button v-for="option in alignOptions" :key="option.value" :value="option.value">
          {{ option.label }}
        </n-radio-button>
      </n-radio-group>
    </n-form-item>
  </n-form>
</template>

<style scoped>
.welcome-editor__preview {
  width: 100%;
  /* 与一行卡片等高，字段为空时保持预览区高度 */
  min-height: 64px;
}
</style>
