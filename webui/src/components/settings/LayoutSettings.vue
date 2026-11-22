<script lang="ts" setup>
import { ref } from "vue";
import { useDialog } from "naive-ui";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import { deleteAllCards } from "@/composables/useCards.ts";
import { deleteAllGroups } from "@/composables/useCardGroups.ts";
import { SETTINGS_LIMITS } from "@/config/default.ts";
import { useSettingsSave } from "@/composables/useSettingsSave.ts";
import { settings } from "@/stores/settings.ts";
import { desktopGridContainerMinWidth } from "@/utils/cardLayoutCalculate.ts";

const message = useAppMessage();
const dialog = useDialog();
const saveSiteSettings = useSettingsSave();

const deletingCards = ref(false);
const deletingGroups = ref(false);

function handleDesktopCols(value: number | null) {
  if (value !== null) void saveSiteSettings({ desktopCols: value });
}

function handleMaxWidth(value: number | null) {
  if (value !== null) {
    void saveSiteSettings({ desktopGridContainerMaxWidth: value });
  }
}

function handleFullWidth(value: boolean) {
  void saveSiteSettings({ desktopGridFullWidth: value });
}

function handleDeleteCards() {
  if (deletingCards.value) return;

  dialog.warning({
    title: "删除所有卡片",
    content: "将删除首页的全部卡片，且无法撤销。建议先备份。",
    positiveText: "全部删除",
    negativeText: "取消",
    onPositiveClick: () => {
      void runDeleteCards();
    },
  });
}

function handleDeleteGroups() {
  if (deletingGroups.value) return;

  dialog.warning({
    title: "删除所有分组",
    content: "将删除首页的全部分组（包括卡片），且无法撤销。建议先备份。",
    positiveText: "全部删除",
    negativeText: "取消",
    onPositiveClick: () => {
      void runDeleteGroups();
    },
  });
}

async function runDeleteCards() {
  deletingCards.value = true;
  try {
    const error = await deleteAllCards();
    if (error) {
      message.error(error);
      return;
    }
    message.success("已删除所有卡片");
  } finally {
    deletingCards.value = false;
  }
}

async function runDeleteGroups() {
  deletingGroups.value = true;
  try {
    const error = await deleteAllGroups();
    if (error) {
      message.error(error);
      return;
    }
    message.success("已删除所有分组");
  } finally {
    deletingGroups.value = false;
  }
}
</script>

<template>
  <h2 class="settings__heading">布局</h2>
  <n-card size="small">
    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">桌面列数</div>
        <div class="settings__row-desc">
          桌面端卡片栅格的列数，范围
          {{ SETTINGS_LIMITS.desktopCols.min }}–{{ SETTINGS_LIMITS.desktopCols.max }}
        </div>
      </div>
      <div class="settings__row-control">
        <n-input-number
          :max="SETTINGS_LIMITS.desktopCols.max"
          :min="SETTINGS_LIMITS.desktopCols.min"
          :show-button="false"
          :value="settings.desktopCols"
          @update:value="handleDesktopCols"
        />
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">容器宽度</div>
        <div class="settings__row-desc">
          桌面端卡片区域的宽度，范围
          {{ desktopGridContainerMinWidth(settings.desktopCols) }}–{{
            SETTINGS_LIMITS.desktopGridContainerMaxWidth.max
          }}（px）
        </div>
      </div>
      <div class="settings__row-control">
        <n-input-number
          :disabled="settings.desktopGridFullWidth"
          :max="SETTINGS_LIMITS.desktopGridContainerMaxWidth.max"
          :min="desktopGridContainerMinWidth(settings.desktopCols)"
          :show-button="false"
          :step="10"
          :value="settings.desktopGridContainerMaxWidth"
          @update:value="handleMaxWidth"
        />
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">铺满屏幕</div>
        <div class="settings__row-desc">忽略容器宽度，卡片区域按屏幕宽度铺满</div>
      </div>
      <div class="settings__row-control">
        <n-switch :value="settings.desktopGridFullWidth" @update:value="handleFullWidth" />
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">删除所有卡片</div>
        <div class="settings__row-desc">清空首页的全部卡片，操作不可撤销</div>
      </div>
      <div class="settings__row-control">
        <n-button :loading="deletingCards" secondary type="error" @click="handleDeleteCards">
          全部删除
        </n-button>
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">删除所有分组</div>
        <div class="settings__row-desc">清空首页的全部分组（包括卡片），操作不可撤销</div>
      </div>
      <div class="settings__row-control">
        <n-button :loading="deletingGroups" secondary type="error" @click="handleDeleteGroups">
          全部删除
        </n-button>
      </div>
    </div>
  </n-card>
</template>

<style scoped></style>
