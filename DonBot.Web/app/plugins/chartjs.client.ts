import {
  Chart,
  LineController,
  BarController,
  DoughnutController,
  LineElement,
  PointElement,
  BarElement,
  ArcElement,
  LinearScale,
  CategoryScale,
  Legend,
  Tooltip,
  Filler,
} from 'chart.js'

export default defineNuxtPlugin(() => {
  Chart.register(
    LineController,
    BarController,
    DoughnutController,
    LineElement,
    PointElement,
    BarElement,
    ArcElement,
    LinearScale,
    CategoryScale,
    Legend,
    Tooltip,
    Filler,
  )
})
