/* All assets are local. Only explicit, named motions are selected. */
"use strict";
(() => {
    let app, model, manifest, canvasBounds, interactions, companion, gesture;
    let cueQueue = Promise.resolve();
    const params = new URLSearchParams(location.search);
    const validLimits = [30, 60, 90, 120];
    let frameLimit = validLimits.includes(Number(params.get("fps"))) ? Number(params.get("fps")) : 30;
    let renderedFrames = 0, sampleStart = performance.now(), loaded = false, hitTesting = false;
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

    async function cue(kind) {
        if (!companion) return;
        if (kind === "paused" || kind === "resumed") {
            interactions.paused = kind === "paused"; interactions.cancel(); companion.speech(0);
        }
        if (interactions.manualBusy || kind === "keyboard") return;
        if (kind === "paused") companion.select("emote-sad",false,true);
        else if (kind === "resumed" || kind === "celebrate") companion.select("motion-celebrate",false);
    }

    window.chrome.webview.addEventListener("message", event => {
        if (event.data.type === "cue") cueQueue = cueQueue.then(() => cue(event.data.kind)).catch(fail);
        if (event.data.type === "settings") setFrameLimit(event.data.frameLimit);
        if (interactions && event.data.type === "interactionSettings") interactions.configure(event.data);
        if (companion && event.data.type === "input") companion.activity(event.data);
        if (interactions && event.data.type === "input") interactions.receive(event.data);
        if (companion && event.data.type === "companionSettings") companion.configure(event.data);
        if(gesture && event.data.type==="gesture")gesture.receive(event.data);
        if(companion && companion.life)companion.life.receive(event.data);
        if (companion && event.data.type === "speech") companion.speech(event.data.duration);
        if (companion && event.data.type === "touch") touchAt(event.data.x*innerWidth,event.data.y*innerHeight);
        if(companion && event.data.type==="requestActionMenu" && hitRegion(event.data.x*innerWidth,event.data.y*innerHeight)) {
            const hand=companion.layers.filter(l=>l.hand&&l.hold&&!l.ending).pop();
            post({type:"actionMenu",request:event.data.request,selected:hand?hand.r.id:""});
        }
        if (companion && event.data.type === "companion") {
            const d=event.data;
            if (d.action === "head" || d.action === "body") companion.touch(d.action);
            else if (d.action === "rub") companion.life.rub(true,3);
            else if (d.action === "speechPreview") companion.say("encourage");
            else if (d.action === "reset") companion.reset();
            else if (d.action === "select") companion.select(d.id,d.hold);
            else if (d.action === "save") post({type:"combination",ids:companion.combination()});
            else if (d.action === "restore") companion.restore(d.ids);
            else if (["drink","eat","weapon"].includes(d.action)) {
                const id={drink:"hand-pot",eat:"hand-rice",weapon:"motion-weapon"}[d.action];
                if(companion.select(id,true))companion.say(d.action);
            }
        }
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

    function hitRegion(x,y) {
        if (!model || x<0 || y<0 || x>=innerWidth || y>=innerHeight) return false;
        // Read only on an intentional click. Actual rendered alpha excludes blank mesh triangles,
        // masks and invisible props; the normal frame loop does not read back the GPU.
        const texture=PIXI.RenderTexture.create({width:innerWidth,height:innerHeight,resolution:1});
        let visible=false, headVisible=false;
        const core=model.internalModel.coreModel, opacity=core.getDrawableOpacity;
        hitTesting=true;
        try {
            app.renderer.render(app.stage,{renderTexture:texture,clear:true});
            const pixels=app.renderer.extract.pixels(texture);
            visible=pixels[(Math.floor(y)*Math.ceil(innerWidth)+Math.floor(x))*4+3]>16;
            if(visible && companion.headDrawables.size) {
                core.getDrawableOpacity=i=>companion.headDrawables.has(i) ? opacity.call(core,i) : 0;
                app.renderer.render(app.stage,{renderTexture:texture,clear:true});
                const headPixels=app.renderer.extract.pixels(texture);
                headVisible=headPixels[(Math.floor(y)*Math.ceil(innerWidth)+Math.floor(x))*4+3]>16;
            }
        } finally { core.getDrawableOpacity=opacity; texture.destroy(true); hitTesting=false; }
        if(!visible)return false;
        if(companion.headDrawables.size)return headVisible ? "head":"body";
        const point=model.toModelPosition(new PIXI.Point(x,y)), head=companion.headBounds();
        return head && point.x>=head.x && point.x<=head.x+head.width && point.y>=head.y && point.y<=head.y+head.height ? "head":"body";
    }
    function touchAt(x,y) {
        const region=hitRegion(x,y);
        return region ? companion.touch(region) : false;
    }

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
        companion = new PetCompanion(model,interactions,manifest,url,post);
        await companion.ready;
        gesture=new PetGesture((x,y)=>hitRegion(x*innerWidth,y*innerHeight),kind=>companion.touch(kind),active=>companion.life.rub(active));
        if (params.get("diagnostics") === "1") window.petDiagnostics = { model, interactions, companion, app, touchAt, hitRegion, gesture };
        // Use one capped ticker for both model/physics updates and rendering.
        app.ticker.add(() => { companion.update(app.ticker.deltaMS); interactions.update(app.ticker.deltaMS); model.update(app.ticker.deltaMS); });
        model.update(16);
        canvasBounds = originalCanvas();
        fit();
        app.renderer.render(app.stage);
        app.renderer.on("postrender", () => { if (loaded && !hitTesting) renderedFrames++; });
        loaded = true;
        resetSample();
        post({ type: "ready", canvasWidth: canvasBounds.width, canvasHeight: canvasBounds.height });
    })().catch(fail);
})();
