<script lang="ts" setup>
import { ref } from "vue";
import Checkmark24Regular from "@vicons/fluent/es/Checkmark24Regular.js";
import SlideSize24Regular from "@vicons/fluent/es/SlideSize24Regular.js";
import QuestionCircle24Regular from "@vicons/fluent/es/QuestionCircle24Regular.js";
import { appTheme } from "@/stores/theme.ts";
import type {
  Live2dManifestWardrobeGroup,
  Live2dManifestWardrobeOption,
} from "@/services/live2dManifest.ts";
import type {
  Live2dExpressionItem,
  Live2dMotionItem,
  Live2dMotionSection,
} from "@/services/live2dRuntime.ts";

defineProps<{
  visible: boolean;
  x: number;
  y: number;
  expressions: Live2dExpressionItem[];
  motionSections: Live2dMotionSection[];
  wardrobes: Live2dManifestWardrobeGroup[];
  edgeSnap: boolean;
  resizeMode: boolean;
  selectedExpression: string;
  /** 配置优先，回退清单默认值 */
  isOutfitSelected: (
    group: Live2dManifestWardrobeGroup,
    option: Live2dManifestWardrobeOption,
  ) => boolean;
}>();

const emit = defineEmits<{
  selectExpression: [runtimeName: string];
  selectMotion: [motion: Live2dMotionItem];
  selectOutfit: [group: Live2dManifestWardrobeGroup, option: Live2dManifestWardrobeOption];
  toggleEdgeSnap: [];
  toggleResize: [];
}>();

/** 暴露给父组件，用于判断事件是否在菜单内部 */
const menuRef = ref<HTMLElement | null>(null);
defineExpose({ menuElement: menuRef });
</script>

<template>
  <Teleport to="body">
    <div
      v-if="visible"
      ref="menuRef"
      :class="{ 'live2d-menu--dark': appTheme.name === 'dark' }"
      :style="{ left: `${x}px`, top: `${y}px` }"
      class="live2d-menu"
      @pointerdown.stop
      @contextmenu.prevent.stop
    >
      <!-- 用 n-scrollbar 保持站点滚动容器外观一致 -->
      <n-scrollbar style="max-height: 60vh">
        <button class="live2d-menu__item" type="button" @click="emit('toggleEdgeSnap')">
          <Checkmark24Regular
            :class="{ 'live2d-menu__icon--hidden': !edgeSnap }"
            class="live2d-menu__icon"
          />
          <span class="live2d-menu__label">边缘吸附</span>
          <n-tooltip>
            <template #trigger>
              <n-icon>
                <QuestionCircle24Regular />
              </n-icon>
            </template>
            启用后，Live2D舞台边缘无法超出屏幕边界，建议仅在Live2D舞台四周本身就存在较宽的空白像素时才禁用边缘吸附。
          </n-tooltip>
        </button>

        <button class="live2d-menu__item" type="button" @click="emit('toggleResize')">
          <SlideSize24Regular class="live2d-menu__icon" />
          <span class="live2d-menu__label">
            {{ resizeMode ? "退出尺寸调整" : "调整尺寸" }}
          </span>
        </button>

        <div class="live2d-menu__divider"></div>

        <template v-for="section in motionSections" :key="section.key">
          <div class="live2d-menu__group">{{ section.group || "动作" }}</div>
          <button
            v-for="motion in section.items"
            :key="`${motion.group}:${motion.index}:${motion.file}`"
            class="live2d-menu__item"
            type="button"
            @click="emit('selectMotion', motion)"
          >
            <Checkmark24Regular class="live2d-menu__icon live2d-menu__icon--hidden" />
            <span class="live2d-menu__label">{{ motion.name }}</span>
          </button>
        </template>

        <template v-if="wardrobes.length > 0">
          <div v-if="motionSections.length > 0" class="live2d-menu__divider"></div>

          <template v-for="group in wardrobes" :key="group.id">
            <div class="live2d-menu__group">{{ group.name }}</div>
            <button
              v-for="option in group.options"
              :key="option.id"
              class="live2d-menu__item"
              type="button"
              @click="emit('selectOutfit', group, option)"
            >
              <Checkmark24Regular
                :class="{
                  'live2d-menu__icon--hidden': !isOutfitSelected(group, option),
                }"
                class="live2d-menu__icon"
              />
              <span class="live2d-menu__label">{{ option.name }}</span>
            </button>
          </template>
        </template>

        <template v-if="expressions.length > 0">
          <div
            v-if="motionSections.length > 0 || wardrobes.length > 0"
            class="live2d-menu__divider"
          ></div>

          <div class="live2d-menu__group">表情</div>
          <button
            v-for="expression in expressions"
            :key="expression.runtimeName"
            class="live2d-menu__item"
            type="button"
            @click="emit('selectExpression', expression.runtimeName)"
          >
            <Checkmark24Regular
              :class="{
                'live2d-menu__icon--hidden': selectedExpression !== expression.runtimeName,
              }"
              class="live2d-menu__icon"
            />
            <span class="live2d-menu__label">{{ expression.name }}</span>
          </button>
        </template>

        <div
          v-if="motionSections.length === 0 && expressions.length === 0 && wardrobes.length === 0"
          class="live2d-menu__empty"
        >
          当前模型没有表情/动作/服装
        </div>
      </n-scrollbar>
    </div>
  </Teleport>
</template>

<style scoped>
.live2d-menu {
  position: fixed;
  z-index: 10000;
  min-width: 168px;
  max-width: 280px;
  padding: 4px;
  border: 1px solid rgba(0, 0, 0, 0.08);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.98);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.16);
  color: rgba(0, 0, 0, 0.82);
}

.live2d-menu--dark {
  border-color: rgba(255, 255, 255, 0.12);
  background: rgba(30, 30, 30, 0.98);
  color: rgba(255, 255, 255, 0.86);
}

.live2d-menu__item {
  display: flex;
  gap: 8px;
  align-items: center;
  width: 100%;
  padding: 8px 10px;
  border: none;
  border-radius: 6px;
  background: transparent;
  color: inherit;
  font-size: 0.85rem;
  text-align: left;
  cursor: pointer;
}

.live2d-menu__item:hover {
  background: rgba(127, 127, 127, 0.14);
}

.live2d-menu__icon {
  flex: 0 0 auto;
  width: 16px;
  height: 16px;
}

.live2d-menu__icon--hidden {
  visibility: hidden;
}

.live2d-menu__label {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.live2d-menu__group {
  padding: 6px 10px 2px;
  font-size: 0.75rem;
  opacity: 0.55;
}

.live2d-menu__empty {
  padding: 8px 10px;
  font-size: 0.85rem;
  opacity: 0.6;
}

.live2d-menu__divider {
  height: 1px;
  margin: 4px 6px;
  background: rgba(127, 127, 127, 0.24);
}
</style>
