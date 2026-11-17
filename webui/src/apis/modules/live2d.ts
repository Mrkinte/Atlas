import { client } from "@/apis/client.ts";
import type { components } from "@/apis/schema";

type UpdateLive2DModelConfigRequest = components["schemas"]["UpdateLive2DModelConfigRequest"];

export function getLive2dModelConfig(modelPath: string) {
  return client.GET("/api/live2d/config", {
    params: { query: { modelPath } },
  });
}

export function updateLive2dModelConfig(body: UpdateLive2DModelConfigRequest) {
  return client.PUT("/api/live2d/config", { body });
}

export function listLive2dModels() {
  return client.GET("/api/live2d/models");
}

/** ZIP，需含 .model3.json */
export function uploadLive2dModel(file: File, name?: string) {
  const form = new FormData();
  form.append("file", file);
  return client.POST("/api/live2d/models", {
    params: { query: name ? { name } : undefined },
    // openapi-fetch 原样透传 FormData，生成类型把 binary 描述为 string，需要收窄
    body: form as never,
  });
}

export function deleteLive2dModel(id: string) {
  return client.DELETE("/api/live2d/models/{id}", {
    params: { path: { id } },
  });
}

// 静态资源不是 OpenAPI 端点，无法走 openapi-fetch；裸 fetch 只允许出现在此模块
export async function fetchLive2dAssetJson(url: string | URL): Promise<unknown> {
  try {
    const response = await fetch(url, { cache: "no-cache" });
    if (!response.ok) return undefined;
    return await response.json();
  } catch {
    return undefined;
  }
}
