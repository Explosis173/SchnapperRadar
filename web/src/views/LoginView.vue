<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { ApiError, api } from '../api/client'

const router = useRouter()
const password = ref('')
const error = ref<string | null>(null)
const busy = ref(false)

async function submit(): Promise<void> {
  busy.value = true
  error.value = null
  try {
    await api.login(password.value)
    password.value = ''
    await router.replace({ name: 'feed' })
  } catch (e) {
    error.value = e instanceof ApiError && e.status === 429 ? e.message : 'Falsches Passwort.'
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <main class="login">
    <form @submit.prevent="submit">
      <h1>🎯 SchnapperRadar</h1>
      <input v-model="password" type="password" autocomplete="current-password" placeholder="Passwort" required autofocus />
      <button class="primary" type="submit" :disabled="busy">{{ busy ? 'Prüfe…' : 'Anmelden' }}</button>
      <p v-if="error" class="error">{{ error }}</p>
    </form>
  </main>
</template>

<style scoped>
.login { min-height: 100dvh; display: grid; place-items: center; padding: 24px; }
form { width: min(360px, 100%); display: flex; flex-direction: column; gap: 12px; }
h1 { text-align: center; margin: 0 0 12px; }
</style>
