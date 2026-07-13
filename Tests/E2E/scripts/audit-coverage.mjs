import fs from 'node:fs/promises';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '..');
const catalog = JSON.parse(await fs.readFile(path.join(root, 'catalog/ui-actions.json'), 'utf8'));
const plan = JSON.parse(await fs.readFile(path.join(root, 'plans/run-plan.json'), 'utf8'));
const planned = new Set(plan.scenarios.map(s => s.actionId));
const expected = catalog.actions.filter(a => plan.lanes.includes(a.lane || 'local')).map(a => a.id);
const missing = expected.filter(id => !planned.has(id));
const empty = catalog.actions.filter(a => !Array.isArray(a.scenarios) || a.scenarios.length === 0).map(a => a.id);
if (missing.length || empty.length) {
  console.error(JSON.stringify({ missing, empty }, null, 2));
  process.exit(1);
}
console.log(`Kapsam tamam: ${expected.length} eylem, ${plan.scenarios.length} senaryo.`);
