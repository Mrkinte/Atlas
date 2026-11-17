<script lang="ts" setup>
import { computed, reactive, ref } from "vue";
import { type FormInst, type FormRules } from "naive-ui";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import type { CardEditorExpose } from "@/cards/types.ts";
import Image24Regular from "@vicons/fluent/es/Image24Regular.js";
import { fetchSiteFavicon } from "@/services/favicon.ts";
import { openImageLibrary } from "@/composables/useImageLibraryHost.ts";
import ServiceCard from "@/cards/ServiceCard/ServiceCard.vue";
import type { HostMode, ProtocolMode, ServiceCardConfig } from "@/cards/ServiceCard/config.ts";
import { isValidHttpUrl, resolveAutoServiceUrl } from "@/cards/ServiceCard/target.ts";

const message = useAppMessage();

/** 历史数据可能缺字段，全部可选 */
const props = defineProps<Partial<ServiceCardConfig>>();

/** 补齐编辑态默认值 */
const editValues = reactive<ServiceCardConfig>({
  name: props.name ?? "",
  description: props.description ?? "",
  hostMode: props.hostMode === "manual" ? "manual" : "auto",
  protocol: props.protocol === "https" || props.protocol === "http" ? props.protocol : "auto",
  port: props.port ?? undefined,
  path: props.path ?? "",
  url1: props.url1 ?? "",
  url2: props.url2 ?? "",
  url3: props.url3 ?? "",
  icon: props.icon ?? "",
  openMethod: props.openMethod ?? "_blank",
});
const formRef = ref<FormInst | null>(null);

const hostModeOptions: Array<{ label: string; value: HostMode }> = [
  { label: "自动识别", value: "auto" },
  { label: "手动设置", value: "manual" },
];

const protocolOptions: Array<{ label: string; value: ProtocolMode }> = [
  { label: "跟随当前网址", value: "auto" },
  { label: "强制 https", value: "https" },
  { label: "强制 http", value: "http" },
];

const openMethodOptions = [
  {
    label: "新标签打开",
    value: "_blank",
  },
  {
    label: "当前页面打开",
    value: "_self",
  },
];

const onlineIconLibraryOptions = [
  {
    label: "DashboardIcons",
    key: "https://dashboardicons.com/",
  },
  {
    label: "阿里巴巴 IconFont",
    key: "https://www.iconfont.cn",
  },
];

/** 自动模式下实时预览目标地址 */
const autoUrlPreview = computed(() =>
  resolveAutoServiceUrl(editValues.port, editValues.path, editValues.protocol),
);

/** 自动模式要求端口或反代路径，手动模式要求网址完整 */
const rules = computed<FormRules>(() => {
  const result: FormRules = {
    name: {
      required: true,
      message: "请输入服务名称",
      trigger: ["input", "blur"],
    },
  };

  if (editValues.hostMode === "auto") {
    result.port = {
      validator: () => {
        if (editValues.port || editValues.path?.trim()) return true;
        return new Error("端口和反向代理路径至少填写一项");
      },
      trigger: ["input", "blur"],
    };
  } else {
    result.url1 = {
      validator: (_rule, value: string) => {
        if (!value?.trim()) return new Error("请填写第一个网址");
        return isValidHttpUrl(value) ? true : new Error("请输入完整网址（含 http:// 或 https://）");
      },
      trigger: ["input", "blur"],
    };
    result.url2 = {
      validator: (_rule, value: string) => {
        if (!value?.trim()) return true;
        return isValidHttpUrl(value) ? true : new Error("请输入完整网址（含 http:// 或 https://）");
      },
      trigger: ["input", "blur"],
    };
    result.url3 = {
      validator: (_rule, value: string) => {
        if (!value?.trim()) return true;
        return isValidHttpUrl(value) ? true : new Error("请输入完整网址（含 http:// 或 https://）");
      },
      trigger: ["input", "blur"],
    };
  }

  return result;
});

async function validate(): Promise<boolean> {
  try {
    await formRef.value?.validate();
    return true;
  } catch {
    return false;
  }
}

function getConfig() {
  return { ...editValues };
}

async function handleSelectIcon() {
  const item = await openImageLibrary("icon");
  if (!item) return;
  editValues.icon = item.url;
}

const faviconSourceUrl = computed(() => {
  if (editValues.hostMode === "auto") return autoUrlPreview.value;

  return (
    [editValues.url1, editValues.url2, editValues.url3]
      .map((url) => url?.trim() ?? "")
      .find((url) => isValidHttpUrl(url)) ?? ""
  );
});

const fetchingIcon = ref(false);

/** 服务端抓取站点图标并存入图标库 */
async function handleFetchIcon() {
  if (fetchingIcon.value) return;

  const url = faviconSourceUrl.value;
  if (!url) {
    message.warning("请先填写有效的目标网址");
    return;
  }

  fetchingIcon.value = true;
  // 失败提示结合目标网址可访问性，不展示接口错误文案
  const result = await fetchSiteFavicon(url);
  fetchingIcon.value = false;

  if (result.ok) {
    editValues.icon = result.iconUrl;
    message.success("已自动获取网站图标");
  } else {
    message.error("获取图标失败，请确认目标网址可访问");
  }
}

const handleOpenIconWebsite = (key: string) => {
  window.open(key, "_blank");
};

defineExpose({ validate, getConfig } satisfies CardEditorExpose);
</script>

<template>
  <n-form ref="formRef" :model="editValues" :rules="rules" label-placement="top">
    <n-form-item label="预览">
      <div class="service-editor__preview">
        <ServiceCard v-bind="editValues" />
      </div>
    </n-form-item>

    <n-form-item label="图标">
      <n-flex align="center">
        <n-icon :size="48">
          <Image24Regular v-if="!editValues.icon" />
          <img v-else :src="editValues.icon" alt="" width="100%" />
        </n-icon>

        <n-button @click="handleSelectIcon"> 选择图标 </n-button>
        <n-button :loading="fetchingIcon" @click="handleFetchIcon"> 自动获取图标 </n-button>
        <n-dropdown
          :options="onlineIconLibraryOptions"
          trigger="click"
          @select="handleOpenIconWebsite"
        >
          <n-button> 在线图标库 </n-button>
        </n-dropdown>
      </n-flex>
    </n-form-item>

    <n-grid :cols="2" :x-gap="12">
      <n-gi>
        <n-form-item label="名称" path="name">
          <n-input
            v-model:value="editValues.name"
            maxlength="20"
            placeholder="请输入服务名称"
            show-count
          />
        </n-form-item>
      </n-gi>
      <n-gi>
        <n-form-item label="描述">
          <n-input
            v-model:value="editValues.description"
            maxlength="50"
            placeholder="请输入一段简短的描述"
            show-count
          />
        </n-form-item>
      </n-gi>
    </n-grid>

    <n-form-item label="主机地址">
      <n-flex :size="4" vertical>
        <n-radio-group v-model:value="editValues.hostMode">
          <n-radio-button v-for="item in hostModeOptions" :key="item.value" :value="item.value">
            {{ item.label }}
          </n-radio-button>
        </n-radio-group>
        <n-text depth="3">
          {{
            editValues.hostMode === "auto"
              ? "按当前访问 Atlas 的方式自动推断：端口访问使用端口，反代子路径访问使用路径"
              : "最多填写三个完整网址，点击卡片后从上方选择要打开哪一个"
          }}
        </n-text>
      </n-flex>
    </n-form-item>

    <template v-if="editValues.hostMode === 'auto'">
      <n-form-item label="传输协议">
        <n-flex :size="4" style="width: 100%" vertical>
          <n-radio-group v-model:value="editValues.protocol">
            <n-radio-button v-for="item in protocolOptions" :key="item.value" :value="item.value">
              {{ item.label }}
            </n-radio-button>
          </n-radio-group>
          <n-text depth="3">
            {{
              editValues.protocol === "auto"
                ? "使用与当前访问 Atlas 相同的协议"
                : "无论当前网页使用什么协议，都按这里的选择生成地址"
            }}
          </n-text>
        </n-flex>
      </n-form-item>

      <n-grid :cols="2" :x-gap="12">
        <n-gi>
          <n-form-item label="端口" path="port">
            <n-input-number
              v-model:value="editValues.port"
              :max="65535"
              :min="1"
              :show-button="false"
              placeholder="例如 8080"
              style="width: 100%"
            />
          </n-form-item>
        </n-gi>
        <n-gi>
          <n-form-item label="反向代理路径">
            <n-input
              v-model:value="editValues.path"
              maxlength="100"
              placeholder="例如 /qBittorrent"
              show-count
            />
          </n-form-item>
        </n-gi>
      </n-grid>
      <n-form-item label="目标地址">
        <n-text :code="true" :depth="autoUrlPreview ? undefined : 3">
          {{ autoUrlPreview || "请填写端口或反向代理路径" }}
        </n-text>
      </n-form-item>
    </template>

    <template v-else>
      <n-form-item label="网址 1" path="url1">
        <n-input
          v-model:value="editValues.url1"
          maxlength="200"
          placeholder="http(s)://example.com:8080"
          show-count
        />
      </n-form-item>
      <n-form-item label="网址 2（可选）" path="url2">
        <n-input
          v-model:value="editValues.url2"
          maxlength="200"
          placeholder="http(s)://example.com/other"
          show-count
        />
      </n-form-item>
      <n-form-item label="网址 3（可选）" path="url3">
        <n-input
          v-model:value="editValues.url3"
          maxlength="200"
          placeholder="http(s)://example.com/another"
          show-count
        />
      </n-form-item>
    </template>

    <n-form-item label="打开方式">
      <n-select v-model:value="editValues.openMethod" :options="openMethodOptions" />
    </n-form-item>
  </n-form>
</template>

<style scoped>
.service-editor__preview {
  width: 100%;
  /* 与栅格里一行卡片的高度接近 */
  height: 72px;
  /* 预览只负责展示外观，避免误点后直接跳到目标服务 */
  pointer-events: none;
}
</style>
