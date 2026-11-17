import { ref } from "vue";
import { createDemoSession, getDemoStatus } from "@/apis/modules/demo.ts";
import { applySession, refreshAuthStatus } from "@/stores/auth.ts";

/** 后端启动时确定，前端只在启动阶段写入一次 */
export const demoMode = ref(false);

/** 必须在路由守卫执行前完成：演示站点由服务端直接签发会话，访客不会看到登录页 */
export async function bootstrapDemo(): Promise<void> {
  try {
    const { data, response } = await getDemoStatus();
    if (!response.ok || data?.enabled !== true) return;

    demoMode.value = true;

    if (await refreshAuthStatus()) return;

    const { data: session, response: sessionResponse } = await createDemoSession();
    if (sessionResponse.ok && session) applySession(session);
  } catch {
    // 查询失败按普通站点处理，不阻塞页面
  }
}
