<script lang="ts" setup>
import type { CSSProperties } from "vue";
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { useThemeVars } from "naive-ui";
import {
  GridItem,
  GridLayout,
  correctBounds,
  noCompactor,
  normalizeLayout,
  type GridTransferResult,
  type InteractionStartPayload,
  type Layout,
  type LayoutUpdateMeta,
  type ReadonlyLayout,
  type ResizeConfig,
  type TransferConfig,
} from "grid-layout-plus";
import {
  COL_WIDTH,
  GRID_GAP_Y,
  HOME_GRID_MAX_ROWS,
  MOBILE_GRID_COLS,
  ROW_HEIGHT,
} from "@/config/default.ts";
import { getCardPlugin } from "@/cards/registry.ts";
import type { PlatformCardRequired } from "@/cards/types.ts";
import { gridMargin } from "@/utils/cardLayoutCalculate.ts";
import { settings } from "@/stores/settings.ts";
import { platform } from "@/config/platform.ts";
import { useServiceProbe } from "@/composables/useServiceProbe.ts";
import ServiceProbeProvider from "@/components/ServiceProbeProvider.vue";

const props = defineProps<{
  cards: PlatformCardRequired[];
  editMode: boolean;
  selectedId: string | null;
  transferGroup: string;
}>();

const resizeConfig: ResizeConfig = {
  handles: ["n", "e", "s", "w", "ne", "nw", "se", "sw"],
};

const themeVars = useThemeVars();

const emit = defineEmits<{
  moved: [i: string, newX: number, newY: number];
  resized: [i: string, newH: number, newW: number];
  contextmenu: [event: MouseEvent, item: PlatformCardRequired];
  duplicate: [card: PlatformCardRequired, x: number, y: number];
  select: [i: string];
  transfer: [result: GridTransferResult];
}>();

defineSlots<{
  default: (props: { card: PlatformCardRequired }) => unknown;
}>();

/** 首页打开后自动探测所有 ServiceCard 的目标地址 */
const { entries: probeEntries } = useServiceProbe(() => props.cards);

const isMobile = computed(() => platform.value === "mobile");

const colNum = computed(() => (isMobile.value ? MOBILE_GRID_COLS : settings.desktopCols));

// grid-layout-plus 初始布局校验会拒绝越界/重叠，旧数据或列数调整后必须先归位，否则首页渲染中断
function buildLayout(cards: readonly PlatformCardRequired[], cols: number): Layout {
  const items: Layout = cards.map((card) => ({
    i: card.i,
    x: card.x,
    y: card.y,
    w: Math.min(card.w, cols),
    h: card.h,
    minW: Math.min(card.minW ?? 1, cols),
    minH: card.minH,
  }));

  const bounded = correctBounds(items, { cols });

  return normalizeLayout(bounded, { cols, collisionMode: "push", compactor: noCompactor });
}

const layout = ref<Layout>(buildLayout(props.cards, colNum.value));

const cardsById = computed(
  () => new Map(props.cards.map((card) => [String(card.i), card] as const)),
);

/** 跨组转移先提交目标布局，卡片数据要等上层响应 transfer 后才补上 */
const gridItems = computed(() =>
  layout.value.flatMap((item) => {
    const card = cardsById.value.get(String(item.i));
    return card ? [{ item, card }] : [];
  }),
);

watch([() => props.cards, colNum], async ([cards]) => {
  const cols = colNum.value;
  // 等库先应用新的列数，否则新平台的布局会按旧 cols 校验被拒
  await nextTick();
  layout.value = buildLayout(cards, cols);
});

const gridHost = ref<HTMLElement | null>(null);
const containerWidth = ref(0);
let resizeObserver: ResizeObserver | null = null;

/** (宽度 - 列宽 * 列数) / (列数 + 1)，最小 20；同时作为 gap 与 containerPadding，栅格线才会和列宽对齐 */
const dynamicMargin = computed(() => gridMargin(containerWidth.value, colNum.value));

/** 与库内部列宽口径一致，用于把像素位移换算成栅格位移 */
const colWidth = computed(() => {
  const cols = colNum.value;
  if (cols <= 0) return 0;
  return (containerWidth.value - dynamicMargin.value * (cols + 1)) / cols;
});

const transferConfig = computed<TransferConfig | undefined>(() =>
  props.editMode ? { group: props.transferGroup } : undefined,
);

/** 按住 Ctrl 时禁用库自带拖拽，交给副本拖拽接管 */
const ctrlHeld = ref(false);

type DuplicateState = {
  card: PlatformCardRequired;
  pointerId: number;
  startClientX: number;
  startClientY: number;
  /** 副本初始位置，视口坐标 */
  originLeft: number;
  originTop: number;
  left: number;
  top: number;
  width: number;
  height: number;
  /** 源卡片在栅格中的位置与尺寸 */
  sourceX: number;
  sourceY: number;
  sourceW: number;
  sourceH: number;
};

const duplicate = ref<DuplicateState | null>(null);

const ghostStyle = computed<CSSProperties>(() => {
  const d = duplicate.value;
  if (!d) return {};
  return {
    left: `${d.left}px`,
    top: `${d.top}px`,
    width: `${d.width}px`,
    height: `${d.height}px`,
  };
});

/** 位移小于此值视为误触，不复制 */
const DUPLICATE_MIN_DISTANCE = 4;

function handleCtrlKeyDown(event: KeyboardEvent) {
  if (event.key === "Control") {
    ctrlHeld.value = true;
  } else if (event.key === "Escape") {
    endDuplicate();
  }
}

function handleCtrlKeyUp(event: KeyboardEvent) {
  if (event.key === "Control") ctrlHeld.value = false;
}

/** 窗口失焦时按键状态不可信，一并结束拖拽 */
function handleWindowBlur() {
  ctrlHeld.value = false;
  endDuplicate();
}

function handleDuplicatePointerDown(event: PointerEvent) {
  if (!props.editMode || duplicate.value) return;
  if (!event.ctrlKey || event.button !== 0) return;

  const itemEl = (event.target as HTMLElement | null)?.closest<HTMLElement>("[data-card-id]");
  if (!itemEl) return;

  const id = itemEl.dataset.cardId ?? "";
  const card = cardsById.value.get(id);
  const item = layout.value.find((entry) => String(entry.i) === id);
  if (!card || !item) return;

  // 阻止库的拖拽与文本选择；Ctrl 按住时栅格级 isDraggable 已关闭
  event.preventDefault();
  event.stopPropagation();

  const rect = itemEl.getBoundingClientRect();
  duplicate.value = {
    card,
    pointerId: event.pointerId,
    startClientX: event.clientX,
    startClientY: event.clientY,
    originLeft: rect.left,
    originTop: rect.top,
    left: rect.left,
    top: rect.top,
    width: rect.width,
    height: rect.height,
    sourceX: item.x,
    sourceY: item.y,
    sourceW: item.w,
    sourceH: item.h,
  };

  window.addEventListener("pointermove", handleDuplicatePointerMove, true);
  window.addEventListener("pointerup", handleDuplicatePointerUp, true);
  window.addEventListener("pointercancel", endDuplicate, true);
}

function handleDuplicatePointerMove(event: PointerEvent) {
  const d = duplicate.value;
  if (!d || event.pointerId !== d.pointerId) return;
  d.left = d.originLeft + (event.clientX - d.startClientX);
  d.top = d.originTop + (event.clientY - d.startClientY);
}

function handleDuplicatePointerUp(event: PointerEvent) {
  const d = duplicate.value;
  if (!d || event.pointerId !== d.pointerId) return;

  const deltaX = d.left - d.originLeft;
  const deltaY = d.top - d.originTop;
  endDuplicate();

  if (Math.abs(deltaX) < DUPLICATE_MIN_DISTANCE && Math.abs(deltaY) < DUPLICATE_MIN_DISTANCE) {
    return;
  }

  const colPitch = colWidth.value + dynamicMargin.value;
  const rowPitch = ROW_HEIGHT + GRID_GAP_Y;
  const maxX = Math.max(0, colNum.value - d.sourceW);
  const targetX = Math.max(0, Math.min(maxX, d.sourceX + Math.round(deltaX / colPitch)));
  const targetY = Math.max(0, d.sourceY + Math.round(deltaY / rowPitch));
  const pos = findFreeSlot(layout.value, targetX, targetY, d.sourceW, d.sourceH, maxX);

  emit("duplicate", d.card, pos.x, pos.y);
}

function endDuplicate() {
  duplicate.value = null;
  window.removeEventListener("pointermove", handleDuplicatePointerMove, true);
  window.removeEventListener("pointerup", handleDuplicatePointerUp, true);
  window.removeEventListener("pointercancel", endDuplicate, true);
}

/**
 * 从目标位置向下找最近的空位；同一行内优先取离目标最近的一列
 */
function findFreeSlot(
  items: Layout,
  targetX: number,
  targetY: number,
  w: number,
  h: number,
  maxX: number,
) {
  const overlaps = (x: number, y: number) =>
    items.some((c) => x < c.x + c.w && x + w > c.x && y < c.y + c.h && y + h > c.y);

  const bottom = items.reduce((max, c) => Math.max(max, c.y + c.h), targetY);
  const xs = Array.from({ length: maxX + 1 }, (_, x) => x).sort(
    (a, b) => Math.abs(a - targetX) - Math.abs(b - targetX),
  );

  for (let y = targetY; y <= bottom; y++) {
    const free = xs.find((x) => !overlaps(x, y));
    if (free !== undefined) return { x: free, y };
  }

  // 全部被占满时放到最下方
  return { x: Math.min(targetX, maxX), y: bottom };
}

function handleLayoutUpdated(next: ReadonlyLayout, meta: LayoutUpdateMeta) {
  if (meta.source !== "interaction") return;

  for (const item of next) {
    const card = cardsById.value.get(String(item.i));
    if (!card) continue;
    if (card.x !== item.x || card.y !== item.y) {
      emit("moved", String(item.i), item.x, item.y);
    }
    if (card.w !== item.w || card.h !== item.h) {
      emit("resized", String(item.i), item.h, item.w);
    }
  }
}

function handleContextMenu(event: MouseEvent, card: PlatformCardRequired) {
  emit("contextmenu", event, card);
}

function handleSelect(card: PlatformCardRequired) {
  if (!props.editMode) return;
  emit("select", String(card.i));
}

function handleTransfer(result: GridTransferResult) {
  if (!layout.value.some((item) => item.i === result.item.i)) return;
  emit("transfer", result);
}

function handleGridError(error: unknown) {
  console.error("[home-grid]", error);
}

/** 栅格线：列间距 = 列宽 + 水平 margin，行间距 = 行高 + 纵向 margin */
const gridGuides = computed(() => ({
  "--home-grid-selected-color": themeVars.value.primaryColor,
  "--vgl-guide-margin-x": `${dynamicMargin.value}px`,
  "--vgl-guide-margin-y": `${GRID_GAP_Y}px`,
  "--vgl-guide-col-pitch": `${COL_WIDTH + dynamicMargin.value}px`,
  "--vgl-guide-row-pitch": `${ROW_HEIGHT + GRID_GAP_Y}px`,
}));

// 编辑态空分组保留一行高度，跨组拖拽才有可命中的落点
const emptyGridStyle = computed<CSSProperties>(() =>
  props.editMode && props.cards.length === 0
    ? { minHeight: `${ROW_HEIGHT + GRID_GAP_Y * 2}px` }
    : {},
);

// 向下拖动时候选位置会把所在栅格撑高，后面的分组被推远；交互期间锁住起始高度
const dragHeight = ref<number | null>(null);

const gridFrameStyle = computed<CSSProperties>(() => ({
  ...emptyGridStyle.value,
  ...(dragHeight.value === null ? {} : { maxHeight: `${dragHeight.value}px` }),
}));

function handleInteractionStart(payload: InteractionStartPayload) {
  if (payload.type !== "drag") return;
  const grid = gridHost.value?.querySelector<HTMLElement>(".vgl-layout");
  dragHeight.value = grid ? grid.getBoundingClientRect().height : null;
}

function handleInteractionEnd() {
  dragHeight.value = null;
}

/** 编辑模式才跟踪 Ctrl 与 Esc，退出时结束可能残留的拖拽 */
watch(
  () => props.editMode,
  (editing) => {
    ctrlHeld.value = false;
    endDuplicate();

    if (editing) {
      window.addEventListener("keydown", handleCtrlKeyDown);
      window.addEventListener("keyup", handleCtrlKeyUp);
      window.addEventListener("blur", handleWindowBlur);
    } else {
      window.removeEventListener("keydown", handleCtrlKeyDown);
      window.removeEventListener("keyup", handleCtrlKeyUp);
      window.removeEventListener("blur", handleWindowBlur);
    }
  },
  { immediate: true },
);

onMounted(() => {
  const host = gridHost.value;
  if (!host) return;

  containerWidth.value = host.offsetWidth;

  // 捕获阶段接管 Ctrl 拖拽，先于库内部的 pointerdown 监听
  host.addEventListener("pointerdown", handleDuplicatePointerDown, true);

  resizeObserver = new ResizeObserver(() => {
    containerWidth.value = host.offsetWidth;
  });
  resizeObserver.observe(host);
});

onBeforeUnmount(() => {
  resizeObserver?.disconnect();
  resizeObserver = null;

  gridHost.value?.removeEventListener("pointerdown", handleDuplicatePointerDown, true);

  window.removeEventListener("keydown", handleCtrlKeyDown);
  window.removeEventListener("keyup", handleCtrlKeyUp);
  window.removeEventListener("blur", handleWindowBlur);
  endDuplicate();
});
</script>

<template>
  <div
    ref="gridHost"
    :class="{ 'grid-guides': editMode }"
    :style="gridGuides"
    class="home-grid-host"
  >
    <grid-layout
      v-model:layout="layout"
      class="home-grid"
      :col-num="colNum"
      :compactor="noCompactor"
      :container-padding="[dynamicMargin, GRID_GAP_Y]"
      :gap="[dynamicMargin, GRID_GAP_Y]"
      :is-draggable="editMode && !ctrlHeld"
      :is-resizable="editMode"
      :max-rows="HOME_GRID_MAX_ROWS"
      :resize-config="resizeConfig"
      :row-height="ROW_HEIGHT"
      :style="gridFrameStyle"
      :transfer-config="transferConfig"
      collision-mode="push"
      @error="handleGridError"
      @interaction-end="handleInteractionEnd"
      @interaction-start="handleInteractionStart"
      @layout-updated="handleLayoutUpdated"
      @transfer="handleTransfer"
    >
      <grid-item
        v-for="{ item, card } in gridItems"
        :key="item.i"
        :class="{
          'card-item__editing': editMode,
          'card-item__selected': editMode && String(item.i) === selectedId,
        }"
        :data-card-id="item.i"
        :i="item.i"
        @click.stop="handleSelect(card)"
        @contextmenu.prevent.stop="(event: MouseEvent) => handleContextMenu(event, card)"
      >
        <div v-if="editMode && (!card.isVisible || !card.mobileVisible)" class="card-item__tags">
          <n-tag v-if="!card.isVisible" size="tiny" type="primary"> 仅登录可见 </n-tag>
          <n-tag v-if="!card.mobileVisible" size="tiny" type="warning"> 移动端隐藏 </n-tag>
        </div>
        <!-- 探测结果按格子注入，由卡片自行展示 -->
        <ServiceProbeProvider :entry="probeEntries.get(String(card.i))">
          <div :class="{ 'card-item__content--locked': editMode }" class="card-item__content">
            <slot :card="card" />
          </div>
        </ServiceProbeProvider>
      </grid-item>

      <!-- Ctrl 拖拽复制时的副本预览：固定在视口坐标上跟随指针，不参与栅格布局 -->
      <div v-if="duplicate" :style="ghostStyle" class="duplicate-ghost">
        <component
          :is="getCardPlugin(duplicate.card.type)?.display"
          v-bind="duplicate.card.config"
        />
      </div>
    </grid-layout>
  </div>
</template>

<style scoped>
.home-grid-host {
  position: relative;
  width: 100%;
}

.home-grid {
  width: 100%;
}

.card-item__editing {
  border: 1px dashed;
  border-radius: var(--border-radius);
  background-color: rgba(127, 127, 127, 0.2);
  /* 编辑态下由自己处理触摸拖拽，避免浏览器接管为滚动 / 下拉刷新 */
  touch-action: none;
}

.card-item__selected {
  border-color: var(--home-grid-selected-color);
}

.card-item__content {
  width: 100%;
  height: 100%;
}

.card-item__tags {
  position: absolute;
  top: 2px;
  left: 50%;
  z-index: 10;
  display: flex;
  gap: 4px;
  transform: translateX(-50%);
}

/* 编辑态下内容不可交互，按下事件直接落到 grid-item 上，拖拽不受影响 */
.card-item__content--locked {
  pointer-events: none;
}

/* 副本不接收指针事件 */
.duplicate-ghost {
  position: fixed;
  z-index: 10;
  overflow: hidden;
  border-radius: var(--border-radius);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.35);
  opacity: 0.75;
  pointer-events: none;
}

/* 栅格线（仅编辑模式）：每个格子画左右/上下两条边，使 item 正好填满格子（间距留在格子之间） */
/* --vgl-guide-* 由组件的 :style 在运行时注入，IDE 静态分析看不到声明，故忽略未解析告警 */
/* noinspection CssUnresolvedCustomProperty */
.grid-guides::before {
  --guide-line: rgba(127, 127, 127, 0.4);
  position: absolute;
  top: var(--vgl-guide-margin-y);
  /* 右/下各多出 1px，让最后一格右边、下边的线不被裁掉 */
  right: calc(var(--vgl-guide-margin-x) - 1px);
  bottom: calc(var(--vgl-guide-margin-y) - 1px);
  left: var(--vgl-guide-margin-x);
  content: "";
  pointer-events: none;
  background-image:
    linear-gradient(to right, var(--guide-line) 1px, transparent 1px),
    linear-gradient(to right, var(--guide-line) 1px, transparent 1px),
    linear-gradient(to bottom, var(--guide-line) 1px, transparent 1px),
    linear-gradient(to bottom, var(--guide-line) 1px, transparent 1px);
  background-size: var(--vgl-guide-col-pitch) var(--vgl-guide-row-pitch);
  background-position:
    0 0,
    calc(var(--vgl-guide-col-pitch) - var(--vgl-guide-margin-x)) 0,
    0 0,
    0 calc(var(--vgl-guide-row-pitch) - var(--vgl-guide-margin-y));
}
</style>
