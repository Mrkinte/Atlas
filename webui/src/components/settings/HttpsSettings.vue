<script lang="ts" setup>
import { computed, onMounted, ref } from "vue";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import { extractErrorMessage } from "@/apis/errors.ts";
import {
  generateSelfSignedCertificate,
  getHttpsStatus,
  updateHttps,
  uploadCertificate,
} from "@/apis/modules/https.ts";
import { NETWORK_ERROR } from "@/config/messages.ts";
import type { components } from "@/apis/schema";

type HttpsStatus = components["schemas"]["HttpsStatusResponse"];

const message = useAppMessage();

const status = ref<HttpsStatus | null>(null);
const portValue = ref<number | null>(null);
const saving = ref(false);
const uploading = ref(false);
const generating = ref(false);
const pfxPassword = ref("");
const certInputRef = ref<HTMLInputElement | null>(null);

/** 允许上传的证书文件：PFX 单文件，或 PEM 证书 + 私钥 */
const CERT_ACCEPT = ".pfx,.p12,.pem,.crt,.cer,.key";
const CERT_MAX_SIZE = 1024 * 1024;

const notAfter = computed(() =>
  status.value?.certificate ? new Date(status.value.certificate.notAfter) : null,
);

const daysLeft = computed(() => {
  if (!notAfter.value) return null;
  const diff = notAfter.value.getTime() - Date.now();
  return Math.max(0, Math.ceil(diff / (24 * 60 * 60 * 1000)));
});

const notAfterText = computed(() => (notAfter.value ? notAfter.value.toLocaleDateString() : ""));

function applyStatus(next: HttpsStatus) {
  status.value = next;
  portValue.value = next.port;
}

async function loadStatus() {
  try {
    const { data, error, response } = await getHttpsStatus();
    if (!response.ok) {
      // 401 时由路由接管跳转登录页，不再额外提示
      if (response.status !== 401) {
        message.error(extractErrorMessage(error, "操作失败", response.status));
      }
      return;
    }
    if (data) applyStatus(data);
  } catch {
    message.error(NETWORK_ERROR);
  }
}

onMounted(loadStatus);

/** 监听地址只在启动时绑定，改动后需要重启 */
async function saveSettings(enabled: boolean, port: number | null) {
  if (saving.value || !port) return;
  saving.value = true;
  try {
    const { data, error, response } = await updateHttps({ enabled, port });

    if (!response.ok) {
      message.error(extractErrorMessage(error, "操作失败", response.status));
      return;
    }
    if (data) {
      applyStatus(data);
      if (data.restartRequired) {
        message.warning("已保存，重启 Atlas 后生效");
      } else {
        message.success("已保存");
      }
    }
  } catch {
    message.error(NETWORK_ERROR);
  } finally {
    saving.value = false;
  }
}

function handleToggle(enabled: boolean) {
  void saveSettings(enabled, portValue.value);
}

/** 端口在失焦或回车时提交，避免输入过程中反复请求 */
function handlePortCommit() {
  const current = status.value?.port ?? null;
  if (portValue.value === current) return;
  void saveSettings(status.value?.enabled ?? false, portValue.value);
}

function handlePickCertificate() {
  if (uploading.value) return;
  certInputRef.value?.click();
}

/** PEM 私钥文件里含 PRIVATE KEY 标记 */
async function isPrivateKeyFile(file: File): Promise<boolean> {
  if (/\.(key)$/i.test(file.name)) return true;
  if (!/\.(pem|key)$/i.test(file.name)) return false;
  return (await file.text()).includes("PRIVATE KEY");
}

/** 单个 PFX/P12，或 PEM 证书 + 私钥；上传后后端热替换，无需重启 */
async function handleCertificateChange(event: Event) {
  const input = event.target as HTMLInputElement;
  const files = [...(input.files ?? [])];
  input.value = "";
  if (!files.length) return;

  if (files.some((file) => file.size > CERT_MAX_SIZE)) {
    message.error("证书文件不能超过 1 MB");
    return;
  }

  let certificate: File | undefined;
  let privateKey: File | undefined;
  const isPkcs12 = files.length === 1 && /\.(pfx|p12)$/i.test(files[0].name);

  if (isPkcs12) {
    certificate = files[0];
  } else {
    const entries = await Promise.all(
      files.map(async (file) => ({
        file,
        privateKey: await isPrivateKeyFile(file),
      })),
    );

    certificate = entries.find((entry) => !entry.privateKey)?.file;
    privateKey = entries.find((entry) => entry.privateKey)?.file;

    if (!certificate || !privateKey) {
      message.error("请同时选择 PEM 证书与私钥文件");
      return;
    }
  }

  uploading.value = true;
  try {
    const { data, error, response } = await uploadCertificate(
      certificate,
      privateKey,
      pfxPassword.value || undefined,
    );

    if (!response.ok) {
      message.error(extractErrorMessage(error, "操作失败", response.status));
      return;
    }
    if (data) applyStatus(data);
    pfxPassword.value = "";
    message.success("证书已生效");
  } catch {
    message.error(NETWORK_ERROR);
  } finally {
    uploading.value = false;
  }
}

/** 浏览器仍会提示不受信任，适合内网临时使用 */
async function handleSelfSigned() {
  if (generating.value) return;
  generating.value = true;
  try {
    const { data, error, response } = await generateSelfSignedCertificate();

    if (!response.ok) {
      message.error(extractErrorMessage(error, "操作失败", response.status));
      return;
    }
    if (data) applyStatus(data);
    message.success("已生成自签名证书");
  } catch {
    message.error(NETWORK_ERROR);
  } finally {
    generating.value = false;
  }
}
</script>

<template>
  <h2 class="settings__heading">HTTPS</h2>

  <n-card size="small">
    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">启用 HTTPS</div>
        <div class="settings__row-desc">
          启用后 HTTP 请求自动跳转到 HTTPS，未配置证书时会生成自签名证书
        </div>
      </div>
      <div class="settings__row-control">
        <n-flex :size="8" align="center">
          <n-tag v-if="status?.restartRequired" size="small" type="warning"> 待重启 </n-tag>
          <n-tag v-else-if="status?.active" size="small" type="success"> 运行中 </n-tag>
          <n-switch
            :disabled="!status"
            :loading="saving"
            :value="status?.enabled ?? false"
            @update:value="handleToggle"
          />
        </n-flex>
      </div>
    </div>

    <n-divider class="settings__divider" />

    <div class="settings__row">
      <div class="settings__row-main">
        <div class="settings__row-label">HTTPS 端口</div>
        <div class="settings__row-desc">默认 5443，监听地址只在启动时绑定，修改后需重启 Atlas</div>
      </div>
      <div class="settings__row-control">
        <n-input-number
          v-model:value="portValue"
          :disabled="saving || !status"
          :max="65535"
          :min="1"
          :show-button="false"
          placeholder="端口"
          style="width: 120px"
          @blur="handlePortCommit"
          @keydown.enter="handlePortCommit"
        />
      </div>
    </div>

    <n-alert v-if="status?.restartRequired" :bordered="false" class="https__alert" type="warning">
      配置已保存，重启 Atlas 服务后可改用 HTTPS 端口
      {{ status.port }} 访问。
    </n-alert>

    <n-divider class="settings__divider" />

    <div class="settings__row settings__row--stack">
      <div class="settings__row-main">
        <div class="settings__row-label">服务器证书</div>
        <div class="settings__row-desc">上传后立即生效，无需重启，支持 CA 签发证书与自签名证书</div>
      </div>

      <div v-if="status?.certificate" class="https__cert">
        <div class="https__cert-line">
          <span class="https__cert-key">名称</span>
          {{ status.certificate.subject }}
        </div>
        <div class="https__cert-line">
          <span class="https__cert-key">颁发者</span>
          {{ status.certificate.issuer }}
        </div>
        <div class="https__cert-line">
          <span class="https__cert-key">有效期至</span>
          {{ notAfterText }}
          <n-tag v-if="daysLeft !== null" :type="daysLeft < 30 ? 'warning' : 'default'" size="tiny">
            剩余 {{ daysLeft }} 天
          </n-tag>
        </div>
        <div class="https__cert-line">
          <span class="https__cert-key">类型</span>
          {{ status.certificate.isSelfSigned ? "自签名" : "CA 签发" }}
        </div>
      </div>
      <div v-else class="https__cert-empty">尚未配置证书</div>

      <n-alert v-if="status?.error" :bordered="false" type="warning">
        {{ status.error }}
      </n-alert>

      <n-flex :size="8" vertical>
        <n-input
          v-model:value="pfxPassword"
          :disabled="uploading"
          placeholder="PFX 密码（没有密码可留空）"
          show-password-on="click"
          type="password"
        />
        <n-flex :size="8">
          <n-button :disabled="!status" :loading="uploading" @click="handlePickCertificate">
            上传证书
          </n-button>
          <n-button
            :disabled="!status"
            :loading="generating"
            quaternary
            type="warning"
            @click="handleSelfSigned"
          >
            生成自签名证书
          </n-button>
        </n-flex>
        <div class="https__hint">支持 .pfx / .p12 单文件，或同时选择 PEM 证书与私钥两个文件</div>
      </n-flex>

      <input
        ref="certInputRef"
        :accept="CERT_ACCEPT"
        hidden
        multiple
        type="file"
        @change="handleCertificateChange"
      />
    </div>
  </n-card>
</template>

<style scoped>
.https__alert {
  margin-top: 8px;
}

.https__cert {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 12px;
  border-radius: 8px;
  background-color: rgba(127, 127, 127, 0.08);
  font-size: 0.85rem;
}

.https__cert-line {
  display: flex;
  gap: 8px;
  align-items: center;
  min-width: 0;
  word-break: break-all;
}

.https__cert-key {
  flex: none;
  width: 56px;
  opacity: 0.6;
}

.https__cert-empty {
  padding: 12px;
  border-radius: 8px;
  background-color: rgba(127, 127, 127, 0.08);
  font-size: 0.85rem;
  opacity: 0.6;
}

.https__hint {
  font-size: 0.78rem;
  opacity: 0.55;
}
</style>
