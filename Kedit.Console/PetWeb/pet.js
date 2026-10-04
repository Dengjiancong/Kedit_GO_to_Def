/* All assets are local. Only explicit, named motions are selected. */
"use strict";
(() => {
    let app, model, manifest, contentBounds, lastMotion = -Infinity;
    const send = (type, text) => window.chrome.webview.postMessage({ type, text: String(text || "") });
    const fail = error => send("error", error && error.message || error);
    window.addEventListener("error", e => fail(e.error || e.message));
    window.addEventListener("unhandledrejection", e => { e.preventDefault(); fail(e.reason); });

    function fit() {
        if (!model) return;
        const { x, y, width, height } = contentBounds;
        if (!(width > 0 && height > 0)) throw new Error("模型尺寸无效");
        const scale = Math.min(innerWidth / width, innerHeight / height) * 0.90;
        model.scale.set(scale);
        model.x = (innerWidth - width * scale) / 2 - x * scale;
        model.y = innerHeight - (y + height) * scale - 8;
    }

    function measureContent() {
        // Measure the actual rendered alpha once. Mesh bounds can include large
        // transparent margins and hidden accessories in exported Cubism models.
        model.scale.set(1);
        const scale = Math.min(innerWidth / model.width, innerHeight / model.height) * 0.96;
        model.scale.set(scale);
        model.x = (innerWidth - model.width) / 2;
        model.y = innerHeight - model.height;
        app.renderer.render(app.stage);
        const canvas = app.renderer.extract.canvas();
        const pixels = canvas.getContext("2d").getImageData(0, 0, canvas.width, canvas.height).data;
        let minX = canvas.width, minY = canvas.height, maxX = -1, maxY = -1;
        for (let y = 0; y < canvas.height; y++) {
            for (let x = 0; x < canvas.width; x++) {
                if (pixels[(y * canvas.width + x) * 4 + 3] < 24) continue;
                minX = Math.min(minX, x); maxX = Math.max(maxX, x);
                minY = Math.min(minY, y); maxY = Math.max(maxY, y);
            }
        }
        if (maxX <= minX || maxY <= minY) throw new Error("模型没有可见像素");
        const ratio = canvas.width / innerWidth;
        return {
            x: (minX / ratio - model.x) / scale, y: (minY / ratio - model.y) / scale,
            width: (maxX - minX + 1) / ratio / scale, height: (maxY - minY + 1) / ratio / scale
        };
    }

    async function motion(name) {
        const groups = manifest.FileReferences.Motions || {};
        for (const [group, motions] of Object.entries(groups)) {
            const index = motions.findIndex(m => m.File.replace(/\\/g, "/").split("/").pop() === name);
            if (index !== -1) { await model.motion(group, index, 3); return; }
        }
    }

    async function cue(kind) {
        if (!model) return;
        const expressions = manifest.FileReferences.Expressions || [];
        if (kind === "paused" && expressions.some(e => e.Name === "emote-sad")) {
            await model.expression("emote-sad");
        } else {
            const manager = model.internalModel.motionManager.expressionManager;
            if (manager) manager.resetExpression();
            const now = performance.now();
            if (now - lastMotion > 1500) {
                lastMotion = now;
                await motion(kind === "celebrate" || kind === "resumed" ? "motion-celebrate.motion3.json" : "motion-keyboard.motion3.json");
            }
        }
    }

    window.chrome.webview.addEventListener("message", event => {
        if (event.data.type === "cue") cue(event.data.kind).catch(fail);
    });
    window.addEventListener("resize", () => { if (app) app.renderer.resize(innerWidth, innerHeight); fit(); });
    document.addEventListener("visibilitychange", () => {
        if (app) document.hidden ? app.stop() : app.start();
    });

    (async () => {
        if (!window.Live2DCubismCore || !window.PIXI || !PIXI.live2d) throw new Error("本地 Live2D 渲染组件缺失，请重新构建。");
        const url = new URLSearchParams(location.search).get("model");
        if (!url || new URL(url).origin !== "https://model.kedit.local") throw new Error("模型地址无效");
        const response = await fetch(url);
        if (!response.ok) throw new Error("读取模型失败：" + response.status);
        manifest = await response.json();
        app = new PIXI.Application({
            view: document.getElementById("pet"), width: innerWidth, height: innerHeight,
            backgroundAlpha: 0, antialias: true, resolution: Math.min(devicePixelRatio || 1, 2),
            autoDensity: true, powerPreference: "low-power"
        });
        app.ticker.maxFPS = 30;
        // Empty motion groups can contain costume changes; do not use them for idle.
        model = await PIXI.live2d.Live2DModel.from(url, { autoUpdate: false, autoInteract: false, motionPreload: "NONE", idleMotionGroup: "__kedit_no_idle_motion__" });
        model.interactive = false;
        app.stage.addChild(model);
        // Use one capped ticker for both model/physics updates and rendering.
        app.ticker.add(() => model.update(app.ticker.deltaMS));
        model.update(16);
        contentBounds = measureContent();
        fit();
        app.renderer.render(app.stage);
        send("ready", "模型已加载 · 30 FPS");
    })().catch(fail);
})();
