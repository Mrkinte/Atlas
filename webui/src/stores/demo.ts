import { ref } from "vue";
import { client } from "@/apis/client.ts";
import { createDemoSession, getDemoStatus } from "@/apis/modules/demo.ts";
import { DEMO_READONLY_MESSAGE } from "@/config/messages.ts";
import { applySession, refreshAuthStatus } from "@/stores/auth.ts";

/** 后端启动时确定，前端只在启动阶段写入一次 */
export const demoMode = ref(false);

// 后端虽然会拒绝写入，但上传文件时请求体已经开始传输，等服务端返回 403 早已白耗上行带宽；
// 演示模式下直接短路，Multipart 即为文件上传（Live2D、图库、备份）
client.use({
  onRequest({ request }) {
    if (!demoMode.value) return;
    if (!request.headers.get("content-type")?.startsWith("multipart/form-data")) return;

    return new Response(
      JSON.stringify({
        type: "about:blank",
        title: "Forbidden",
        status: 403,
        code: "demo-readonly",
        detail: DEMO_READONLY_MESSAGE,
      }),
      {
        status: 403,
        headers: { "content-type": "application/problem+json; charset=utf-8" },
      },
    );
  },
});

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
