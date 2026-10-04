"use strict";
const assert = require("node:assert/strict");
const PetInteractions = require("../Kedit.Console/PetWeb/pet-interactions.js");
const flush = () => new Promise(resolve => setImmediate(resolve));
async function fixture(eyes = true, keyboard = true) {
    const ids = ["ParamAngleX", "ParamAngleY", "Keyboard", "Clothes", ...(eyes ? ["ParamEyeBallX", "ParamEyeBallY"] : [])];
    const values = ids.map(() => 0); values[2] = .2; values[3] = .8;
    const events = {}, messages = [];
    const core = {
        getParameterIndex: id => ids.indexOf(id), getParameterCount: () => ids.length,
        getParameterValueByIndex: i => values[i], setParameterValueByIndex: (i, v, weight = 1) => { values[i] = values[i] * (1 - weight) + v * weight; },
        addParameterValueByIndex: (i, v) => { values[i] += v; },
        getParameterDefaultValue: () => 0, getParameterMinimumValue: () => -1, getParameterMaximumValue: () => 1,
        saveParameters() {}
    };
    let starts = 0, stopped = true, finishStart;
    const manager = { loadMotion: async () => ({ setIsLoop() {}, setIsLoopFadeIn() {} }),
        isFinished: () => stopped, stopAllMotions() { stopped = true; } };
    const model = { internalModel: { coreModel: core, motionManager: manager, on: (n, fn) => { events[n] = fn; } },
        motion: () => { starts++; stopped = false; return new Promise(resolve => { finishStart = () => resolve(true); }); } };
    global.fetch = async () => ({ ok: true, json: async () => ({ Curves: [{ Target: "Parameter", Id: "Keyboard" }] }) });
    const subject = new PetInteractions(model, { FileReferences: { Motions: keyboard ? { "": [{ File: "motion-keyboard.motion3.json" }] } : {} } }, "https://model.kedit.local/test.model3.json", m => messages.push(m));
    await subject.ready;
    subject.configure({ mouseFollow: true, typingEnabled: true });
    const tick = () => { subject.update(33); events.afterMotionUpdate(); };
    return { subject, values, events, messages, tick, finish: () => finishStart(), starts: () => starts };
}
(async () => {
    const f = await fixture(); const s = f.subject;
    assert.equal(f.messages[0].eyes, true); assert.equal(f.messages[0].typing, true);
    s.receive({ x: 99, y: -99, typing: false }); s.update(33); f.events.beforeModelUpdate();
    assert(f.values[4] > 0 && f.values[4] < .7); assert(f.values[5] < 0); assert.equal(f.values[0], 0);
    s.receive({ typing: true }); f.tick(); f.finish(); await flush();
    for (let i = 0; i < 100; i++) { s.receive({ typing: true }); f.tick(); }
    assert.equal(f.starts(), 1, "Continuous typing must not restart motion");
    f.values[2] = 1; s.receive({ typing: false }); f.tick();
    assert(f.values[2] < 1 && f.values[2] > .2, "Stop should interpolate");
    for (let i = 0; i < 10; i++) f.tick();
    assert(Math.abs(f.values[2] - .2) < 1e-8); assert.equal(f.values[3], .8, "Clothing must survive recovery");
    s.receive({ typing: true }); f.tick();
    const pause = s.beforeCue("paused"); f.finish(); await pause; await flush(); s.afterCue(); f.tick();
    assert.equal(s.typing, false); assert.equal(f.values[2], .2);
    s.receive({ typing: true }); f.tick(); assert.equal(f.starts(), 2, "Pause must suppress new typing");
    await s.beforeCue("resumed"); s.afterCue(); f.tick(); f.finish(); await flush();
    assert.equal(f.starts(), 3);
    s.lastInput = -Infinity; f.tick(); assert.equal(s.typing, false, "Lost input stream must stop typing");
    assert.equal(await s.beforeCue("keyboard"), false, "Shortcut bubbles must not restart keyboard motion");
    const fallback = await fixture(false, false);
    assert.equal(fallback.messages[0].eyes, false); assert.equal(fallback.messages[0].head, true); assert.equal(fallback.messages[0].typing, false);
    fallback.subject.receive({ x: 1, typing: true }); fallback.tick(); fallback.events.beforeModelUpdate();
    assert(fallback.values[0] > 0); assert.equal(fallback.starts(), 0);
    for (let i = 0; i < 30; i++) {
        fallback.values[0] = -.5; // Author's natural head swing must not reverse the intended look direction.
        fallback.subject.receive({ x: 1, typing: false }); fallback.tick(); fallback.events.beforeModelUpdate();
    }
    assert(fallback.values[0] > 0);
    fallback.subject.configure({ mouseFollow: false });
    for (let i = 0; i < 40; i++) { fallback.values[0] = -.5; fallback.tick(); fallback.events.beforeModelUpdate(); }
    assert(Math.abs(fallback.values[0] + .5) < .001, "Disabling follow must return head control to the model");
    console.log("PASS: gaze limits/smoothing, head fallback, missing motions, continuous typing, smooth parameter restoration, appearance preservation, pending-start/pause race, resume and input timeout.");
})().catch(error => { console.error(error); process.exitCode = 1; });
