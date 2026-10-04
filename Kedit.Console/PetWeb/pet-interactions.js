"use strict";
// Anonymous key pulses drive a bounded animation, never an animation backlog.
class PetInteractions {
    constructor(model, manifest, url, report) {
        this.model = model; this.core = model.internalModel.coreModel;
        this.manager = model.internalModel.motionManager; this.report = report;
        this.settings = {}; this.input = { x: 0, y: 0 };
        this.x = this.y = this.gazeWeight = this.headWeight = 0;
        this.keyboardWeight = this.textWeight = this.poseWeight = 0;
        this.dt = 16; this.lastInput = this.lastKey = -Infinity;
        this.releaseStroke = this.lastStroke = 0;
        this.paused = this.busy = this.typing = this.fast = false;
        this.pulses = []; this.samples = []; this.strokeCount = 0;
        this.eyes = [this.index("ParamEyeBallX"), this.index("ParamEyeBallY")];
        this.head = [this.index("ParamAngleX"), this.index("ParamAngleY")];
        model.internalModel.on("beforeModelUpdate", () => this.apply());
        this.ready = this.prepare(manifest, url);
    }
    index(id) {
        const index = this.core.getParameterIndex(id);
        return index >= 0 && index < this.core.getParameterCount() ? index : -1;
    }
    async prepare(manifest, url) {
        try {
            const entries = Object.values(manifest.FileReferences.Motions || {}).flat();
            const entry = entries.find(m => m.File.replace(/\\/g, "/").split("/").pop() === "motion-keyboard.motion3.json");
            if (entry) {
                const response = await fetch(new URL(entry.File.replace(/\\/g, "/"), url));
                if (!response.ok) throw new Error("Keyboard motion could not be read");
                const data = await response.json();
                // Verified Amiya mapping; do not guess arbitrary custom parameter meanings.
                const ids = ["ParamExpression7", "ParamExpression12", "ParamExpression13", "ParamExpression14", "ParamExpression15"];
                if (!ids.every(id => this.index(id) >= 0 && data.Curves.some(c => c.Target === "Parameter" && c.Id === id)))
                    throw new Error("This model has no verified per-key animation mapping");
                this.controls = ids.map(id => this.index(id));
                if (this.core.getParameterMaximumValue(this.controls[4]) !== 60)
                    throw new Error("Unrecognized keyboard text parameter range");
                this.textDrawables = this.findTextDrawables();
                if (!this.textDrawables.size) throw new Error("Keyboard text could not be separated");
                const originalOpacity = this.core.getDrawableOpacity.bind(this.core);
                this.core.getDrawableOpacity = index => originalOpacity(index) *
                    (this.keyboardWeight > 0 && this.textDrawables.has(index) ? this.textWeight : 1);
                this.keyboard = true;
            }
        } catch (error) { this.warning = error.message; }
        this.report({ type: "interactionCapabilities", eyes: this.eyes.some(i => i >= 0),
            head: this.head.some(i => i >= 0), typing: !!this.keyboard, warning: this.warning || "" });
    }
    findTextDrawables() {
        // Probe only this model in memory; restore all author parameters afterwards.
        const c = this.core, values = Array.from({length:c.getParameterCount()}, (_,i) => c.getParameterValueByIndex(i));
        const affected = new Set();
        try {
            c.setParameterValueByIndex(this.controls[4], 0); c.update();
            const vertices = Array.from({length:c.getDrawableCount()}, (_,i) => Array.from(c.getDrawableVertices(i)));
            const opacity = vertices.map((_,i) => c.getDrawableOpacity(i));
            c.setParameterValueByIndex(this.controls[4], 30); c.update();
            vertices.forEach((v,i) => {
                const current = c.getDrawableVertices(i);
                if (Math.abs(c.getDrawableOpacity(i)-opacity[i]) > .00001 || v.some((x,j) => Math.abs(x-current[j]) > .00001)) affected.add(i);
            });
        } finally {
            values.forEach((v,i) => c.setParameterValueByIndex(i,v)); c.saveParameters(); c.update();
        }
        return affected;
    }
    configure(settings) {
        const old = this.settings;
        this.settings = { ...settings,
            followAmount: this.number(settings.followAmount, 10, 100, 45),
            followSensitivity: this.number(settings.followSensitivity, .5, 4, 2),
            followSpeed: this.number(settings.followSpeed, .5, 3, 1.5) };
        if (!settings.typingEnabled || old.typingScope !== settings.typingScope) this.cancel();
    }
    number(value, min, max, fallback) { return Number.isFinite(value) ? Math.max(min, Math.min(max, value)) : fallback; }
    receive(input) {
        const now = performance.now();
        this.input = { x: this.number(input.x, -1, 1, 0), y: this.number(input.y, -1, 1, 0) };
        this.lastInput = now;
        if (input.reset) { this.cancel(); return; }
        const stale = Number.isFinite(input.sentAt) && Math.abs(Date.now()-input.sentAt) > 250;
        const count = stale ? 0 : Math.floor(this.number(input.presses, 0, 32, 0));
        if (!count || !this.keyboard || !this.settings.typingEnabled || this.paused || this.busy) return;
        this.lastKey = now;
        this.samples.push({ time: now, count });
        this.trim(now);
        // Events received within one frame merge; overlapping pulses never restart an existing stroke.
        this.pulses.push(now); this.pulses = this.pulses.slice(-8); this.strokeCount++;
    }
    trim(now) {
        this.samples = this.samples.filter(s => now-s.time < 1000);
        this.pulses = this.pulses.filter(t => now-t < 180);
    }
    cancel() {
        this.releaseStroke = this.lastStroke;
        this.lastKey = -Infinity; this.samples = []; this.pulses = []; this.fast = false; this.typing = false;
    }
    update(dt) {
        this.dt = Math.max(0, Math.min(100, dt));
        this.releaseStroke = this.approach(this.releaseStroke, 0, 120);
        const now = performance.now();
        if (this.busy && !this.cuePending && this.manager.isFinished()) this.busy = false;
        this.trim(now);
        if (now-this.lastInput > 750 && this.pulses.length) this.pulses = [];
        const permitted = this.keyboard && this.settings.typingEnabled && !this.paused && !this.busy;
        this.typing = !!permitted && this.pulses.length > 0;
        const rate = this.samples.reduce((sum,s) => sum+s.count, 0);
        if (!permitted || now-this.lastKey > 220 || rate < 3) this.fast = false;
        else if (rate >= 5) this.fast = true;
        const retain = permitted && now-this.lastKey < 60000;
        this.keyboardWeight = this.approach(this.keyboardWeight, retain ? 1 : 0, retain ? 160 : 250);
        this.textWeight = this.approach(this.textWeight, this.fast ? 1 : 0, this.fast ? 160 : 200);
        this.poseWeight = this.approach(this.poseWeight, this.typing ? 1 : 0, 120);
    }
    approach(value, target, duration) {
        return value < target ? Math.min(target, value+this.dt/duration) : Math.max(target, value-this.dt/duration);
    }
    async beforeCue(kind) {
        if (kind === "keyboard") return false;
        this.paused = kind === "paused" ? true : kind === "resumed" ? false : this.paused;
        this.busy = true; this.cuePending = true; this.cancel();
        return true;
    }
    afterCue() { this.cuePending = false; }
    apply() {
        const now = performance.now(), c = this.core;
        if (this.keyboard && this.keyboardWeight > 0) {
            const pulse = this.pulses.reduce((peak,t) => Math.max(peak, Math.sin(Math.PI*Math.min(1,(now-t)/180)) ** 2), this.releaseStroke);
            this.lastStroke = pulse;
            const values = [1, 1-pulse, 0, 0, 30];
            this.controls.forEach((index,i) => c.setParameterValueByIndex(index, values[i], this.keyboardWeight));
            // Retain the keyboard, but release the typing head pose as soon as the last stroke ends.
            this.head.forEach((index,i) => { if (index >= 0) c.setParameterValueByIndex(index, i === 0 ? -10 : -5, this.poseWeight*this.keyboardWeight); });
        }
        const active = this.settings.mouseFollow && !this.paused && !this.busy && !this.typing && now-this.lastInput < 750;
        const alpha = 1-Math.exp(-this.dt/(120/(this.settings.followSpeed || 1.5)));
        this.gazeWeight += ((active ? 1 : 0)-this.gazeWeight)*alpha;
        const eyesAvailable = this.eyes.some(i => i >= 0);
        this.headWeight += ((active && (!eyesAvailable || this.settings.headFollow) ? 1 : 0)-this.headWeight)*alpha;
        this.x += ((active ? this.number(this.input.x*this.settings.followSensitivity,-1,1,0) : 0)-this.x)*alpha;
        this.y += ((active ? this.number(this.input.y*this.settings.followSensitivity,-1,1,0) : 0)-this.y)*alpha;
        const apply = (indices, amount, weight) => indices.forEach((index,axis) => {
            if (index < 0 || weight < .00001) return;
            const value = axis === 0 ? this.x : this.y, center = c.getParameterDefaultValue(index);
            const range = value < 0 ? center-c.getParameterMinimumValue(index) : c.getParameterMaximumValue(index)-center;
            c.setParameterValueByIndex(index, center+value*range*amount, weight);
        });
        apply(this.eyes, this.settings.followAmount/100, this.gazeWeight);
        apply(this.head, this.settings.followAmount/100, this.headWeight);
    }
}
if (typeof module !== "undefined") module.exports = PetInteractions;
