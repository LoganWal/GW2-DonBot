// noinspection JSUnusedGlobalSymbols
export default defineNuxtPlugin((nuxtApp) => {
  nuxtApp.vueApp.directive('fit-text', {
    mounted: fit,
    updated: fit,
  })
})

function fit(el: HTMLElement) {
  el.style.fontSize = ''
  let px = parseFloat(window.getComputedStyle(el).fontSize)
  while (el.scrollWidth > el.clientWidth && px > 10) {
    px -= 0.5
    el.style.fontSize = px + 'px'
  }
}
