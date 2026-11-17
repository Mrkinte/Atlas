import { client } from "@/apis/client.ts";
import type { components } from "@/apis/schema";

type UpdateHttpsRequest = components["schemas"]["UpdateHttpsRequest"];

export function getHttpsStatus() {
  return client.GET("/api/https");
}

/** 改完需重启生效 */
export function updateHttps(body: UpdateHttpsRequest) {
  return client.PUT("/api/https", { body });
}

/** PFX/P12 单文件，或 PEM 证书 + 私钥 */
export function uploadCertificate(certificate: File, privateKey?: File, password?: string) {
  const form = new FormData();
  form.append("certificate", certificate);
  if (privateKey) form.append("privateKey", privateKey);
  if (password) form.append("password", password);
  return client.POST("/api/https/certificate", {
    // openapi-fetch 原样透传 FormData，生成类型把 binary 描述为 string，需要收窄
    body: form as never,
  });
}

export function generateSelfSignedCertificate() {
  return client.POST("/api/https/self-signed");
}
