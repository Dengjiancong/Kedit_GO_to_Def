"use strict";
// Exercise the actual page script with controlled time and renderer completions.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

(async () => {
    let now = 0, app, timer, browserMessage, interactions;
    const messages = [], events = {};
    const model = {
        internalModel: {
            originalWidth: 1000, originalHeight: 500,
            localTransform: { apply: p => ({ x: p.x + 100, y: p.y - 50 }) },
            motionManager: { expressionManager: { resetExpression() {} } }
        },
        scale: { value: 1, set(value) { this.value = value; } },
        update() {}, motion: async () => {}, expression: async () => {}
    };
    class Application {
        constructor() {
            app = this;
            this.stage = { addChild() {} };
            this.ticker = { maxFPS: 0, deltaMS: 16, FPS: 999, add() {} };
            this.renderer = {
                resize() {}, render() {},
                on(event, callback) { events[event] = callback; }
            };
        }
        start() {} stop() {}
    }
    const sandbox = {
        URL, URLSearchParams, console, innerWidth: 400, innerHeight: 400, devicePixelRatio: 1,
        performance: { now: () => now },
        PetGesture: require("../Kedit.Console/PetWeb/pet-gesture.js"),
        PetInteractions: class { constructor() { interactions = this; this.settings = {}; this.ready = Promise.resolve(); } configure() {} receive() {} update() {} },
        PetCompanion: class { constructor() { this.ready=Promise.resolve(); } update() {} },
        location: { search: "?model=https%3A%2F%2Fmodel.kedit.local%2Ftest.model3.json&fps=30" },
        fetch: async () => ({ ok: true, json: async () => ({ FileReferences: {} }) }),
        document: { hidden: false, getElementById() {}, addEventListener() {} },
        setInterval: callback => { timer = callback; return 1; }, clearInterval() {},
        PIXI: { Application, Point: class { constructor(x, y) { this.x = x; this.y = y; } },
            live2d: { Live2DModel: { from: async () => model } } }
    };
    sandbox.window = { Live2DCubismCore: {}, PIXI: sandbox.PIXI,
        addEventListener: (name, fn) => { events[name] = fn; },
        chrome: { webview: { postMessage: data => messages.push(data), addEventListener: (_, fn) => { browserMessage = fn; } } } };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, "../Kedit.Console/PetWeb/pet.js"), "utf8"), sandbox);
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(messages.some(m => m.type === "error"), false);
    const ready = messages.find(m => m.type === "ready");
    assert.equal(ready.canvasWidth, 1000); assert.equal(ready.canvasHeight, 500);
    assert.equal(model.scale.value, 0.4);
    // The full translated author canvas spans x=0..400, y=100..300.
    assert.equal(model.x, -40); assert.equal(model.y, 120);
    sandbox.innerWidth = 800; sandbox.innerHeight = 400; events.resize();
    assert.equal(model.scale.value, 0.8); assert.equal(model.x, -80); assert.equal(model.y, 40);
    for (let i = 0; i < 30; i++) events.postrender();
    now = 1000; timer();
    assert.equal(messages.at(-1).fps, 30); // Not ticker.FPS=999.
    browserMessage({ data: { type: "settings", frameLimit: 120 } });
    assert.equal(app.ticker.maxFPS, 120);
    for (let i = 0; i < 114; i++) events.postrender();
    now = 2000; timer(); assert.equal(messages.at(-1).fps, 114); assert.equal(messages.at(-1).limit, 120);
    browserMessage({ data: { type: "settings", frameLimit: 999 } });
    assert.equal(app.ticker.maxFPS, 120);
    now = 3000; timer(); assert.equal(messages.at(-1).fps, 0); // Stopped renders cannot show old FPS.
    assert.equal(messages.some(m => m.type === "typingMetrics"), false, "Do not send incomplete typing settings during startup");
    interactions.settings = { scrollRate:600, scrollSeconds:5 }; interactions.rate = 720; interactions.qualifiedSeconds = 3;
    interactions.cycleStart = null; now = 4000; timer();
    assert.equal(messages.at(-1).type, "typingMetrics"); assert.equal(messages.at(-1).rate, 720);
    assert.equal(messages.at(-1).seconds, 3); assert.equal(messages.at(-1).scrollRate, 600); assert.equal(messages.at(-1).scrolling, false);
    console.log("PASS: full author canvas, translated layout, resize, completed-render FPS, live limit changes, invalid limit and stalled-render reporting.");
})().catch(error => { console.error(error); process.exitCode = 1; });
