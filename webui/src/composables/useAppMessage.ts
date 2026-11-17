import { useMessage } from "naive-ui";
import { showLive2dFeedback } from "@/services/live2dFeedback.ts";
import { demoMode } from "@/stores/demo.ts";

/**
 * success / info 优先由看板娘展示，
 * warning / error 保留 toast，避免重要信息被忽略
 */
export function useAppMessage() {
  const message = useMessage();

  return {
    success(content: string) {
      if (showLive2dFeedback(content, "success")) return;
      message.success(content);
    },
    info(content: string) {
      if (showLive2dFeedback(content, "info")) return;
      message.info(content);
    },
    warning(content: string) {
      message.warning(content);
    },
    error(content: string) {
      // 演示站点的写入都会被后端拒绝，提示只会打扰访客
      if (demoMode.value) {
        console.warn(content);
        return;
      }
      message.error(content);
    },
  };
}
