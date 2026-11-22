<script lang="ts" setup>
import { computed, ref, watch } from "vue";
import type { GridTransferResult } from "grid-layout-plus";
import ArrowDown24Regular from "@vicons/fluent/es/ArrowDown24Regular.js";
import ArrowUp24Regular from "@vicons/fluent/es/ArrowUp24Regular.js";
import ChevronDown24Regular from "@vicons/fluent/es/ChevronDown24Regular.js";
import ChevronUp24Regular from "@vicons/fluent/es/ChevronUp24Regular.js";
import Delete24Regular from "@vicons/fluent/es/Delete24Regular.js";
import type { CardGroup, PlatformCardRequired } from "@/cards/types.ts";
import { CARD_GROUP_NAME_MAX_LENGTH } from "@/config/default.ts";
import { platform } from "@/config/platform.ts";
import { settings } from "@/stores/settings.ts";
import { desktopGridContainerMinWidth } from "@/utils/cardLayoutCalculate.ts";
import HomeGrid from "@/components/home/HomeGrid.vue";

const props = defineProps<{
  group: CardGroup;
  cards: PlatformCardRequired[];
  editMode: boolean;
  selectedId: string | null;
  simulateMobile: boolean;
  transferGroup: string;
  first: boolean;
  last: boolean;
  canDelete: boolean;
}>();

const emit = defineEmits<{
  rename: [group: CardGroup, name: string];
  toggleCollapse: [group: CardGroup];
  move: [group: CardGroup, direction: -1 | 1];
  remove: [group: CardGroup];
  contextmenu: [event: MouseEvent, item: PlatformCardRequired];
  duplicate: [card: PlatformCardRequired, x: number, y: number];
  moved: [i: string, newX: number, newY: number];
  resized: [i: string, newH: number, newW: number];
  select: [i: string];
  transfer: [result: GridTransferResult];
}>();

defineSlots<{
  default: (props: { card: PlatformCardRequired }) => unknown;
}>();

// 标题栏与栅格同宽，宽度约束统一落在分组根元素上
const sectionStyle = computed(() => {
  if (props.simulateMobile) return { width: "480px" };
  if (platform.value === "mobile") return { width: "100%" };
  return {
    width: "100%",
    minWidth: `${desktopGridContainerMinWidth(settings.desktopCols)}px`,
    maxWidth: settings.desktopGridFullWidth ? "100%" : `${settings.desktopGridContainerMaxWidth}px`,
  };
});

const headerVisible = computed(() => props.group.name.trim().length > 0 || props.editMode);

// 名称为空的浏览态没有标题栏，折叠会让卡片既看不到也无法展开
const gridVisible = computed(() => !props.group.collapsed || !headerVisible.value);

const nameDraft = ref(props.group.name);
watch(
  () => props.group.name,
  (name) => {
    nameDraft.value = name;
  },
);

function commitName() {
  const next = nameDraft.value.trim();
  nameDraft.value = next;
  if (next === props.group.name) return;
  emit("rename", props.group, next);
}

function resetName() {
  nameDraft.value = props.group.name;
}
</script>

<template>
  <section :data-group-id="group.id" :style="sectionStyle" class="home-group">
    <header v-if="headerVisible" class="home-group__header">
      <n-button quaternary size="small" @click="emit('toggleCollapse', group)">
        <template #icon>
          <n-icon>
            <ChevronDown24Regular v-if="group.collapsed" />
            <ChevronUp24Regular v-else />
          </n-icon>
        </template>
      </n-button>

      <template v-if="editMode">
        <n-input
          v-model:value="nameDraft"
          :maxlength="CARD_GROUP_NAME_MAX_LENGTH"
          class="home-group__name-input"
          clearable
          placeholder="未命名分组"
          size="small"
          @blur="commitName"
          @keyup.enter="commitName"
          @keyup.esc="resetName"
        />
        <n-button
          :disabled="first"
          quaternary
          size="small"
          title="上移分组"
          @click="emit('move', group, -1)"
        >
          <template #icon>
            <n-icon><ArrowUp24Regular /></n-icon>
          </template>
        </n-button>
        <n-button
          :disabled="last"
          quaternary
          size="small"
          title="下移分组"
          @click="emit('move', group, 1)"
        >
          <template #icon>
            <n-icon><ArrowDown24Regular /></n-icon>
          </template>
        </n-button>
        <n-popconfirm v-if="canDelete" @positive-click="emit('remove', group)">
          <template #trigger>
            <n-button quaternary size="small" title="删除分组">
              <template #icon>
                <n-icon><Delete24Regular /></n-icon>
              </template>
            </n-button>
          </template>
          删除分组「{{ group.name || "未命名分组" }}」及组内所有卡片？该操作不可撤销
        </n-popconfirm>
      </template>
      <span v-else class="home-group__name">{{ group.name }}</span>
    </header>

    <div v-if="gridVisible" class="home-group__grid">
      <HomeGrid
        :cards="cards"
        :edit-mode="editMode"
        :selected-id="selectedId"
        :transfer-group="transferGroup"
        @contextmenu="(event, item) => emit('contextmenu', event, item)"
        @duplicate="(card, x, y) => emit('duplicate', card, x, y)"
        @moved="(i, newX, newY) => emit('moved', i, newX, newY)"
        @resized="(i, newH, newW) => emit('resized', i, newH, newW)"
        @select="(i) => emit('select', i)"
        @transfer="(result) => emit('transfer', result)"
      >
        <template #default="{ card }">
          <slot :card="card" />
        </template>
      </HomeGrid>
    </div>
  </section>
</template>

<style scoped>
.home-group {
  display: flex;
  flex-direction: column;
  gap: 12px;
  width: 100%;
  margin: 0 auto;
}

.home-group__header {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  margin-left: 16px;
}

.home-group__name-input {
  width: 260px;
}

.home-group__name {
  font-size: 16px;
  font-weight: 600;
}

.home-group__grid {
  display: flex;
  justify-content: center;
  width: 100%;
}
</style>
