import { createHash } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';

const assets = [
  {
    path: 'artifacts/operator-ui/public/branding/amharc-logo-transparent.png',
    sha256: 'f52e22b435ee65595920a0445aa1f42b8d07faec07413fe9f6e432eac5297999',
  },
  {
    path: 'artifacts/operator-ui/public/branding/amharc-app-icon.png',
    sha256: 'b17f15d47008defa08a21c992414dc337151d055f22460ffc24f2e35e1a4d54b',
  },
  {
    path: 'agent-windows/src/AmharcAgent.Api/wwwroot/branding/amharc-logo-transparent.png',
    sha256: 'f52e22b435ee65595920a0445aa1f42b8d07faec07413fe9f6e432eac5297999',
  },
];

let failed = false;
for (const asset of assets) {
  const bytes = await readFile(resolve(asset.path));
  const actual = createHash('sha256').update(bytes).digest('hex');
  if (actual !== asset.sha256) {
    failed = true;
    console.error(`BRAND ASSET MISMATCH: ${asset.path}`);
    console.error(`  expected ${asset.sha256}`);
    console.error(`  actual   ${actual}`);
  } else {
    console.log(`OK ${asset.path}`);
  }
}

if (failed) process.exit(1);
