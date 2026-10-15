export interface ScalingVariable { name: string; value: number; unit?: string; scalingMode: 'none' | 'linear' | 'sqrt' }
export function scaleVariable(v: ScalingVariable, ratio: number): number {
  if (/temp|preheat/i.test(v.name) || v.unit === '°F' || v.unit === '°C') return v.value
  if (!Number.isFinite(ratio) || ratio <= 0) return v.value
  if (v.scalingMode === 'linear') return v.value * ratio
  if (v.scalingMode === 'sqrt') return v.value * Math.sqrt(ratio)
  return v.value
}
