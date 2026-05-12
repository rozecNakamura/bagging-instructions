import { readFile } from 'node:fs/promises';

for (const file of process.argv.slice(2)) {
    const content = await readFile(file, 'latin1');
    const pages = content.match(/\/Type\s*\/Page\b/g)?.length ?? 0;
    console.log(`${file}: ${pages} pages`);
}
