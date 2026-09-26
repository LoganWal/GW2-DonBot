import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { stripTypeScriptTypes } from 'node:module'
import test from 'node:test'

const source = readFileSync(new URL('../../DonBot.Web/app/composables/useFightTypes.ts', import.meta.url), 'utf8')
const classificationSource = source.replace(
  "import { formatMilliseconds } from './useFormatters'",
  'const formatMilliseconds = () => { throw new Error("Duration formatting is outside this test") }',
)
const fightTypes = await import(`data:text/javascript;base64,${Buffer.from(stripTypeScriptTypes(classificationSource)).toString('base64')}`)

for (const [id, name] of [[55, 'Kela'], [56, 'Vloxx']])
{
  test(`${name} belongs to VoE strikes in filters and grouped views`, () => {
    assert.equal(fightTypes.fightGroup(id), 'VoE Strikes')
    assert.ok(fightTypes.strikeTypes.includes(id))
    assert.ok(fightTypes.pveTypes.includes(id))
    assert.ok(!fightTypes.raidTypes.includes(id))
    const categories = fightTypes.groupBySuperCategory([{ fightType: id }])
    assert.equal(categories.length, 1)
    assert.equal(categories[0].label, 'Strikes')
    assert.equal(categories[0].groups[0].label, 'VoE Strikes')
    const options = fightTypes.fightTypeGroupedOptions.find(group => group.label === 'VoE Strikes')
    assert.ok(options.items.some(item => item.value === id))
    assert.ok(fightTypes.fightTypeQuickCategories.find(category => category.label === 'Strikes').types.includes(id))
    assert.ok(!fightTypes.fightTypeQuickCategories.find(category => category.label === 'Raids (Wings)').types.includes(id))
  })
}
