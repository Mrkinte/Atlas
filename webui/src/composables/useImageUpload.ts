import type { LibraryKind } from "@/composables/useImageLibraryHost.ts";

/** 各类素材的上传限制，与后端校验保持一致 */
export const IMAGE_UPLOAD_LIMITS: Record<
  LibraryKind,
  { label: string; accept: string; maxSize: number }
> = {
  icon: {
    label: "图标",
    accept: "image/png,image/jpeg,image/webp,image/gif",
    maxSize: 1024 * 1024,
  },
  image: {
    label: "图片",
    accept: "image/png,image/jpeg,image/webp,image/gif,image/bmp",
    maxSize: 10 * 1024 * 1024,
  },
};
