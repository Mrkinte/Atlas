import { uploadFavicon } from "@/apis/modules/images.ts";

export type FaviconFetchResult = { ok: true; iconUrl: string } | { ok: false };

/** 由服务端抓取站点图标入库，供图库与卡片编辑器共用 */
export async function fetchSiteFavicon(url: string): Promise<FaviconFetchResult> {
  try {
    const { data, response } = await uploadFavicon({ url });
    if (response.ok && data) return { ok: true, iconUrl: data.url };
    return { ok: false };
  } catch {
    return { ok: false };
  }
}
