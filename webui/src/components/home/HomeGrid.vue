<script lang="ts" setup>
import type { CSSProperties } from "vue";
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { GridItem, GridLayout } from "grid-layout-plus";
import { COL_WIDTH, GRID_GAP_Y, MOBILE_GRID_COLS, ROW_HEIGHT } from "@/config/default.ts";
import { getCardPlugin } from "@/cards/registry.ts";
import type { PlatformCardRequired } from "@/cards/types.ts";
import { desktopGridContainerMinWidth, gridMargin } from "@/utils/cardLayoutCalculate.ts";
import { settings } from "@/stores/settings.ts";
import { platform } from "@/config/platform.ts";
import { useServiceProbe } from "@/composables/useServiceProbe.ts";
import ServiceProbeProvider from "@/components/ServiceProbeProvider.vue";

const props = defineProps<{
  cards: PlatformCardRequired[];
  editMode: boolean;
  simulateMobile: boolean;
}>();

const emit = defineEmits<{
  moved: [i: string, newX: number, newY: number];
  resized: [i: string, newH: number, newW: number];
  contextmenu: [event: MouseEvent, item: PlatformCardRequired];
  duplicate: [card: PlatformCardRequired, x: number, y: number];
}>();

defineSlots<{
  default: (props: { card: PlatformCardRequired }) => unknown;
}>();

/** 首页打开后自动探测所有 ServiceCard 的目标地址 */
const { entries: probeEntries } = useServiceProbe(() => props.cards);

/** grid-layout 会就地修改 item，必须是可写引用 */
const layout = ref<PlatformCardRequired[]>(props.cards);
watch(
  () => props.cards,
  (cards) => {
    layout.value = cards;
  },
);

/** grid-layout 宽度 & 动态水平 margin：(宽度 - 列宽 * 列数) / (列数 + 1)，最小 20 */
const gridRef = ref<InstanceType<typeof GridLayout> | null>(null);
const containerWidth = ref(0);
let resizeObserver: ResizeObserver | null = null;

const isMobile = computed(() => platform.value === "mobile");

const colNum = computed(() => (isMobile.value ? MOBILE_GRID_COLS : settings.desktopCols));

const dynamicMargin = computed(() => gridMargin(containerWidth.value, colNum.value));

/** 与库内部 Re() 口径一致，用于把像素位移换算成栅格位移 */
const colWidth = computed(() => {
  const cols = colNum.value;
  if (cols <= 0) return 0;
  return (containerWidth.value - dynamicMargin.value * (cols + 1)) / cols;
});

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

  const card = layout.value.find((it) => it.i === itemEl.dataset.cardId);
  if (!card) return;

  // 阻止库的拖拽与文本选择；实际是否启动还取决于 ctrlHeld 已让 isDraggable 关闭
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
  const maxX = Math.max(0, colNum.value - d.card.w);
  const targetX = Math.max(0, Math.min(maxX, d.card.x + Math.round(deltaX / colPitch)));
  const targetY = Math.max(0, d.card.y + Math.round(deltaY / rowPitch));
  const pos = findFreeSlot(layout.value, targetX, targetY, d.card.w, d.card.h, maxX);

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
  cards: PlatformCardRequired[],
  targetX: number,
  targetY: number,
  w: number,
  h: number,
  maxX: number,
) {
  const overlaps = (x: number, y: number) =>
    cards.some((c) => x < c.x + c.w && x + w > c.x && y < c.y + c.h && y + h > c.y);

  const bottom = cards.reduce((max, c) => Math.max(max, c.y + c.h), targetY);
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

/** 栅格线：列间距 = 列宽 + 水平 margin，行间距 = 行高 + 纵向 margin */
const gridGuides = computed(() => ({
  "--vgl-guide-margin-x": `${dynamicMargin.value}px`,
  "--vgl-guide-margin-y": `${GRID_GAP_Y}px`,
  "--vgl-guide-col-pitch": `${COL_WIDTH + dynamicMargin.value}px`,
  "--vgl-guide-row-pitch": `${ROW_HEIGHT + GRID_GAP_Y}px`,
}));

const gridStyle = computed(() => [
  !isMobile.value
    ? {
        width: "100%",
        "min-width": `${desktopGridContainerMinWidth(settings.desktopCols)}px`,
        "max-width": settings.desktopGridFullWidth
          ? "100%"
          : `${settings.desktopGridContainerMaxWidth}px`,
      }
    : {
        width: "100%",
      },
  gridGuides.value,
]);

const simulateMobileStyle = computed(() => [
  {
    width: "480px",
  },
  gridGuides.value,
]);

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
  // grid-layout 的根节点是内部的 div.vgl-layout；offsetWidth 与库计算列宽的口径一致
  const el = gridRef.value?.$el as HTMLElement | undefined;
  if (el) {
    containerWidth.value = el.offsetWidth;

    // 捕获阶段接管 Ctrl 拖拽，先于库内部的 pointerdown 监听
    el.addEventListener("pointerdown", handleDuplicatePointerDown, true);

    resizeObserver = new ResizeObserver(() => {
      containerWidth.value = el.offsetWidth;
    });
    resizeObserver.observe(el);
  }
});

onBeforeUnmount(() => {
  resizeObserver?.disconnect();
  resizeObserver = null;

  const el = gridRef.value?.$el as HTMLElement | undefined;
  el?.removeEventListener("pointerdown", handleDuplicatePointerDown, true);

  window.removeEventListener("keydown", handleCtrlKeyDown);
  window.removeEventListener("keyup", handleCtrlKeyUp);
  window.removeEventListener("blur", handleWindowBlur);
  endDuplicate();
});
</script>

<template>
  <grid-layout
    ref="gridRef"
    v-model:layout="layout"
    :class="{ 'grid-guides': editMode }"
    :col-num="colNum"
    :is-draggable="editMode"
    :is-resizable="editMode"
    :margin="[dynamicMargin, GRID_GAP_Y]"
    :prevent-collision="true"
    :row-height="ROW_HEIGHT"
    :style="simulateMobile ? simulateMobileStyle : gridStyle"
    :use-css-transforms="true"
    :vertical-compact="false"
  >
    <grid-item
      v-for="card in layout"
      :key="card.i"
      :class="{ 'card-item__editing': editMode }"
      :data-card-id="card.i"
      :h="card.h"
      :i="card.i"
      :is-draggable="editMode && !ctrlHeld"
      :minH="card.minH"
      :minW="card.minW"
      :w="card.w"
      :x="card.x"
      :y="card.y"
      @moved="(i, newX, newY) => emit('moved', i, newX, newY)"
      @resized="(i, newH, newW) => emit('resized', i, newH, newW)"
      @contextmenu.prevent.stop="(event: MouseEvent) => emit('contextmenu', event, card)"
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
      <component :is="getCardPlugin(duplicate.card.type)?.display" v-bind="duplicate.card.config" />
    </div>
  </grid-layout>
</template>

<style scoped>
.card-item__editing {
  border: 1px dashed;
  border-radius: var(--border-radius);
  background-color: rgba(127, 127, 127, 0.2);
  /* 编辑态下由自己处理触摸拖拽，避免浏览器接管为滚动 / 下拉刷新 */
  touch-action: none;
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
