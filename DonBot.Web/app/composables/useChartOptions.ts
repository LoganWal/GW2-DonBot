export const chartLineColor = (r: number, g: number, b: number, a = 1) =>
  `rgba(${r},${g},${b},${a})`

export const makeNumericDataset = (
  rows: any[],
  label: string,
  field: string,
  r: number,
  g: number,
  b: number
) => ({
  label,
  data: rows.map(p => Number(p[field]) || 0),
  borderColor: chartLineColor(r, g, b),
  backgroundColor: chartLineColor(r, g, b, 0.15),
  tension: 0.3,
  pointRadius: 4,
  pointHoverRadius: 7,
  fill: false,
})

export const createClickableChartOptions = (onPointClick: (index: number) => void) => computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  onClick: (_event: any, elements: any[]) => {
    if (elements.length) {
      onPointClick(elements[0].index)
    }
  },
  plugins: {
    legend: {
      labels: { color: '#94a3b8', boxWidth: 12 }
    },
    tooltip: {
      mode: 'index' as const,
      intersect: false,
      callbacks: {
        footer: () => ['Click to open log'],
      },
    },
  },
  scales: {
    x: {
      ticks: { color: '#64748b', maxTicksLimit: 10 },
      grid: { color: 'rgba(255,255,255,0.05)' }
    },
    y: {
      ticks: { color: '#64748b' },
      grid: { color: 'rgba(255,255,255,0.05)' }
    }
  }
}))
