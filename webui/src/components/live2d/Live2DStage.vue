<script lang="ts" setup>
import { onBeforeUnmount, onMounted, ref, watch } from "vue";
import Live2dContextMenu from "@/components/live2d/Live2dContextMenu.vue";
import { clearSession, isAuthenticated } from "@/stores/auth.js";
import { settings, whenSettingsLoaded } from "@/stores/settings.js";
import { LIVE2D_LAYOUT_DEFAULTS } from "@/config/default.js";
import { UNAUTHORIZED_MESSAGE } from "@/config/messages.js";
import {
  BUILT_IN_MODEL_PATH,
  createEmptyLive2dModelConfig,
  type Live2dModelConfig,
  loadLive2dModelConfig,
  saveLive2dModelConfig,
} from "@/services/live2dModelConfig.js";
import {
  type Live2dManifest,
  type Live2dManifestWardrobeGroup,
  type Live2dManifestWardrobeOption,
  loadLive2dManifest,
} from "@/services/live2dManifest.js";
import {
  buildOml2dOptions,
  DEFAULT_MODEL_VOLUME,
  type Live2DInstance,
  type Live2dMotionItem,
  MOTION_PRIORITY_FORCE,
} from "@/services/live2dRuntime.js";
import { useAppMessage } from "@/composables/useAppMessage.js";
import { useSettingsSave } from "@/composables/useSettingsSave.js";
import { useLive2dLayout } from "@/composables/useLive2dLayout.js";
import { useLive2dHitTest } from "@/composables/useLive2dHitTest.js";
import { useLive2dMeshDebug } from "@/composables/useLive2dMeshDebug.js";
import { useLive2dStageInteraction } from "@/composables/useLive2dStageInteraction.js";
import { useLive2dStageMenu } from "@/composables/useLive2dStageMenu.js";
import { useLive2dIdleLoop } from "@/composables/useLive2dIdleLoop.js";
import { useLive2dMotionText } from "@/composables/useLive2dMotionText.js";
import { useLive2dWardrobe } from "@/composables/useLive2dWardrobe.js";

/** 组件挂载后生成的元素 ID；卸载时移除，避免热更新叠加多个舞台 */
const GENERATED_ELEMENT_IDS = ["oml2d-global-style", "oml2d-stage"];

let oml2d: Live2DInstance | undefined;
let modelReady = false;
let currentScale = LIVE2D_LAYOUT_DEFAULTS.scale;

/** 组件已卸载：异步回调返回后不再创建舞台或继续操作实例 */
let destroyed = false;

/** 当前舞台使用的模型地址，用于判断设置页是否切换模型 */
let loadedModelPath = "";

/** 当前模型的展示清单；没有清单时回退到 model3.json 的原始动作组与文件名 */
let manifest: Live2dManifest | undefined;

/** 当前模型的后端配置：开机/待机动作、表情、服装与语音开关 */
const modelConfig = ref<Live2dModelConfig>(createEmptyLive2dModelConfig(BUILT_IN_MODEL_PATH));

const message = useAppMessage();
const saveSiteSettings = useSettingsSave();

/** 未授权保存失败是否已提示过，避免连续换装时重复弹窗 */
let saveBlockedNotified = false;

let disposeClickThrough: (() => void) | undefined;
let disposeMeshDebug: (() => void) | undefined;

/** 布局持久化：边缘吸附、位置还原与拖动/缩放后的保存队列 */
const layout = useLive2dLayout({
  getScale: () => currentScale,
  isReady: () => modelReady,
  readLayout: () => modelConfig.value,
  applyLayout: (patch) => {
    Object.assign(modelConfig.value, patch);
  },
  saveLayout: () => saveLive2dModelConfig(modelConfig.value),
});

/** 是否开启边缘吸附：开启后舞台不能移出视口，关闭后允许拖到屏幕外 */
const edgeSnap = layout.edgeSnap;

/** 拖拽与缩放交互 */
const {
  resizeMode,
  setResizeMode,
  updateResizeHandleVisibility,
  enableStageDrag,
  enableStageResize,
  dispose: disposeInteraction,
} = useLive2dStageInteraction({
  getOml2d: () => oml2d,
  isReady: () => modelReady,
  getScale: () => currentScale,
  setScale: (scale) => {
    currentScale = scale;
  },
  layout,
});

const { isPointerOnModel, enable } = useLive2dHitTest({
  isReady: () => modelReady,
  isAdjusting: layout.isAdjusting,
  isResizeMode: () => resizeMode.value,
  getPixiApp: () => oml2d?.pixiApp?.app,
});

/** 开发环境调试：左键单击时在控制台输出命中的 ArtMesh，生产构建不注册监听 */
const { enable: enableMeshDebug } = useLive2dMeshDebug({
  isReady: () => modelReady,
  getModel: () => oml2d?.models?.model,
});

/** 菜单元素在子组件内渲染 */
const menuComponent = ref<InstanceType<typeof Live2dContextMenu> | null>(null);

const {
  menuVisible,
  menuX,
  menuY,
  menuExpressions,
  menuMotionSections,
  menuWardrobes,
  getExpressions,
  getMotions,
  getMotionName,
  resolveMotionItem,
  closeMenu,
  enableStageMenu,
  dispose: disposeMenu,
} = useLive2dStageMenu({
  getModel: () => oml2d?.models?.model,
  getManifest: () => manifest,
  isReady: () => modelReady,
  isPointerOnModel,
  getMenuElement: () => menuComponent.value?.menuElement ?? null,
});

const {
  clearIdleTimer,
  scheduleIdle,
  enableHitInterrupt,
  attachVisibility,
  dispose: disposeIdle,
} = useLive2dIdleLoop({
  getModel: () => oml2d?.models?.model,
  isReady: () => modelReady,
  isDestroyed: () => destroyed,
  getIdleMotionId: () => modelConfig.value.idleMotionId,
  resolveMotion: resolveMotionByConfigId,
  playMotion: (item) => {
    void oml2d?.models?.model?.motion?.(item.group, item.index, MOTION_PRIORITY_FORCE);
  },
  getMotions,
  closeMenu,
});

const {
  enableMotionTexts,
  enableFeedback,
  dispose: disposeMotionText,
} = useLive2dMotionText({
  getOml2d: () => oml2d,
  isReady: () => modelReady,
  getMotions,
  getManifest: () => manifest,
});

const {
  isOutfitSelected,
  applyWardrobeParameters,
  enableWardrobe,
  dispose: disposeWardrobe,
} = useLive2dWardrobe({
  getModel: () => oml2d?.models?.model,
  getManifest: () => manifest,
  modelConfig,
});

async function persistModelConfig() {
  const error = await saveLive2dModelConfig(modelConfig.value);
  if (!error) {
    saveBlockedNotified = false;
    return;
  }

  if (error === UNAUTHORIZED_MESSAGE) {
    // clearSession 会同步改写登录态，先记下原始状态再同步
    const wasAuthenticated = isAuthenticated.value;
    if (wasAuthenticated) clearSession(true);

    if (saveBlockedNotified) return;
    saveBlockedNotified = true;
    message.warning(
      wasAuthenticated ? "登录状态已失效，请重新登录后保存" : "当前未登录，登录后可自动保存",
    );
    return;
  }

  message.error(error);
}

async function selectExpression(name: string) {
  closeMenu();

  const applied = await oml2d?.models?.model?.expression?.(name);
  if (applied === false) {
    oml2d?.tipsMessage("切换表情失败", 1800, 3);
    return;
  }

  modelConfig.value.expression = name;
  void persistModelConfig();
}

/** 未配置时使用清单默认表情 */
function applyConfiguredExpression() {
  const name = modelConfig.value.expression || manifest?.defaults.expression || "";
  if (!name) return;

  if (!getExpressions().some((expression) => expression.Name === name)) return;

  // 清单声明了表情时只认清单里的条目，避免把服装等未声明的表情误当成普通表情
  if (
    manifest?.expressionsDeclared &&
    !manifest.expressions.some((expression) => expression.runtimeName === name)
  ) {
    return;
  }

  void oml2d?.models?.model?.expression?.(name);
}

/** 动作标识解析：清单 id 优先，其次按动作文件全局查找 */
function resolveMotionByConfigId(identifier: string): Live2dMotionItem | undefined {
  if (!identifier) return undefined;

  const manifestMotion = manifest?.motions.find((motion) => motion.id === identifier);
  if (manifestMotion) {
    const item = resolveMotionItem(manifestMotion.group, manifestMotion.file);
    if (item) {
      return { ...item, id: manifestMotion.id, name: manifestMotion.name };
    }
  }

  for (const [group, motions] of Object.entries(getMotions())) {
    const index = motions.findIndex((motion) => motion.File === identifier);
    if (index < 0) continue;

    return { group, index, name: getMotionName(identifier), file: identifier };
  }

  return undefined;
}

/** 右键菜单播放动作，仅本次播放，不写入配置 */
async function selectMotion(motion: Live2dMotionItem) {
  closeMenu();
  clearIdleTimer();

  const applied = await oml2d?.models?.model?.motion?.(
    motion.group,
    motion.index,
    MOTION_PRIORITY_FORCE,
  );
  if (applied === false) {
    oml2d?.tipsMessage("播放动作失败", 1800, 3);
    return;
  }

  // 动作结束后由待机定时器接管，期间高优先级动作会持续推迟待机
  scheduleIdle();
}

/** 播放配置的开机动作，未配置时使用清单默认入场动作 */
function playConfiguredEntry(): boolean {
  let item = resolveMotionByConfigId(modelConfig.value.entryMotionId);

  if (!item) {
    const entryId = manifest?.defaults.entryMotion;
    const motion = manifest?.motions.find((entry) => entry.id === entryId);
    if (motion) {
      item = resolveMotionItem(motion.group, motion.file);
    }
  }

  if (!item) return false;

  void oml2d?.models?.model?.motion?.(item.group, item.index, MOTION_PRIORITY_FORCE);

  scheduleIdle();
  return true;
}

async function selectOutfit(
  group: Live2dManifestWardrobeGroup,
  option: Live2dManifestWardrobeOption,
) {
  closeMenu();

  // 先更新本地状态再保存：未登录时接口会失败，但本次访问内仍然能立即换装
  modelConfig.value.outfits = [
    ...modelConfig.value.outfits.filter((item) => item.groupId !== group.id),
    { groupId: group.id, optionId: option.id },
  ];

  applyWardrobeParameters();
  void persistModelConfig();
}

function toggleResizeFromMenu() {
  closeMenu();
  if (!modelReady) return;

  setResizeMode(!resizeMode.value);
  oml2d?.tipsMessage(resizeMode.value ? "拖动右下角手柄调整大小" : "已退出尺寸调整", 2600, 3);
}

/** 关闭后由设置项驱动 App 卸载舞台 */
async function handleCloseStageFromMenu() {
  closeMenu();
  if (await saveSiteSettings({ live2DEnabled: false })) return;
  message.success("已关闭看板娘");
}

/** 开启吸附时收回移出视口的部分，关闭时记录当前像素位置 */
function toggleEdgeSnapFromMenu() {
  closeMenu();

  const enabled = !edgeSnap.value;
  layout.setEdgeSnap(enabled);
  if (enabled) {
    // 重新开启吸附时把舞台收回视口内，位置有变化会自动保存
    layout.clampStageIntoViewport();
  } else {
    // 把当前位置记成自由坐标，之后再次关闭吸附时不会跳回旧位置
    layout.persistLayout();
  }

  oml2d?.tipsMessage(enabled ? "已开启边缘吸附" : "已关闭边缘吸附，可拖动到屏幕外", 2600, 3);
}

function removeGeneratedElements() {
  for (const id of GENERATED_ELEMENT_IDS) {
    document.getElementById(id)?.remove();
  }
}

function destroyStage() {
  // 先标记卸载，阻止仍在等待中的异步初始化继续创建舞台
  destroyed = true;

  // 把最后的位置立即写回，再取消跟踪与防抖中的保存
  layout.dispose();
  disposeInteraction();
  disposeClickThrough?.();
  disposeClickThrough = undefined;
  disposeMeshDebug?.();
  disposeMeshDebug = undefined;
  disposeMenu();
  disposeMotionText();
  disposeIdle();

  // 库没有提供实例销毁方法，手动停掉渲染循环，避免热更新后遗留孤儿画布
  oml2d?.pixiApp?.app?.destroy(true);
  disposeWardrobe();

  oml2d = undefined;
  manifest = undefined;
  removeGeneratedElements();
  modelReady = false;
}

async function initStage() {
  const modelPath = settings.live2DModelPath || BUILT_IN_MODEL_PATH;

  // 配置与渲染库相互独立，并行加载可以减少等待
  const [config, { loadOml2d }] = await Promise.all([
    loadLive2dModelConfig(modelPath),
    import("oh-my-live2d"),
  ]);
  // 等待期间组件可能已被卸载，不能再创建舞台，否则会遗留无法回收的画布与监听器
  if (destroyed) return;

  modelConfig.value = config;
  // 每个模型各自保存缩放比例，加载后按模型配置恢复
  currentScale = config.scale;
  const volume = config.soundEnabled ? DEFAULT_MODEL_VOLUME : 0;
  loadedModelPath = modelPath;

  oml2d = loadOml2d(buildOml2dOptions(modelPath, currentScale, volume));

  // 欢迎语功能已移除，但库仍会在每次舞台入场时弹出内置的时段问候语，
  // 直接禁用该入口，避免它占用气泡或与动作台词抢显示
  if (oml2d.tips) oml2d.tips.welcome = () => {};

  oml2d.onLoad(async (status) => {
    if (destroyed) return;

    modelReady = status === "success";
    if (status === "fail") {
      console.warn("[Live2D] 模型加载失败:", loadedModelPath);
    } else if (status === "success") {
      layout.settleStoredPosition();
      // 加载展示清单；清单缺失时回退到原始动作组与文件名
      manifest = await loadLive2dManifest(loadedModelPath);
      // 等待清单期间组件可能已卸载，不能再注册动作与定时器
      if (destroyed) return;

      enableMotionTexts();
      enableWardrobe();
      enableHitInterrupt();
      applyConfiguredExpression();
      playConfiguredEntry();
      scheduleIdle();
    }

    updateResizeHandleVisibility();
  });
  // 舞台完全滑入后尺寸才最终确定，此时按保存的比例定位才准确
  oml2d.onStageSlideIn(() => {
    if (destroyed) return;
    layout.settleStoredPosition();
  });

  enableStageDrag();
  enableStageResize();
  disposeClickThrough = enable();
  enableStageMenu();
  disposeMeshDebug = enableMeshDebug();
  enableFeedback();
  layout.startTracking();
  attachVisibility();
}

/**
 * 切换模型后缩放与位置都跟随新模型，
 * 舞台大小由渲染库按模型原始尺寸算出
 */
async function reloadModel(modelPath: string) {
  const instance = oml2d;
  if (!instance || destroyed) return;

  const config = await loadLive2dModelConfig(modelPath);
  // 等待期间舞台可能已被销毁或替换，不能再操作旧实例
  if (destroyed || oml2d !== instance) return;

  modelConfig.value = config;
  currentScale = config.scale;

  const model = instance.options.models?.[0];
  if (model) {
    model.path = modelPath;
    model.scale = currentScale;
    model.volume = config.soundEnabled ? DEFAULT_MODEL_VOLUME : 0;
  }

  loadedModelPath = modelPath;
  closeMenu();
  clearIdleTimer();
  // 新模型会重新创建内部模型实例，先断开旧实例的服装参数覆盖与待机定时器
  disposeWardrobe();
  manifest = undefined;
  void instance.reloadModel();
}

// 设置页切换模型后重新加载；自身保存回写不会改变模型地址，因此不会触发
watch(
  () => settings.live2DModelPath,
  (path) => {
    const modelPath = path || BUILT_IN_MODEL_PATH;
    if (!oml2d || modelPath === loadedModelPath) return;

    reloadModel(modelPath);
  },
);

onMounted(async () => {
  // 先等首次配置到达再创建舞台，避免用默认尺寸渲染后再重载一次；
  // 请求迟迟不返回时也照常创建，不因为接口异常让看板娘一直不出现
  await Promise.race([whenSettingsLoaded(), new Promise((resolve) => setTimeout(resolve, 3000))]);
  // 等待期间组件可能已被卸载（例如刚启用又关闭看板娘）
  if (destroyed) return;
  await initStage();
});

onBeforeUnmount(() => {
  destroyStage();
});
</script>

<template>
  <Live2dContextMenu
    ref="menuComponent"
    :edge-snap="edgeSnap"
    :expressions="menuExpressions"
    :is-outfit-selected="isOutfitSelected"
    :motion-sections="menuMotionSections"
    :resize-mode="resizeMode"
    :selected-expression="modelConfig.expression"
    :visible="menuVisible"
    :wardrobes="menuWardrobes"
    :x="menuX"
    :y="menuY"
    @close-stage="handleCloseStageFromMenu"
    @select-expression="selectExpression"
    @select-motion="selectMotion"
    @select-outfit="selectOutfit"
    @toggle-edge-snap="toggleEdgeSnapFromMenu"
    @toggle-resize="toggleResizeFromMenu"
  />
</template>
