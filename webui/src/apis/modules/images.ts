import { client } from "@/apis/client.ts";
import { extractErrorMessage } from "@/apis/errors.ts";
import { DEMO_READONLY_MESSAGE } from "@/config/messages.ts";
import { demoMode } from "@/stores/demo.ts";
import type { components } from "@/apis/schema";

type FaviconRequest = components["schemas"]["FaviconRequest"];
type ImageResponse = components["schemas"]["ImageResponse"];

/** 不传 kind 返回全部分类 */
export function listImages(query?: { kind?: string }) {
  return client.GET("/api/images", { params: { query } });
}

/** 无进度回调，需要进度用 uploadImageWithProgress */
export function uploadImage(file: File, query?: { kind?: string }) {
  const body = new FormData();
  body.append("file", file);
  return client.POST("/api/images", { params: { query }, body });
}

export function uploadFavicon(body: FaviconRequest) {
  return client.POST("/api/images/favicon", { body });
}

export function deleteImage(imageId: ImageResponse["id"]) {
  return client.DELETE("/api/images/{imageId}", {
    params: { path: { imageId } },
  });
}

/** openapi-fetch 拿不到上传进度，改用 XHR，onProgress 取值 0-100 */
export function uploadImageWithProgress(
  file: File,
  kind: string,
  onProgress?: (percent: number) => void,
): Promise<ImageResponse> {
  // XHR 绕过 client 中间件，演示模式下同样不能把文件发出去
  if (demoMode.value) return Promise.reject(new Error(DEMO_READONLY_MESSAGE));

  return new Promise((resolve, reject) => {
    const formData = new FormData();
    formData.append("file", file);

    const xhr = new XMLHttpRequest();
    xhr.open("POST", `/api/images?kind=${kind}`);

    xhr.upload.addEventListener("progress", (event) => {
      if (event.lengthComputable) {
        onProgress?.(Math.round((event.loaded / event.total) * 100));
      }
    });

    xhr.addEventListener("load", () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        try {
          resolve(JSON.parse(xhr.responseText) as ImageResponse);
        } catch {
          reject(new Error("上传响应解析失败"));
        }
        return;
      }

      let payload: unknown;
      try {
        payload = JSON.parse(xhr.responseText);
      } catch {
        payload = xhr.responseText;
      }
      // ProblemDetails：401 提示登录，其余优先展示 detail
      reject(new Error(extractErrorMessage(payload, "上传失败", xhr.status)));
    });

    xhr.addEventListener("error", () => reject(new Error("网络异常，上传失败")));
    xhr.addEventListener("abort", () => reject(new Error("上传已取消")));

    xhr.send(formData);
  });
}
