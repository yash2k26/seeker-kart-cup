// A scripted opponent for the Unity play-mode room test (RoomSmoke.Run): joins a room as a guest, reports loaded,
// and after GO drives the real circuit at a steady 28 m/s (slower than the AI on our kart, so we win), streaming
// states the room's referee accepts, and reports the finish of that drive.
//   node scripts/bot.mjs http://127.0.0.1:8787 CODE
import { drive, race } from './drive.mjs';

const [, , BASE = 'http://127.0.0.1:8787', CODE] = process.argv;
if (!CODE) { console.error('usage: bot.mjs BASE CODE'); process.exit(2); }
const log = (...a) => console.log('[bot]', ...a);
const ws = new WebSocket(`${BASE.replace(/^http/, 'ws')}/v1/rooms/${CODE}/ws`);
const send = o => ws.send(JSON.stringify(o));
let track = 'beach', finished = false, sawOtherState = 0;
setTimeout(() => { log('timeout'); process.exit(2); }, 240_000);

ws.onopen = () => send({ t: 'hello', device: 'bot-' + Math.random().toString(36).slice(2, 10), character: 1, kart: 'ooli' });
ws.onerror = e => { log('socket error', e.message ?? ''); process.exit(2); };
ws.onclose = () => { log('closed'); process.exit(finished ? 0 : 3); };
ws.onmessage = ev => {
  const m = JSON.parse(ev.data);
  switch (m.t) {
    case 'welcome': log('welcome', m.id, 'players', m.room.players.length); break;
    case 'load': track = m.track; log('load', m.track, `${m.race}/${m.races}`); setTimeout(() => send({ t: 'loaded' }), 500); break;
    case 'countdown': {
      const d = drive(track, 28);
      log('countdown, GO in', m.startAt - Date.now(), 'ms; will finish in', d.finishT.toFixed(1), 's');
      race(send, m.startAt, d).then(() => { finished = true; log('sent fin', d.ms); });
      break;
    }
    case 'sp': send({ t: 'spr', s: m.s }); break;                   // the room timing our round trip
    case 's': if (++sawOtherState === 1) log('first state from another racer', m.id); break;
    case 'finished': log('someone finished', m.id, m.ms, m.dnf ? 'DNF' : ''); break;
    case 'results':
      log('results', JSON.stringify(m.results.order.map(o => `${o.place}:${o.name}:${o.ms}:${o.dnf ? 'DNF' : 'ok'}`)), 'states seen', sawOtherState);
      setTimeout(() => { send({ t: 'leave' }); process.exit(0); }, 1500);
      break;
    case 'error': log('error', m.msg); break;
    case 'closed': log('room closed', m.reason); process.exit(3); break;
  }
};
