<script lang="ts" setup>
import { computed, ref } from "vue";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import {
  BACKUP_SECTIONS,
  type BackupInspectResult,
  type BackupRestoreResult,
  type BackupSection,
  exportBackup,
  inspectBackup,
  restoreBackup,
} from "@/composables/useBackup.ts";

/** 与后端一致的备份包大小上限 */
const BACKUP_MAX_SIZE = 512 * 1024 * 1024;

type SectionOption = {
  key: BackupSection;
  label: string;
  desc: string;
};

const SECTION_OPTIONS: SectionOption[] = [
  { key: "cards", label: "卡片", desc: "首页卡片布局与卡片配置" },
  { key: "settings", label: "站点设置", desc: "标题、外观、页脚等站点配置" },
  { key: "images", label: "图库", desc: "上传的图片与图标" },
  { key: "live2d", label: "Live2D", desc: "看板娘开关、模型与行为配置" },
];

const message = useAppMessage();

const exporting = ref(false);
const inspecting = ref(false);
const restoring = ref(false);
const fileInputRef = ref<HTMLInputElement | null>(null);

const exportSections = ref<BackupSection[]>([...BACKUP_SECTIONS]);

const restoreVisible = ref(false);
const restoreFile = ref<File | null>(null);
const inspectResult = ref<BackupInspectResult | null>(null);
const restoreSections = ref<BackupSection[]>([]);

const imageWarning = computed(
  () =>
    inspectResult.value?.sections.includes("images") && !restoreSections.value.includes("images"),
);

const exportImageWarning = computed(() => !exportSections.value.includes("images"));

function toggleSection(list: BackupSection[], key: BackupSection, checked: boolean) {
  const index = list.indexOf(key);
  if (checked && index < 0) list.push(key);
  if (!checked && index >= 0) list.splice(index, 1);
}

function isAvailable(key: BackupSection): boolean {
  return inspectResult.value?.sections.includes(key) ?? false;
}

function isSelected(key: BackupSection): boolean {
  return restoreSections.value.includes(key);
}

function sectionSummary(key: BackupSection): string {
  const result = inspectResult.value;
  if (!result) return "";

  switch (key) {
    case "cards":
      return `${result.cards} 张`;
    case "images":
      return `${result.images} 个`;
    case "live2d":
      return `${result.live2DConfigs} 个模型配置`;
    default:
      return "已包含";
  }
}

function formatExportedAt(value: string | undefined): string {
  if (!value) return "";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

async function handleExport() {
  if (exporting.value || exportSections.value.length === 0) return;

  exporting.value = true;
  try {
    const error = await exportBackup(exportSections.value);
    if (error) {
      message.error(error);
      return;
    }
    message.success("备份已导出");
  } finally {
    exporting.value = false;
  }
}

function handlePickFile() {
  if (restoring.value || inspecting.value) return;
  fileInputRef.value?.click();
}

function handleFileChange(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = "";
  if (!file) return;

  if (!file.name.toLowerCase().endsWith(".zip")) {
    message.error("请选择 ZIP 备份文件");
    return;
  }
  if (file.size > BACKUP_MAX_SIZE) {
    message.error("备份文件不能超过 512 MB");
    return;
  }

  void inspectBackupFile(file);
}

async function inspectBackupFile(file: File) {
  inspecting.value = true;
  try {
    const { data, error } = await inspectBackup(file);
    if (error || !data) {
      message.error(error ?? "无法读取备份文件");
      return;
    }

    restoreFile.value = file;
    inspectResult.value = data;
    restoreSections.value = [...data.sections];
    restoreVisible.value = true;
  } finally {
    inspecting.value = false;
  }
}

async function handleRestore() {
  const file = restoreFile.value;
  if (!file || restoring.value || restoreSections.value.length === 0) return;

  restoring.value = true;
  try {
    const { data, error } = await restoreBackup(file, restoreSections.value);
    if (error) {
      message.error(error);
      return;
    }

    const result = data as BackupRestoreResult;
    restoreVisible.value = false;
    message.success(`恢复完成：${describeResult(result)}`);

    // 数据整体替换，刷新页面让配置与首页卡片重新加载
    setTimeout(() => window.location.reload(), 800);
  } finally {
    restoring.value = false;
  }
}

function describeResult(result: BackupRestoreResult): string {
  const parts: string[] = [];
  if (restoreSections.value.includes("settings")) parts.push("站点设置");
  if (restoreSections.value.includes("cards")) parts.push(`${result.cards} 张卡片`);
  if (restoreSections.value.includes("images")) parts.push(`${result.images} 个图库项`);
  if (restoreSections.value.includes("live2d")) parts.push(`${result.live2DConfigs} 个模型配置`);
  if (result.files > 0) parts.push(`${result.files} 个文件`);
  return parts.join("、");
}
</script>

<template>
  <h2 class="settings__heading">备份与恢复</h2>

  <n-card size="small">
    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">可选备份</div>
        <div class="settings__row-desc">请在下方选择需要备份的内容</div>
      </div>
    </div>

    <div class="settings__row" v-for="item in SECTION_OPTIONS" :key="item.key">
      <div class="settings__row-main">
        <div class="settings__row-label">{{ item.label }}</div>
        <div class="settings__row-desc">{{ item.desc }}</div>
      </div>
      <div class="settings__row-control">
        <n-checkbox
          size="large"
          :checked="exportSections.includes(item.key)"
          @update:checked="(checked: boolean) => toggleSection(exportSections, item.key, checked)"
        />
      </div>
    </div>

    <n-text v-if="exportImageWarning" class="backup__hint" type="warning">
      未勾选图库时，卡片图标与站点背景图可能不会随之迁移
    </n-text>

    <n-divider class="settings__divider" />

    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">导出备份</div>
        <div class="settings__row-desc">将卡片、站点配置、图库等打包为 ZIP，便于迁移</div>
      </div>
      <div class="settings__row-control">
        <n-button
          :disabled="exportSections.length === 0"
          :loading="exporting"
          @click="handleExport"
        >
          导出
        </n-button>
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">恢复备份</div>
        <div class="settings__row-desc">从备份文件中恢复配置</div>
      </div>
      <div class="settings__row-control">
        <n-button :loading="inspecting" secondary type="warning" @click="handlePickFile">
          恢复
        </n-button>
      </div>
    </div>

    <input
      ref="fileInputRef"
      accept=".zip,application/zip"
      hidden
      type="file"
      @change="handleFileChange"
    />
  </n-card>

  <n-modal
    v-model:show="restoreVisible"
    :bordered="false"
    :closable="!restoring"
    :mask-closable="!restoring"
    preset="card"
    style="width: 520px; max-width: calc(100vw - 32px)"
    title="选择恢复内容"
  >
    <div class="backup__restore">
      <div>
        <div class="backup__section-label">{{ restoreFile?.name }}</div>
        <div class="backup__section-desc">
          导出时间：{{ formatExportedAt(inspectResult?.exportedAt) }}
        </div>
      </div>

      <n-flex vertical size="large">
        <n-checkbox
          v-for="item in SECTION_OPTIONS"
          :key="item.key"
          :checked="isSelected(item.key)"
          :disabled="!isAvailable(item.key)"
          size="large"
          @update:checked="(checked: boolean) => toggleSection(restoreSections, item.key, checked)"
        >
          <span class="backup__section-label">{{ item.label }}</span>
          <span class="backup__section-desc">{{ sectionSummary(item.key) }}</span>
        </n-checkbox>
      </n-flex>

      <n-alert v-if="imageWarning" :bordered="false" type="warning">
        未勾选图库时，卡片图标与站点背景图可能指向备份外的图片
      </n-alert>

      <div class="backup__section-desc">
        未勾选的内容保持现状，恢复会覆盖所选内容且无法撤销。恢复完成后页面会自动刷新。
      </div>
    </div>

    <template #footer>
      <div class="backup__footer">
        <n-button :disabled="restoring" @click="restoreVisible = false">取消</n-button>
        <n-button
          :disabled="restoreSections.length === 0"
          :loading="restoring"
          type="warning"
          @click="handleRestore"
        >
          恢复所选内容
        </n-button>
      </div>
    </template>
  </n-modal>

  <n-alert :bordered="false" class="backup__alert" type="info">
    备份不包含账号密码与 HTTPS 证书
  </n-alert>
</template>

<style scoped>
.backup__alert {
  margin-top: 16px;
}

.backup__section-label {
  margin-right: 8px;
}

.backup__section-desc {
  font-size: 0.8rem;
  opacity: 0.6;
}

.backup__hint {
  font-size: 0.8rem;
}

.backup__restore {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.backup__footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

@media (max-width: 640px) {
  .backup__sections {
    grid-template-columns: 1fr;
  }
}
</style>
