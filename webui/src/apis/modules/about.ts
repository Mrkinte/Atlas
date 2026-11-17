import { client } from "@/apis/client.ts";

export function getAbout() {
  return client.GET("/api/about");
}
