import { createRouter, createWebHistory } from 'vue-router'
import { api } from './api/client'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'feed', component: () => import('./views/FeedView.vue') },
    { path: '/search', name: 'search', component: () => import('./views/SearchView.vue') },
    { path: '/saved', name: 'saved', component: () => import('./views/SavedView.vue') },
    { path: '/settings', name: 'settings', component: () => import('./views/SettingsView.vue') },
    { path: '/login', name: 'login', component: () => import('./views/LoginView.vue'), meta: { public: true } },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})

let authenticated: boolean | null = null

router.beforeEach(async (to) => {
  if (to.meta.public) return true
  if (authenticated === null) {
    try {
      authenticated = (await api.me()).authenticated
    } catch {
      authenticated = false
    }
  }
  return authenticated ? true : { name: 'login' }
})

router.afterEach((to) => {
  // Nach Login/Logout den Status neu ermitteln lassen.
  if (to.name === 'login') authenticated = null
})
