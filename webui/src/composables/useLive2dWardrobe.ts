import type { Ref } from "vue";
import type { Live2dModelConfig } from "@/services/live2dModelConfig.ts";
import type {
  Live2dManifest,
  Live2dManifestWardrobeGroup,
  Live2dManifestWardrobeOption,
} from "@/services/live2dManifest.ts";
import { type Live2dInternalModelLike, type Live2dModelLike } from "@/services/live2dRuntime.ts";

interface Live2dWardrobeOptions {
  /** 服装参数写入它的内部模型 */
  getModel: () => Live2dModelLike | undefined;
  /** 服装分组、参数与默认值都来自清单 */
  getManifest: () => Live2dManifest | undefined;
  /** 服装选择持久化在这里 */
  modelConfig: Ref<Live2dModelConfig>;
}

/**
 * 按清单配置在每帧模型更新前写入参数，
 * 覆盖动作与表情对服装参数的改动，且不影响下一帧
 */
export function useLive2dWardrobe(options: Live2dWardrobeOptions) {
  /** 重新加载后是新实例，需要重新接入 */
  const wardrobeModels = new WeakSet<Live2dInternalModelLike>();

  /** 重新加载模型前先断开旧实例 */
  let disposeWardrobe: (() => void) | undefined;

  /** 配置优先，未保存时回退清单默认值 */
  function getOutfitOptionId(group: Live2dManifestWardrobeGroup): string | undefined {
    const saved = options.modelConfig.value.outfits.find((item) => item.groupId === group.id);
    return saved?.optionId ?? options.getManifest()?.defaults.wardrobe[group.id];
  }

  function isOutfitSelected(
    group: Live2dManifestWardrobeGroup,
    option: Live2dManifestWardrobeOption,
  ): boolean {
    return getOutfitOptionId(group) === option.id;
  }

  /** 按当前选择覆盖服装参数；在 beforeModelUpdate 中调用，只影响当帧 */
  function applyWardrobeParameters() {
    const manifest = options.getManifest();
    const coreModel = options.getModel()?.internalModel?.coreModel;
    if (!coreModel?.setParameterValueById || !manifest) return;

    for (const group of manifest.wardrobe) {
      const optionId = getOutfitOptionId(group);
      const option = group.options.find((item) => item.id === optionId);
      if (!option) continue;

      // weight 为 1 表示直接覆盖；组间参数不重叠已在清单解析时排除
      // 一个选项可能覆盖多个参数，逐项写入
      for (const [parameterId, value] of Object.entries(option.values)) {
        coreModel.setParameterValueById(parameterId, value, 1);
      }
    }
  }

  /**
   * InternalModel 每帧会先恢复保存的参数再更新，
   * 必须在 beforeModelUpdate 中写入才能覆盖动作与表情且不影响下一帧
   */
  function enableWardrobe() {
    const manifest = options.getManifest();
    const internal = options.getModel()?.internalModel;
    if (!internal || !manifest || manifest.wardrobe.length === 0) return;

    if (wardrobeModels.has(internal)) return;

    const handler = () => applyWardrobeParameters();
    if (typeof internal.on === "function") {
      internal.on("beforeModelUpdate", handler);
      disposeWardrobe = () => internal.off?.("beforeModelUpdate", handler);
    } else if (typeof internal.update === "function") {
      // 兜底：没有事件接口时包住 update，在模型更新前写入参数
      const update = internal.update.bind(internal);
      internal.update = (deltaTime, currentTime) => {
        applyWardrobeParameters();
        return update(deltaTime, currentTime);
      };
    }

    wardrobeModels.add(internal);
    applyWardrobeParameters();
  }

  function dispose() {
    disposeWardrobe?.();
    disposeWardrobe = undefined;
  }

  return {
    getOutfitOptionId,
    isOutfitSelected,
    applyWardrobeParameters,
    enableWardrobe,
    dispose,
  };
}
