import { client } from "@/apis/client.ts";

export function getDemoStatus() {
  return client.GET("/api/demo");
}

export function createDemoSession() {
  return client.POST("/api/demo/session");
}
