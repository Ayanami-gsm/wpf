<template>
  <div class="login-container">
    <div class="login-card">
      <h1 class="title">用户登录</h1>
      <p class="subtitle">Vue 3 + C# Web API 前后端分离示例</p>

      <div class="form-group">
        <label>用户名</label>
        <input
          v-model="username"
          type="text"
          placeholder="请输入用户名"
          @keyup.enter="handleLogin"
        />
      </div>

      <div class="form-group">
        <label>密码</label>
        <input
          v-model="password"
          type="password"
          placeholder="请输入密码"
          @keyup.enter="handleLogin"
        />
      </div>

      <div v-if="message" class="message" :class="messageType">
        {{ message }}
      </div>

      <button class="login-btn" :disabled="loading" @click="handleLogin">
        {{ loading ? '登录中...' : '登 录' }}
      </button>

      <p class="tip">演示账号：admin / root</p>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { login } from './api/auth'

const username = ref('')
const password = ref('')
const loading = ref(false)
const message = ref('')
const messageType = ref('') // success | error

async function handleLogin() {
  // 简单前端校验
  if (!username.value || !password.value) {
    message.value = '请输入用户名和密码'
    messageType.value = 'error'
    return
  }

  loading.value = true
  message.value = ''
  try {
    const res = await login({ username: username.value, password: password.value })
    if (res.data.success) {
      message.value = `✅ ${res.data.message}，欢迎 ${res.data.username}！`
      messageType.value = 'success'
      // 演示：保存 token 到本地存储
      localStorage.setItem('token', res.data.token)
      localStorage.setItem('username', res.data.username)
    } else {
      message.value = `❌ ${res.data.message}`
      messageType.value = 'error'
    }
  } catch (err) {
    message.value = '❌ 请求失败，请确认后端服务已启动'
    messageType.value = 'error'
    console.error(err)
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login-card {
  background: #fff;
  border-radius: 12px;
  padding: 40px 36px;
  width: 380px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
}

.title {
  text-align: center;
  font-size: 24px;
  color: #333;
  margin-bottom: 6px;
}

.subtitle {
  text-align: center;
  font-size: 13px;
  color: #999;
  margin-bottom: 30px;
}

.form-group {
  margin-bottom: 18px;
}

.form-group label {
  display: block;
  font-size: 14px;
  color: #555;
  margin-bottom: 6px;
}

.form-group input {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 14px;
  outline: none;
  transition: border-color 0.2s;
}

.form-group input:focus {
  border-color: #667eea;
}

.message {
  padding: 10px 12px;
  border-radius: 6px;
  font-size: 13px;
  margin-bottom: 16px;
}

.message.success {
  background: #f0f9eb;
  color: #67c23a;
}

.message.error {
  background: #fef0f0;
  color: #f56c6c;
}

.login-btn {
  width: 100%;
  padding: 11px;
  background: linear-gradient(135deg, #667eea, #764ba2);
  color: #fff;
  border: none;
  border-radius: 6px;
  font-size: 16px;
  cursor: pointer;
  transition: opacity 0.2s;
}

.login-btn:hover {
  opacity: 0.9;
}

.login-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.tip {
  text-align: center;
  font-size: 12px;
  color: #aaa;
  margin-top: 16px;
}
</style>
