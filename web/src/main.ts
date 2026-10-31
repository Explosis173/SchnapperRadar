import { createApp } from 'vue'
import App from './App.vue'
import { setUnauthorizedHandler } from './api/client'
import { router } from './router'
import './styles.css'

setUnauthorizedHandler(() => {
  if (router.currentRoute.value.name !== 'login') void router.replace({ name: 'login' })
})

createApp(App).use(router).mount('#app')
