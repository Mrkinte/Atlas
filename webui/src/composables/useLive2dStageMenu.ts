import { nextTick, ref } from "vue";
import type { Live2dManifest, Live2dManifestWardrobeGroup } from "@/services/live2dManifest.ts";
import {
  type Live2dExpression,
  type Live2dExpressionItem,
  type Live2dModelLike,
  type Live2dMotion,
  type Live2dMotionItem,
  type Live2dMotionSection,
} from "@/services/live2dRuntime.ts";

/** oh-my-live2d 创建的舞台容器 ID */
const STAGE_ELEMENT_ID = "oml2d-stage";

interface Live2dStageMenuOptions {
  /** 表情与动作列表来自它的内部设置 */
  getModel: () => Live2dModelLike | undefined;
  /** 没有清单时回退 model3.json 原始动作组 */
  getManifest: () => Live2dManifest | undefined;
  isReady: () => boolean;
  /** 右键只接管落在模型上的事件 */
  isPointerOnModel: (clientX: number, clientY: number) => boolean | undefined;
  /** 用于判断事件是否发生在菜单内部 */
  getMenuElement: () => HTMLElement | null;
}

/**
 * 右键菜单状态、内容构建与舞台监听
 * 菜单 UI 在 Live2dContextMenu.vue 渲染，菜单动作由舞台组件编排
 */
export function useLive2dStageMenu(options: Live2dStageMenuOptions) {
  const menuVisible = ref(false);
  const menuX = ref(0);
  const menuY = ref(0);
  const menuExpressions = ref<Live2dExpressionItem[]>([]);
  const menuMotionSections = ref<Live2dMotionSection[]>([]);
  const menuWardrobes = ref<Live2dManifestWardrobeGroup[]>([]);

  let disposeMenu: (() => void) | undefined;

  function getExpressions(): Live2dExpression[] {
    return options.getModel()?.internalModel?.settings?.expressions ?? [];
  }

  function getMotions(): Record<string, Live2dMotion[]> {
    return options.getModel()?.internalModel?.settings?.motions ?? {};
  }

  /** 去掉 .motion3.json 后缀作为展示名 */
  function getMotionName(file: string): string {
    const name = file.split("/").pop() ?? file;
    return name.replace(/\.motion3\.json$/i, "");
  }

  /** 按原始组名与文件在 model3.json 中反查动作下标；组名缺省时全局查找 */
  function resolveMotionItem(
    group: string | undefined,
    file: string,
  ): Live2dMotionItem | undefined {
    const groups = Object.entries(getMotions());
    const candidates: Array<[string, Live2dMotion[]]> = group
      ? [[group, getMotions()[group] ?? []]]
      : groups;

    for (const [groupName, motions] of candidates) {
      const index = motions.findIndex((motion) => motion.File === file);
      if (index < 0) continue;

      return { group: groupName, index, name: getMotionName(file), file };
    }

    return undefined;
  }

  function buildRuntimeMotionSections(): Live2dMotionSection[] {
    const sections: Live2dMotionSection[] = [];
    for (const [group, motions] of Object.entries(getMotions())) {
      const items = motions.flatMap((motion, index): Live2dMotionItem[] => {
        const file = motion.File;
        if (typeof file !== "string" || file.length === 0) return [];

        return [{ group, index, name: getMotionName(file), file }];
      });

      if (items.length > 0) {
        sections.push({ key: `runtime:${group}`, group, items });
      }
    }

    return sections;
  }

  /** 清单声明了动作时以清单为准，否则回退运行时动作组 */
  function buildMotionSections(): Live2dMotionSection[] {
    const manifest = options.getManifest();
    if (!manifest || manifest.motions.length === 0) {
      return buildRuntimeMotionSections();
    }

    const sections: Live2dMotionSection[] = [];
    const used = new Set<string>();
    const identity = (item: Live2dMotionItem) => `${item.group}|${item.file}`;

    if (manifest.motions.length > 0) {
      const motionsById = new Map(manifest.motions.map((motion) => [motion.id, motion]));

      // 依次生成清单声明的自定义分组，组名与动作别名都来自清单
      for (const group of manifest.motionGroups) {
        const items: Live2dMotionItem[] = [];

        for (const motionId of group.motions) {
          const motion = motionsById.get(motionId);
          if (!motion) continue;

          const item = resolveMotionItem(motion.group, motion.file);
          if (!item || used.has(identity(item))) continue;

          used.add(identity(item));
          items.push({ ...item, id: motion.id, name: motion.name });
        }

        if (items.length > 0) {
          sections.push({
            key: `manifest:${group.id}`,
            group: group.name,
            items,
          });
        }
      }

      // 清单里存在但未归入任何分组的动作统一放进「其他动作」
      const rest: Live2dMotionItem[] = [];
      for (const motion of manifest.motions) {
        const item = resolveMotionItem(motion.group, motion.file);
        if (!item || used.has(identity(item))) continue;

        used.add(identity(item));
        rest.push({ ...item, id: motion.id, name: motion.name });
      }
      if (rest.length > 0) {
        sections.push({ key: "manifest:others", group: "其他动作", items: rest });
      }
    }

    return sections;
  }

  /** 清单声明表情时只展示别名，否则回退模型自带表情 */
  function buildMenuExpressions(): Live2dExpressionItem[] {
    const manifest = options.getManifest();
    const runtimeExpressions = getExpressions();

    if (manifest?.expressionsDeclared) {
      return manifest.expressions.flatMap((expression) =>
        runtimeExpressions.some((runtime) => runtime.Name === expression.runtimeName)
          ? [{ runtimeName: expression.runtimeName, name: expression.name }]
          : [],
      );
    }

    return runtimeExpressions.map((expression) => ({
      runtimeName: expression.Name,
      name: expression.Name,
    }));
  }

  function openMenu(clientX: number, clientY: number) {
    menuExpressions.value = buildMenuExpressions();
    menuMotionSections.value = buildMotionSections();
    menuWardrobes.value = options.getManifest()?.wardrobe ?? [];
    menuX.value = clientX;
    menuY.value = clientY;
    menuVisible.value = true;

    void nextTick(() => {
      const element = options.getMenuElement();
      if (!element) return;

      const padding = 8;
      menuX.value = Math.min(
        Math.max(padding, menuX.value),
        Math.max(padding, window.innerWidth - element.offsetWidth - padding),
      );
      menuY.value = Math.min(
        Math.max(padding, menuY.value),
        Math.max(padding, window.innerHeight - element.offsetHeight - padding),
      );
    });
  }

  function closeMenu() {
    menuVisible.value = false;
  }

  /** 只有右键落在模型不透明像素上时才接管，其余位置保留浏览器默认菜单 */
  function handleContextMenu(event: MouseEvent) {
    if (!options.isReady()) return;

    // 右键事件不在渲染帧内，无法直接读像素，复用 ticker 中最近一次的采样结果；
    // 没有可用采样时视为无法判定，直接接管
    if (options.isPointerOnModel(event.clientX, event.clientY) === false) return;

    event.preventDefault();
    event.stopPropagation();
    openMenu(event.clientX, event.clientY);
  }

  let disposeMenuDismiss: (() => void) | undefined;

  function enableMenuDismiss() {
    const handlePointerDown = () => closeMenu();
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") closeMenu();
    };
    // 捕获阶段能收到菜单内部的滚动事件，菜单自身滚动时不能关闭
    const handleScroll = (event: Event) => {
      const target = event.target;
      const menuElement = options.getMenuElement();
      if (target instanceof Node && menuElement?.contains(target)) return;
      closeMenu();
    };

    document.addEventListener("pointerdown", handlePointerDown);
    window.addEventListener("keydown", handleKeyDown);
    window.addEventListener("resize", closeMenu);
    window.addEventListener("scroll", handleScroll, true);

    disposeMenuDismiss = () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      window.removeEventListener("keydown", handleKeyDown);
      window.removeEventListener("resize", closeMenu);
      window.removeEventListener("scroll", handleScroll, true);
      disposeMenuDismiss = undefined;
    };
  }

  function enableStageMenu() {
    const stage = document.getElementById(STAGE_ELEMENT_ID);
    if (!stage) return;

    // Pixi 对鼠标右键同样会派发 pointertap，触发 oh-my-live2d 的点击动作；
    // 在捕获阶段拦下右键指针事件，右键只保留菜单行为；菜单自身的右键交给模板处理
    const blockRightPointer = (event: PointerEvent) => {
      if (event.button !== 2) return;

      const target = event.target;
      const menuElement = options.getMenuElement();
      if (target instanceof Node && menuElement?.contains(target)) return;
      event.stopPropagation();
    };

    stage.addEventListener("contextmenu", handleContextMenu);
    stage.addEventListener("pointerdown", blockRightPointer, true);
    stage.addEventListener("pointerup", blockRightPointer, true);
    enableMenuDismiss();

    disposeMenu = () => {
      stage.removeEventListener("contextmenu", handleContextMenu);
      stage.removeEventListener("pointerdown", blockRightPointer, true);
      stage.removeEventListener("pointerup", blockRightPointer, true);
      disposeMenuDismiss?.();
      closeMenu();
      disposeMenu = undefined;
    };
  }

  function dispose() {
    disposeMenu?.();
  }

  return {
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
    dispose,
  };
}
