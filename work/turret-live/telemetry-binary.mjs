// Browser-compatible L2TB v1 decoder. HTTP Content-Encoding handles transport
// compression; pass the resulting uncompressed ArrayBuffer/Uint8Array here.
const utf8 = new TextDecoder('utf-8', { fatal: true });
const vectors = ['position', 'anglesDegrees', 'velocity', 'aimRadians', 'lookRadians'];
const scalars = ['lean', 'dynamicStance', 'movement'];

export function decodeTelemetry(input) {
  const bytes = input instanceof Uint8Array ? input : new Uint8Array(input);
  if (bytes.byteLength < 24 || bytes.byteLength > 1048576) throw new Error('Invalid binary length');
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  const int = offset => view.getInt32(offset, true);
  if (int(0) !== 0x4254324c || int(4) !== 1) throw new Error('Unsupported binary version');
  const headerLength = int(8), coldLength = int(12), states = int(16), vehicles = int(20);
  if (headerLength < 2 || coldLength < 2 || states < 0 || vehicles < 0 || states + vehicles > 1024
      || 24 + headerLength + coldLength + states * 96 + vehicles * 56 !== bytes.byteLength)
    throw new Error('Invalid binary counts');
  const header = JSON.parse(utf8.decode(bytes.subarray(24, 24 + headerLength)));
  const records = JSON.parse(utf8.decode(bytes.subarray(24 + headerLength, 24 + headerLength + coldLength)));
  if (header.kind !== 'telemetry' || header.schemaVersion !== 2 || Object.hasOwn(header, 'records')
      || !Array.isArray(records) || records.length + states + vehicles > 1024)
    throw new Error('Invalid binary metadata');
  for (const record of records)
    if (record.type === 'state' || record.type === 'vehicle-state') throw new Error('State in cold table');
  let offset = 24 + headerLength + coldLength;
  function number(position, clock = false) {
    const value = view.getFloat32(position, true);
    if (!Number.isFinite(value)) throw new Error('Nonfinite binary number');
    // Match native JSON clocks, which truncate rather than round six decimals.
    return clock ? Math.trunc(value * 1e6) / 1e6 : Math.sign(value) * Math.round(Math.abs(value) * 1000) / 1000;
  }
  function table(count, character) {
    const ids = character ? 16 : 12, values = character ? 80 : 44;
    for (let i = 0; i < count; i++) {
      const base = offset + i * ids;
      const entity = int(base + 4), bits = int(base + 8);
      if (entity < 1 || (character ? ((bits & ~0x11f03) !== 0 || (bits & 255) > 2) : (bits !== 0 && bits !== 1)))
        throw new Error('Invalid actor/flags');
      let cursor = offset + count * ids + i * values;
      const timeMs = number(cursor, true), previousSampleMs = number(cursor + 4, true);
      cursor += 8;
      const data = { entity: `entity-${entity}`, previousSampleMs };
      for (const name of vectors.slice(0, character ? 5 : 3)) {
        data[name] = [number(cursor), number(cursor + 4), number(cursor + 8)];
        cursor += 12;
      }
      data.discontinuity = character ? (bits & 65536) !== 0 : bits !== 0;
      if (character) {
        data.stance = bits & 255;
        data.flags = (bits >>> 8) & 31;
        data.command = int(base + 12);
        if (data.command < 0) throw new Error('Invalid command');
        for (const name of scalars) { data[name] = number(cursor); cursor += 4; }
        if (Math.abs(data.lean) > 1 || data.dynamicStance < 0 || data.dynamicStance > 1 || data.movement < 0 || data.movement > 3)
          throw new Error('Invalid animation input');
      }
      if (previousSampleMs < 0 || previousSampleMs > timeMs) throw new Error('Invalid observation window');
      records.push({ sequence: int(base), timeMs, type: character ? 'state' : 'vehicle-state', data });
    }
    offset += count * (ids + values);
  }
  table(states, true);
  table(vehicles, false);
  records.sort((a, b) => a.sequence - b.sequence);
  let time = header.fromMs;
  if (!Number.isFinite(time) || time < 0 || !Number.isFinite(header.untilMs) || header.untilMs < time
      || !Number.isInteger(header.firstSequence) || header.firstSequence < 1) throw new Error('Invalid timeline');
  for (let i = 0; i < records.length; i++) {
    const record = records[i];
    if (record.sequence !== header.firstSequence + i || !Number.isFinite(record.timeMs)
        || record.timeMs < time || record.timeMs > header.untilMs) throw new Error('Invalid record timeline');
    time = record.timeMs;
  }
  return { ...header, records };
}
