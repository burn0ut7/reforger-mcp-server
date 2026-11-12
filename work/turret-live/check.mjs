import fs from 'node:fs';
import assert from 'node:assert/strict';
import {decodeTelemetry} from './telemetry-binary.mjs';
const dir = 'C:/Users/Gray/Documents/My Games/ArmaReforgerWorkbench/profile/Lobby2Telemetry';
const id = '01a1c5c0-aabc-8f00-8400-00000007b002';
const batches = fs.readdirSync(dir).filter(n => n.startsWith(id) && n.endsWith('.l2t')).map(n => decodeTelemetry(fs.readFileSync(`${dir}/${n}`))).sort((a,b)=>a.revision-b.revision);
assert(batches.at(-1).closed);
assert(batches.every(b=>b.droppedRecords===0));
const records=batches.flatMap(b=>b.records).sort((a,b)=>a.sequence-b.sequence);
const result={recording:id,batches:batches.length,records:records.length,dropped:0,fixtures:[]};
for(const [entity, count] of [['entity-18',2],['entity-19',2],['entity-20',1],['entity-21',0]]) {
 const turrets=records.filter(r=>r.type==='turret-state' && r.data.entity===entity);
 assert(turrets.length>2);
 assert(turrets.every(r=>r.data.available && r.data.turrets.length===count));
 assert(records.some(r=>r.type==='despawn' && r.data.entity===entity));
 let changedWithHullStill=false, hullRotation=false;
 if(count) {
  for(let i=1;i<turrets.length;i++) {
   const a=turrets[i-1], b=turrets[i];
   if(JSON.stringify(a.data.turrets[0].direction)===JSON.stringify(b.data.turrets[0].direction)) continue;
   const hullAt=t=>records.filter(r=>r.type==='vehicle-state' && r.data.entity===entity && r.timeMs<=t).at(-1)?.data.anglesDegrees;
   const h1=hullAt(a.timeMs),h2=hullAt(b.timeMs);
   if(h1&&h2&&JSON.stringify(h1)===JSON.stringify(h2)) changedWithHullStill=true;
   if(h2&&Math.abs(h2[1]-90)<.1) hullRotation=true;
  }
  assert(changedWithHullStill,entity+' independent aim');
  assert(hullRotation,entity+' world direction rotates with hull');
 }
 result.fixtures.push({entity,turrets:count,snapshots:turrets.length,changedWithHullStill,hullRotation,despawn:true});
}
fs.writeFileSync(new URL('./result.json',import.meta.url),JSON.stringify(result,null,2));
console.log(JSON.stringify(result,null,2));
