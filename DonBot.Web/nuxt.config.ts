import Aura from '@primevue/themes/aura'
import { definePreset } from '@primevue/themes'

const BlueAura = definePreset(Aura, {
  semantic: {
    primary: {
      50: '{blue.50}',
      100: '{blue.100}',
      200: '{blue.200}',
      300: '{blue.300}',
      400: '{blue.400}',
      500: '{blue.500}',
      600: '{blue.600}',
      700: '{blue.700}',
      800: '{blue.800}',
      900: '{blue.900}',
      950: '{blue.950}',
    }
  }
})

export default defineNuxtConfig({
  compatibilityDate: '2026-03-15',
  ssr: false,
  nitro: {
    preset: 'static'
  },
  modules: ['@primevue/nuxt-module'],
  css: ['primeicons/primeicons.css'],
  app: {
    head: {
      htmlAttrs: { class: 'dark' },
      link: [
        { rel: 'icon', type: 'image/png', href: '/donbot.png' },
        { rel: 'preconnect', href: 'https://fonts.googleapis.com' },
        { rel: 'preconnect', href: 'https://fonts.gstatic.com', crossorigin: '' },
        { rel: 'stylesheet', href: 'https://fonts.googleapis.com/css2?family=Inter:ital,opsz,wght@0,14..32,400;0,14..32,500;0,14..32,600;0,14..32,700;1,14..32,400&display=swap' }
      ]
    }
  },
  primevue: {
    options: {
      theme: {
        preset: BlueAura,
        options: {
          darkModeSelector: '.dark'
        }
      }
    }
  },
  runtimeConfig: {
    public: {
      apiBase: 'http://localhost:5001'
    }
  }
})
