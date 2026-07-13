import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';

const root = path.resolve(import.meta.dirname, '..');
const catalog = JSON.parse(await fs.readFile(path.join(root, 'catalog/ui-actions.json'), 'utf8'));
const ids = catalog.actions.map(action => action.id);
if (new Set(ids).size !== ids.length) throw new Error('Katalogda yinelenen action id var.');

const selectedLanes = new Set((process.env.E2E_LANES || 'local').split(',').map(x => x.trim()));
const scenarios = catalog.actions.flatMap(action => {
  const lane = action.lane || 'local';
  if (!selectedLanes.has(lane)) return [];
  return action.scenarios.map(variant => ({
    id: `${action.id}.${variant}`.toUpperCase().replace(/[^A-Z0-9.]+/g, '_'),
    actionId: action.id,
    variant,
    surface: action.surface,
    lane,
    requiredPermissions: action.permissions
  }));
});

const plan = {
  schemaVersion: 1,
  runId: crypto.randomUUID(),
  generatedAt: new Date().toISOString(),
  immutable: true,
  catalogVersion: catalog.version,
  lanes: [...selectedLanes],
  scenarios
};
await fs.mkdir(path.join(root, 'plans'), { recursive: true });
await fs.writeFile(path.join(root, 'plans/run-plan.json'), JSON.stringify(plan, null, 2) + '\n');
console.log(`${scenarios.length} senaryo planlandi: ${path.join(root, 'plans/run-plan.json')}`);
