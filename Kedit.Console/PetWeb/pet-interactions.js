"use strict";
// Runs on the existing capped render ticker; input messages contain no key data.
class PetInteractions {
    constructor(model, manifest, url, report) {
        this.model = model; this.core = model.internalModel.coreModel;
        this.manager = model.internalModel.motionManager; this.report = report;
        this.settings = {}; this.input = { x: 0, y: 0, typing: false };
        this.x = this.y = this.gazeWeight = 0; this.dt = 16; this.lastInput = -Infinity;
        this.paused = this.busy = this.starting = this.typing = false;
        this.baseline = []; this.restore = null;
        this.eyes = [this.index("ParamEyeBallX"), this.index("ParamEyeBallY")];
        this.head = [this.index("ParamAngleX"), this.index("ParamAngleY")];
        model.internalModel.on("afterMotionUpdate", () => this.restoreFrame());
        model.internalModel.on("beforeModelUpdate", () => this.gaze());
        this.ready = this.prepare(manifest, url);
    }
    index(id) {
        const index = this.core.getParameterIndex(id);
        return index >= 0 && index < this.core.getParameterCount() ? index : -1;
    }
    async prepare(manifest, url) {
        try {
            for (const [group, entries] of Object.entries(manifest.FileReferences.Motions || {})) {
                const index = entries.findIndex(m => m.File.replace(/\\/g, "/").split("/").pop() === "motion-keyboard.motion3.json");
                if (index < 0) continue;
                const response = await fetch(new URL(entries[index].File.replace(/\\/g, "/"), url));
                if (!response.ok) throw new Error("无法读取敲键盘动作");
                const data = await response.json();
                const motion = await this.manager.loadMotion(group, index);
                if (!motion) throw new Error("无法加载敲键盘动作");
                motion.setIsLoop(true); motion.setIsLoopFadeIn(false);
                this.keyboard = { group, index, motion };
                this.indices = [...new Set(data.Curves.filter(c => c.Target === "Parameter").map(c => this.index(c.Id)).filter(i => i >= 0))];
                break;
            }
        } catch (error) { this.warning = error.message; }
        this.report({ type: "interactionCapabilities", eyes: this.eyes.some(i => i >= 0),
            head: this.head.some(i => i >= 0), typing: !!this.keyboard, warning: this.warning || "" });
    }
    configure(settings) { this.settings = settings; }
    receive(input) {
        this.input = { x: this.clamp(Number(input.x) || 0), y: this.clamp(Number(input.y) || 0), typing: input.typing === true };
        this.lastInput = performance.now();
    }
    clamp(value) { return Math.max(-1, Math.min(1, value)); }
    update(dt) {
        this.dt = Math.max(0, Math.min(100, dt));
        if (this.busy && !this.cuePending && this.manager.isFinished()) this.busy = false;
        const wanted = this.settings.typingEnabled && this.input.typing && performance.now() - this.lastInput < 750 && !this.paused && !this.busy;
        if (!wanted && this.typing) this.stopTyping(false);
        if (wanted && this.keyboard && !this.typing && !this.starting && !this.restore) this.startTyping();
    }
    async startTyping() {
        this.starting = true; this.typing = true;
        this.baseline = this.indices.map(index => ({ index, value: this.core.getParameterValueByIndex(index) }));
        this.pending = this.model.motion(this.keyboard.group, this.keyboard.index, 3);
        try {
            if (!await this.pending) this.typing = false;
            if (!this.typing || this.paused || this.busy || !this.settings.typingEnabled) {
                this.manager.stopAllMotions(); this.beginRestore();
            }
        } catch (error) {
            this.typing = false; this.manager.stopAllMotions(); this.beginRestore();
            this.keyboard = null;
            this.report({ type: "interactionWarning", text: "敲键盘动作不可用：" + error.message });
        } finally { this.starting = false; }
    }
    beginRestore() {
        this.restore = this.baseline.map(p => ({ index: p.index, value: p.value, from: this.core.getParameterValueByIndex(p.index) }));
        this.restoreTime = 0;
    }
    stopTyping(immediate) {
        if (this.typing) { this.typing = false; this.manager.stopAllMotions(); this.beginRestore(); }
        if (immediate && this.restore) { this.restoreTime = 250; this.restoreFrame(); this.core.saveParameters(); }
    }
    restoreFrame() {
        if (!this.restore) return;
        this.restoreTime += this.dt;
        const t = Math.min(1, this.restoreTime / 250), weight = t * t * (3 - 2 * t);
        for (const p of this.restore) this.core.setParameterValueByIndex(p.index, t === 1 ? p.value : p.from + (p.value - p.from) * weight);
        if (t === 1) this.restore = null;
    }
    async beforeCue(kind) {
        if (kind === "keyboard") return false; // Ordinary shortcuts only show their bubble.
        this.paused = kind === "paused" ? true : kind === "resumed" ? false : this.paused;
        this.busy = true; this.cuePending = true;
        this.stopTyping(true);
        if (this.starting) await this.pending.catch(() => {});
        this.stopTyping(true);
        return true;
    }
    afterCue() { this.cuePending = false; }
    gaze() {
        const active = this.settings.mouseFollow && !this.paused && !this.busy && !this.typing && !this.restore && performance.now() - this.lastInput < 750;
        const alpha = 1 - Math.exp(-this.dt / 120);
        this.gazeWeight += ((active ? 1 : 0) - this.gazeWeight) * alpha;
        this.x += ((active ? this.input.x : 0) - this.x) * alpha;
        this.y += ((active ? this.input.y : 0) - this.y) * alpha;
        const eyesAvailable = this.eyes.some(i => i >= 0);
        const apply = (indices, amount) => indices.forEach((index, axis) => {
            if (index < 0) return;
            const value = axis === 0 ? this.x : this.y;
            const center = this.core.getParameterDefaultValue(index);
            const range = value < 0 ? center - this.core.getParameterMinimumValue(index) : this.core.getParameterMaximumValue(index) - center;
            this.core.setParameterValueByIndex(index, center + value * range * amount, this.gazeWeight);
        });
        apply(this.eyes, .7);
        if (!eyesAvailable || this.settings.headFollow) apply(this.head, .2);
    }
}
if (typeof module !== "undefined") module.exports = PetInteractions;
