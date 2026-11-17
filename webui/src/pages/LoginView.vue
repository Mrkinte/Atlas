<script lang="ts" setup>
import { reactive, ref, watch } from "vue";
import { type FormInst, type FormRules } from "naive-ui";
import { useRoute, useRouter } from "vue-router";
import Person24Regular from "@vicons/fluent/es/Person24Regular.js";
import Key24Regular from "@vicons/fluent/es/Key24Regular.js";
import { client } from "@/apis/client.ts";
import { applySession } from "@/stores/auth.ts";
import { settings } from "@/stores/settings.ts";

const route = useRoute();
const router = useRouter();

const formRef = ref<FormInst | null>(null);
const form = reactive({
  username: "",
  password: "",
});
const rememberMe = ref(false);
const isLoggingIn = ref(false);
/** 登录失败的内联提示，输入变化时自动清除 */
const errorText = ref("");

const rules: FormRules = {
  username: {
    required: true,
    message: "请输入用户名",
    trigger: ["input", "blur"],
  },
  password: {
    required: true,
    message: "请输入密码",
    trigger: ["input", "blur"],
  },
};

watch([() => form.username, () => form.password], () => {
  errorText.value = "";
});

const handleLogIn = async () => {
  if (isLoggingIn.value) return;

  try {
    await formRef.value?.validate();
  } catch {
    return;
  }

  isLoggingIn.value = true;
  errorText.value = "";
  try {
    const { data, response } = await client.POST("/api/auth/login", {
      body: {
        username: form.username,
        password: form.password,
        rememberMe: rememberMe.value,
      },
    });

    if (!response.ok || !data) {
      // 401 为空响应体，按状态码判断；429 保留 Retry-After 头
      errorText.value =
        response.status === 401
          ? "用户名或密码错误"
          : response.status === 429
            ? "尝试次数过多，请稍后再试"
            : "登录失败，请稍后重试";
      return;
    }

    // 登录响应自带过期时间，直接用它排续期定时器
    applySession(data, true);
    const redirect = route.query.redirect;
    // 只接受站内路径，避免被构造的 redirect 带到外部地址
    const target =
      typeof redirect === "string" && redirect.startsWith("/") && !redirect.startsWith("//")
        ? redirect
        : "/";
    router.push(target);
  } catch (err) {
    console.error(err);
    errorText.value = "网络异常，请稍后重试";
  } finally {
    isLoggingIn.value = false;
  }
};

const handleForgetPassword = () => {
  window.open("https://mrkinte.github.io/Atlas-Docs/docs/forgot-password.html")
};
</script>

<template>
  <div class="login-page">
    <n-card class="login-panel">
      <header class="login-panel__header">
        <img :src="settings.favicon || '/favicon.svg'" alt="" class="login-panel__logo" />
        <h1 class="login-panel__title">{{ settings.siteTitle }}</h1>
        <p v-if="settings.siteSubtitle" class="login-panel__subtitle">
          {{ settings.siteSubtitle }}
        </p>
      </header>

      <n-form
        ref="formRef"
        :model="form"
        :rules="rules"
        :show-label="false"
        @submit.prevent="handleLogIn"
      >
        <n-form-item path="username">
          <n-input
            v-model:value="form.username"
            autocomplete="username"
            autofocus
            placeholder="用户名"
            size="large"
          >
            <template #prefix>
              <n-icon :component="Person24Regular" :size="18" />
            </template>
          </n-input>
        </n-form-item>

        <n-form-item path="password">
          <n-input
            v-model:value="form.password"
            autocomplete="current-password"
            placeholder="密码"
            show-password-toggle
            size="large"
            type="password"
          >
            <template #prefix>
              <n-icon :component="Key24Regular" :size="18" />
            </template>
          </n-input>
        </n-form-item>

        <n-text v-if="errorText" class="login-panel__error" type="error">
          {{ errorText }}
        </n-text>

        <n-button :loading="isLoggingIn" attr-type="submit" block size="large" type="primary">
          登录
        </n-button>

        <div class="login-panel__footer">
          <n-checkbox v-model:checked="rememberMe">记住我</n-checkbox>
          <n-button text type="primary" @click="handleForgetPassword"> 忘记密码 </n-button>
        </div>
      </n-form>
    </n-card>
  </div>
</template>

<style scoped>
.login-page {
  box-sizing: border-box;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 32px 16px calc(32px + env(safe-area-inset-bottom));
  overflow-y: auto;
}

.login-panel {
  width: min(400px, 100%);
  border-radius: 18px;
  box-shadow:
    0 24px 64px rgba(0, 0, 0, 0.14),
    0 2px 8px rgba(0, 0, 0, 0.06);
  animation: login-panel-in 0.32s ease-out both;
}

.login-panel :deep(.n-card__content) {
  padding: 36px 32px 26px;
}

@keyframes login-panel-in {
  from {
    opacity: 0;
    transform: translateY(10px);
  }

  to {
    opacity: 1;
    transform: none;
  }
}

.login-panel__header {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  margin-bottom: 28px;
}

.login-panel__logo {
  width: 44px;
  height: 42px;
}

.login-panel__title {
  margin: 6px 0 0;
  font-size: 1.45rem;
  font-weight: 600;
  line-height: 1.3;
  letter-spacing: 0.01em;
  text-align: center;
  overflow-wrap: anywhere;
}

.login-panel__subtitle {
  margin: 0;
  font-size: 0.85rem;
  opacity: 0.62;
  text-align: center;
}

.login-panel :deep(.n-form-item) {
  margin-bottom: 18px;
}

.login-panel__error {
  display: block;
  margin: -4px 0 12px;
  font-size: 0.84rem;
}

.login-panel__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 16px;
}

@media (max-width: 540px) {
  .login-page {
    align-items: flex-start;
    padding-top: 8vh;
  }

  .login-panel :deep(.n-card__content) {
    padding: 28px 22px 22px;
  }

  .login-panel__header {
    margin-bottom: 22px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .login-panel {
    animation: none;
  }
}
</style>
