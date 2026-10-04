/* All assets are local. Only explicit, named motions are selected. */
"use strict";
(() => {
    let app, model, manifest, canvasBounds, interactions, lastMotion = -Infinity;
    let cueQueue = Promise.resolve();
    const params = new URLSearchParams(location.search);
    const validLimits = [30, 60, 90, 120];
    let frameLimit = validLimits.includes(Number(params.get("fps"))) ? Number(params.get("fps")) : 30;
    let renderedFrames = 0, sampleStart = performance.now(), loaded = false;
    const send = (type, text) => window.chrome.webview.postMessage({ type, text: String(text || "") });
    const post = data => window.chrome.webview.postMessage(data);
    const fail = error => send("error", error && error.message || error);
    window.addEventListener("error", e => fail(e.error || e.message));
    window.addEventListener("unhandledrejection", e => { e.preventDefault(); fail(e.reason); });

    function fit() {
        if (!model || !canvasBounds) return;
        const { x, y, width, height } = canvasBounds;
        if (!(width > 0 && height > 0)) throw new Error("模型尺寸无效");
        const scale = Math.min(innerWidth / width, innerHeight / height);
        model.scale.set(scale);
        model.x = (innerWidth - width * scale) / 2 - x * scale;
        model.y = (innerHeight - height * scale) / 2 - y * scale;
    }

    function originalCanvas() {
        // Preserve the author's entire canvas, including transparent margins.
        const internal = model.internalModel;
        const w = internal.originalWidth, h = internal.originalHeight;
        const corners = [[0, 0], [w, 0], [0, h], [w, h]]
            .map(([x, y]) => internal.localTransform.apply(new PIXI.Point(x, y)));
        const x = Math.min(...corners.map(p => p.x)), y = Math.min(...corners.map(p => p.y));
        const width = Math.max(...corners.map(p => p.x)) - x;
        const height = Math.max(...corners.map(p => p.y)) - y;
        if (![x, y, width, height].every(Number.isFinite) || width <= 0 || height <= 0)
            throw new Error("模型原始画布尺寸无效");
        return { x, y, width, height };
    }

    function resetSample() { renderedFrames = 0; sampleStart = performance.now(); }
    function setFrameLimit(limit) {
        if (!validLimits.includes(limit)) return;
        frameLimit = limit;
        if (app) app.ticker.maxFPS = limit;
        resetSample();
    }

    async function motion(name) {
        const groups = manifest.FileReferences.Motions || {};
        for (const [group, motions] of Object.entries(groups)) {
            const index = motions.findIndex(m => m.File.replace(/\\/g, "/").split("/").pop() === name);
            if (index !== -1) {
                const cached = await model.internalModel.motionManager.loadMotion(group, index);
                if (cached) cached.setIsLoop(false);
                await model.motion(group, index, 3); return;
            }
        }
    }

    async function cue(kind) {
        if (!model) return;
        if (interactions && !await interactions.beforeCue(kind)) return;
        try {
        const expressions = manifest.FileReferences.Expressions || [];
        if (kind === "paused" && expressions.some(e => e.Name === "emote-sad")) {
            await model.expression("emote-sad");
        } else {
            const manager = model.internalModel.motionManager.expressionManager;
            if (manager) manager.resetExpression();
            const now = performance.now();
            if (kind === "resumed" || now - lastMotion > 1500) {
                lastMotion = now;
                await motion(kind === "celebrate" || kind === "resumed" ? "motion-celebrate.motion3.json" : "motion-keyboard.motion3.json");
            }
        }
        } finally { if (interactions) interactions.afterCue(); }
    }

    window.chrome.webview.addEventListener("message", event => {
        if (event.data.type === "cue") cueQueue = cueQueue.then(() => cue(event.data.kind)).catch(fail);
        if (event.data.type === "settings") setFrameLimit(event.data.frameLimit);
        if (interactions && event.data.type === "interactionSettings") interactions.configure(event.data);
        if (interactions && event.data.type === "input") interactions.receive(event.data);
    });
    window.addEventListener("resize", () => { if (app) app.renderer.resize(innerWidth, innerHeight); fit(); });
    document.addEventListener("visibilitychange", () => {
        resetSample();
        if (app) document.hidden ? app.stop() : app.start();
    });
    // Count completed render calls over wall-clock time, not ticker.FPS (which can
    // describe RAF cadence rather than the throttled rendering rate).
    const statsTimer = setInterval(() => {
        if (!loaded) return;
        const elapsed = performance.now() - sampleStart;
        if (elapsed < 500) return;
        post({ type: "fps", limit: frameLimit, fps: renderedFrames * 1000 / elapsed });
        if (interactions && interactions.settings && Number.isFinite(interactions.settings.scrollRate) && Number.isFinite(interactions.settings.scrollSeconds)) post({ type: "typingMetrics", rate: interactions.rate,
            seconds: interactions.qualifiedSeconds, scrolling: interactions.cycleStart !== null,
            scrollRate: interactions.settings.scrollRate, scrollSeconds: interactions.settings.scrollSeconds });
        resetSample();
    }, 1000);
    window.addEventListener("pagehide", () => clearInterval(statsTimer));

    (async () => {
        if (!window.Live2DCubismCore || !window.PIXI || !PIXI.live2d) throw new Error("本地 Live2D 渲染组件缺失，请重新构建。");
        const url = params.get("model");
        if (!url || new URL(url).origin !== "https://model.kedit.local") throw new Error("模型地址无效");
        const response = await fetch(url);
        if (!response.ok) throw new Error("读取模型失败：" + response.status);
        manifest = await response.json();
        app = new PIXI.Application({
            view: document.getElementById("pet"), width: innerWidth, height: innerHeight,
            backgroundAlpha: 0, antialias: true, resolution: Math.min(devicePixelRatio || 1, 2),
            autoDensity: true, powerPreference: "low-power"
        });
        app.ticker.maxFPS = frameLimit;
        // Empty motion groups can contain costume changes; do not use them for idle.
        model = await PIXI.live2d.Live2DModel.from(url, { autoUpdate: false, autoInteract: false, motionPreload: "NONE", idleMotionGroup: "__kedit_no_idle_motion__" });
        model.interactive = false;
        app.stage.addChild(model);
        interactions = new PetInteractions(model, manifest, url, post);
        await interactions.ready;
        if (params.get("diagnostics") === "1") window.petDiagnostics = { model, interactions };
        // Use one capped ticker for both model/physics updates and rendering.
        app.ticker.add(() => { interactions.update(app.ticker.deltaMS); model.update(app.ticker.deltaMS); });
        model.update(16);
        canvasBounds = originalCanvas();
        fit();
        app.renderer.render(app.stage);
        app.renderer.on("postrender", () => { if (loaded) renderedFrames++; });
        loaded = true;
        resetSample();
        post({ type: "ready", canvasWidth: canvasBounds.width, canvasHeight: canvasBounds.height });
    })().catch(fail);
})();
