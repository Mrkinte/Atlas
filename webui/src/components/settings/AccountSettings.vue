<script lang="ts" setup>
import { ref } from "vue";
import { useAppMessage } from "@/composables/useAppMessage.ts";
import { useRouter } from "vue-router";
import { client } from "@/apis/client.ts";
import { clearSession, currentUser, logoutSession } from "@/stores/auth.ts";
import { demoMode } from "@/stores/demo.ts";

const message = useAppMessage();
const router = useRouter();

const newUsername = ref("");
const usernamePassword = ref("");
const changingUsername = ref(false);

/** 后端会吊销全部令牌，成功后需用新用户名重新登录 */
async function handleChangeUsername() {
  const username = newUsername.value.trim();

  if (!username) {
    message.warning("请输入新用户名");
    return;
  }

  if (username.length > 32 || /\s/.test(username)) {
    message.warning("用户名最多 32 个字符，且不能包含空格");
    return;
  }

  if (username === currentUser.value) {
    message.info("新用户名与当前用户名相同");
    return;
  }

  if (!usernamePassword.value) {
    message.warning("请输入当前密码以验证身份");
    return;
  }

  changingUsername.value = true;
  try {
    const { response } = await client.POST("/api/auth/change-username", {
      body: {
        newUsername: username,
        password: usernamePassword.value,
      },
    });

    if (response.status === 401) {
      message.error("密码错误");
      return;
    }

    if (response.status === 409) {
      message.error("该用户名已被使用");
      return;
    }

    if (response.status === 400) {
      message.error("用户名格式不合法");
      return;
    }

    if (!response.ok) {
      message.error("用户名修改失败");
      return;
    }

    message.success(`用户名已修改为 ${username}，请重新登录`);
    clearSession(true);
    await router.push("/login");
  } catch {
    message.error("网络异常，请稍后重试");
  } finally {
    changingUsername.value = false;
  }
}

const oldPassword = ref("");
const newPassword = ref("");
const confirmPassword = ref("");
const submitting = ref(false);

/** 成功后后端会吊销全部令牌，需重新登录 */
async function handleChangePassword() {
  if (!oldPassword.value || !newPassword.value) {
    message.warning("请填写旧密码和新密码");
    return;
  }

  if (newPassword.value.length < 8) {
    message.warning("新密码至少 8 位");
    return;
  }

  if (newPassword.value !== confirmPassword.value) {
    message.warning("两次输入的新密码不一致");
    return;
  }

  submitting.value = true;
  try {
    const { response } = await client.POST("/api/auth/reset-password", {
      body: {
        oldPassword: oldPassword.value,
        newPassword: newPassword.value,
      },
    });

    if (response.status === 401) {
      message.error("旧密码错误");
      return;
    }

    if (!response.ok) {
      message.error("密码修改失败");
      return;
    }

    message.success("密码已修改，请重新登录");
    clearSession(true);
    await router.push("/login");
  } catch {
    message.error("网络异常，请稍后重试");
  } finally {
    submitting.value = false;
  }
}

async function handleLogout() {
  await logoutSession();
  await router.push("/login");
}
</script>

<template>
  <h2 class="settings__heading">账户安全</h2>

  <n-flex :size="24" vertical>
    <n-card class="settings__card" size="small" title="用户名">
      <div class="settings__form">
        <div class="settings__field">
          <div class="settings__field-label">当前用户名</div>
          <div class="settings__field-value">{{ currentUser ?? "—" }}</div>
        </div>

        <div class="settings__field">
          <div class="settings__field-label">新用户名</div>
          <n-input
            v-model:value="newUsername"
            :maxlength="32"
            placeholder="1–32 个字符，不能包含空格"
          />
        </div>

        <div class="settings__field">
          <div class="settings__field-label">当前密码</div>
          <n-input
            v-model:value="usernamePassword"
            placeholder="用于验证身份"
            show-password-toggle
            type="password"
            @keydown.enter="handleChangeUsername"
          />
        </div>

        <div class="settings__hint">修改用户名后所有会话将失效，需要用新用户名重新登录</div>

        <div class="settings__actions">
          <n-button :loading="changingUsername" type="primary" @click="handleChangeUsername">
            修改用户名
          </n-button>
        </div>
      </div>
    </n-card>

    <n-card class="settings__card" size="small" title="密码">
      <div class="settings__form">
        <div class="settings__field">
          <div class="settings__field-label">旧密码</div>
          <n-input
            v-model:value="oldPassword"
            placeholder="请输入当前密码"
            show-password-toggle
            type="password"
          />
        </div>

        <div class="settings__field">
          <div class="settings__field-label">新密码</div>
          <n-input
            v-model:value="newPassword"
            placeholder="至少 8 位"
            show-password-toggle
            type="password"
          />
        </div>

        <div class="settings__field">
          <div class="settings__field-label">确认新密码</div>
          <n-input
            v-model:value="confirmPassword"
            placeholder="再次输入新密码"
            show-password-toggle
            type="password"
            @keydown.enter="handleChangePassword"
          />
        </div>

        <div class="settings__hint">修改密码后所有会话将失效，需要重新登录</div>

        <div class="settings__actions">
          <n-button :loading="submitting" type="primary" @click="handleChangePassword">
            修改密码
          </n-button>
        </div>
      </div>
    </n-card>

    <n-card v-if="!demoMode" class="settings__card" size="small" title="登录状态">
      <div class="settings__form">
        <div class="settings__hint">退出登录会吊销当前设备上的访问令牌与刷新令牌</div>

        <div class="settings__actions">
          <n-button secondary type="error" @click="handleLogout"> 退出登录 </n-button>
        </div>
      </div>
    </n-card>
  </n-flex>
</template>

<style scoped>
/* 卡片内边距与字段间距放宽 */
.settings__card :deep(.n-card__content) {
  padding: 20px 24px 24px;
}

.settings__form {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.settings__field-label {
  margin-bottom: 8px;
  font-size: 0.9rem;
}

.settings__field-value {
  font-size: 1rem;
  font-weight: 500;
}

.settings__hint {
  font-size: 0.8rem;
  opacity: 0.6;
}

.settings__actions {
  display: flex;
  justify-content: flex-end;
}
</style>
