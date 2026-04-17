import type { Directive } from 'vue'

declare module 'vue' {
  interface GlobalDirectives {
    vFitText: Directive<HTMLElement>
  }
}
