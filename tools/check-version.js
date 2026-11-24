#!/usr/bin/env node
/* Sürüm tutarlılığı (CI test işi): src/DesktopDuzenSabitleyici.csproj <Version> = News'in en üst kaydı (src/Core/CoreForms.cs),
   release-notes.json tr/en/ar dolu, semver X.Y.Z. Etiket push'unda etiket = vX.Y.Z. */
'use strict';
const fs = require('fs');
const path = require('path');
const R = (p) => fs.readFileSync(path.join(__dirname, '..', p), 'utf8');
const die = (m) => { console.error('::error::' + m); process.exit(1); };
const v = (R('src/DesktopDuzenSabitleyici.csproj').match(/<Version>([^<]+)<\/Version>/) || [])[1];
if (!/^\d+\.\d+\.\d+$/.test(v || '')) die('csproj <Version> X.Y.Z olmalı: ' + v);
const news = (R('src/Core/CoreForms.cs').match(/Items =\s*\{\s*\("([^"]+)"/) || [])[1];
if (news !== v) die(`News en üst kayıt (${news}) ≠ csproj sürümü (${v}) — src/Core/CoreForms.cs News.Items'a yeni sürümü ekleyin`);
const notes = JSON.parse(R('release-notes.json'));
for (const l of ['tr', 'en', 'ar']) if (!String(notes[l] || '').trim()) die('release-notes.json: ' + l + ' boş');
const ref = process.env.GITHUB_REF || '';
if (ref.startsWith('refs/tags/v') && ref.slice(10) !== 'v' + v) die(`etiket ${ref.slice(10)} ≠ v${v}`);
console.log('sürüm tutarlı: ' + v);
