"use strict";
// Transient layers run after gaze/typing and never save over the author's base parameters.
class PetCompanion {
    constructor(model, interactions, manifest, url, report) {
        this.model = model; this.core = model.internalModel.coreModel; this.input = interactions; this.report = report;
        this.resources = []; this.layers = []; this.settings = {}; this.hits = []; this.recent = [];
        this.lastTouch = this.lastActivity = this.lastCare = -Infinity;
        this.activeSince = null; this.returnPending = false; this.speechUntil = 0; this.mouth = 0;
        this.contextTimes = {}; this.clock = performance.now();
        this.sword = new PetSword();
        model.internalModel.on("beforeModelUpdate", () => this.apply());
        this.ready = this.prepare(manifest, url);
    }
    static get names() { return {
        "cloth off":"换装", "emote-angry":"小小生气", "emote-sad":"难过", "emote-shy":"害羞",
        "emote-shy2":"害羞变体二", "emote-shy3":"害羞变体三", "face-sad":"委屈脸", "face-shock":"惊讶脸",
        "hand-rice":"端饭碗", "hand-trumpt":"拿喇叭", "hand-pot":"拿热水壶", "mouth-hungry":"馋嘴",
        "mark-bang":"感叹号", "mark-exceting":"兴奋标记", "mark-flower":"小花", "mark-music":"音符",
        "mark-sweat":"汗滴", "mark-shock":"惊讶标记", "motion-celebrate":"庆祝", "motion-cloth off":"丧失戰衣（换装动画）",
        "motion-keyboard":"原版打字", "motion-music":"打碟", "motion-weapon":"拿剑" }; }
    static category(id) {
        if (["ParamExpression8","ParamExpression10","ParamExpression11","ParamExpression43"].includes(id)) return "外观";
        if (/Mouth/.test(id) || ["Param10","Param11"].includes(id)) return "嘴部";
        if (/^ParamExpression(01|2[0189]|30)$/.test(id) || /Eye/.test(id)) return "面部";
        if (/^ParamExpression(3[1-6])$/.test(id)) return "附加标记";
        if (/Angle/.test(id) || id === "Param13") return "姿势";
        return "手部／道具";
    }
    async prepare(manifest, url) {
        const refs = manifest.FileReferences;
        const entries = (refs.Expressions || []).map(e => ({ id:e.Name, file:e.File, motion:false }));
        for (const group of Object.values(refs.Motions || {})) for (const m of group)
            entries.push({ id:m.File.replace(/\\/g,"/").split("/").pop().replace(/\.motion3\.json$/, ""), file:m.File, motion:true });
        for (const entry of entries) {
            const resource = { ...entry, label:PetCompanion.names[entry.id] || entry.id, channels:[], values:[], available:false };
            try {
                const response = await fetch(new URL(entry.file.replace(/\\/g,"/"), url));
                if (!response.ok) throw new Error("无法读取资源");
                const data = await response.json();
                resource.duration = entry.motion ? data.Meta.Duration * 1000 : 3500;
                if (!Number.isFinite(resource.duration) || resource.duration <= 0) throw new Error("动作时长无效");
                for (const item of entry.motion ? data.Curves : data.Parameters) {
                    if (entry.motion && item.Target !== "Parameter") continue;
                    const index = this.input.index(item.Id);
                    if (index < 0) throw new Error("缺少参数 " + item.Id);
                    const category = PetCompanion.category(item.Id);
                    resource.channels.push(category);
                    resource.values.push({ id:item.Id, index, category, blend:entry.motion ? "Overwrite" : item.Blend || "Add",
                        value:entry.motion ? PetInteractions.motionCurve(item.Segments) : () => item.Value });
                }
                resource.channels = [...new Set(resource.channels)];
                resource.available = resource.values.length > 0;
            } catch (error) { resource.error = error.message; }
            this.resources.push(resource);
        }
        this.eyeDrawables = this.probeEyes();
        this.headDrawables = this.findHeadDrawables();
        this.report({ type:"catalog", items:this.resources.map(r => ({ id:r.id, label:r.label, available:r.available,
            detail:(r.motion ? "动作 · " : "表情 · ") + r.channels.join("、") + (r.error ? " · " + r.error : ""),
            parameters:r.values.map(v => v.id).join(", ") })) });
    }
    probeEyes() {
        const c = this.core, index = this.input.index("ParamEyeLOpen");
        if (index < 0) return [];
        const saved = Array.from({length:c.getParameterCount()}, (_,i) => c.getParameterValueByIndex(i));
        try {
            c.setParameterValueByIndex(index,1); c.update();
            const before = Array.from({length:c.getDrawableCount()}, (_,i) => Array.from(c.getDrawableVertices(i)));
            c.setParameterValueByIndex(index,0); c.update();
            return before.map((v,i) => v.some((x,j) => Math.abs(x-c.getDrawableVertices(i)[j]) > .00001) ? i : -1).filter(i => i >= 0);
        } finally { saved.forEach((v,i) => c.setParameterValueByIndex(i,v)); c.saveParameters(); c.update(); }
    }
    findHeadDrawables() {
        const raw=this.core._model, result=new Set();
        if (!raw || !raw.parts || !raw.drawables.parentPartIndices) return result;
        for(let i=0;i<raw.drawables.count;i++) {
            let part=raw.drawables.parentPartIndices[i], depth=0;
            while(part>=0 && depth++<raw.parts.count) {
                // In this model "face" is an ancestor of almost the whole character;
                // "hair2" contains lower-body fur. Never treat those roots as the head.
                if(/^(hair|eye|mouth|ear)$/i.test(raw.parts.ids[part])) { result.add(i);break; }
                if(/^(body|arm\d*|cloth|hair2|keyboard|eat|celebrate|mark|face\d*)$/i.test(raw.parts.ids[part])) break;
                part=raw.parts.parentIndices[part];
            }
        }
        // Amiya's cheek/base meshes are direct children of Part9 (also the body's parent).
        // These IDs were checked against the rendered model; use IDs, never mutable indices.
        if(this.input.keyboard && raw.parts.ids.includes("Part9")) {
            for(const id of ["ArtMesh135","ArtMesh136","ArtMesh140","ArtMesh141","ArtMesh142","ArtMesh155"]) {
                const i=Array.from(raw.drawables.ids).indexOf(id); if(i>=0)result.add(i);
            }
        }
        return result;
    }
    // Use current deformed eye geometry, not fractions of the full transparent canvas.
    headBounds() {
        const vertices = this.eyeDrawables.flatMap(i => Array.from(this.model.internalModel.getDrawableVertices(i)));
        if (!vertices.length) return null;
        const xs = vertices.filter((_,i) => i%2===0), ys = vertices.filter((_,i) => i%2===1);
        const x = (Math.min(...xs)+Math.max(...xs))/2, y = (Math.min(...ys)+Math.max(...ys))/2;
        const w = Math.max(...xs)-Math.min(...xs);
        // One eye's deformation includes lashes: use its width as a head-relative scale.
        return { x:x-w*2.2, y:y-w*3.1, width:w*4.4, height:w*4.1 };
    }
    configure(settings) {
        this.settings = settings;
        if (!settings.careEnabled || Date.now() < settings.quietUntil) this.returnPending = false;
        if (this.welcomed) return;
        this.welcomed = true;
        if (this.canCare()) this.say("welcome", true);
    }
    activity(data) {
        this.sword.receive(data);
        if (!(data.presses > 0) || data.reset || (data.sentAt && Math.abs(Date.now()-data.sentAt)>250)) return;
        const now = performance.now();
        if (now-this.lastActivity > 300000) { this.returnPending = Number.isFinite(this.lastActivity); this.activeSince = now; }
        if (this.activeSince === null) this.activeSince = now;
        this.lastActivity = now;
    }
    canCare() { return this.settings.careEnabled && Date.now() >= (this.settings.quietUntil || 0) && !this.input.paused; }
    say(context, automatic = false) {
        const now = performance.now(), bank = PetCompanion.phrases[context] || PetCompanion.phrases.encourage;
        if (automatic && (!this.canCare() || now-this.lastCare < (this.settings.careMinutes || 20)*60000 ||
            now-(this.contextTimes[context] || -Infinity) < 1800000)) return false;
        const available = bank.filter(text => !this.recent.includes(text));
        const text = (available.length ? available : bank)[Math.floor(Math.random()*(available.length || bank.length))];
        this.recent.push(text); this.recent = this.recent.slice(-8);
        this.lastCare = now; this.contextTimes[context] = now;
        this.report({type:"bubble", text, duration:Math.max(3500, Math.min(7000, text.length*180))});
        return true;
    }
    speech(duration) { this.speechUntil = performance.now()+Math.max(0,Math.min(10000,duration)); }
    touch(kind) {
        const now = performance.now();
        if (now-this.lastTouch < 350) return false;
        this.lastTouchKind=kind;
        this.lastTouch = now; this.hits = this.hits.filter(t => now-t < 4000); this.hits.push(now);
        const count = this.hits.length;
        const ids = count >= 5 ? ["emote-angry","mark-sweat"] : count >= 3 ? ["mark-shock","mark-sweat"] :
            kind === "head" ? ["emote-shy","mark-flower"] : ["emote-shy3"];
        this.layers.filter(l => l.touch).forEach(l => l.ending = true);
        ids.filter(Boolean).forEach(id => { if(this.resources.some(r=>r.id===id&&r.available))this.select(id, false, true); });
        this.say(count >= 5 ? "protest" : count >= 3 ? "surprise" : kind === "head" ? "pat" : "poke");
        return true;
    }
    select(id, hold, touch = false) {
        const r = this.resources.find(r => r.id === id && r.available);
        if (!r) { this.report({type:"companionStatus",text:"模型中没有可用的这项资源。"}); return false; }
        // All hand resources are exclusive. Face/marks can combine when their actual parameters don't overlap.
        const hand = r.channels.includes("手部／道具"), appearance = /cloth off/.test(r.id);
        const values = r.values.filter(v => appearance || v.category !== "外观");
        const layer = {r,values,hold:!!hold,touch,hand,start:performance.now(),weight:0,ending:false};
        for (const old of this.layers) if (!touch && (hand && old.hand || old.values.some(v => values.some(n => n.id === v.id)))) old.ending = true;
        this.layers.push(layer);
        if (hand) { this.input.manualBusy = true; this.input.cancel(); }
        this.status(); return true;
    }
    reset() { this.layers.forEach(l => l.ending = true); this.speech(0); this.hits=[]; this.status(); }
    combination() { return this.layers.filter(l => l.hold && !l.ending && !l.touch).map(l => l.r.id); }
    restore(ids) { this.reset(); (Array.isArray(ids) ? ids.slice(0,this.resources.length) : []).forEach(id => this.select(id,true)); }
    status() { this.report({type:"companionStatus",text:this.layers.filter(l=>!l.ending).map(l=>l.r.label+(l.hold?"（保持）":"")).join("＋") || "自然状态"}); }
    update(dt) {
        const now = performance.now(); this.dt = Math.max(0,Math.min(100,dt));
        this.sword.update(dt,this.layers.some(l=>l.r.id==="motion-weapon"&&!l.ending&&now-l.start>=l.r.duration) &&
            !!this.input.settings.mouseFollow && !this.input.paused,this.settings);
        let changed = false;
        for (const l of this.layers) {
            if (!l.hold && now-l.start >= Math.max(3000,l.r.duration)) l.ending=true;
            l.weight = Math.max(0,Math.min(1,l.weight+(l.ending?-1:1)*this.dt/250));
        }
        this.layers = this.layers.filter(l => { const keep = !l.ending || l.weight>0; if(!keep)changed=true; return keep; });
        const busy = this.layers.some(l => l.hand);
        if (this.input.manualBusy && !busy) this.input.cancel();
        this.input.manualBusy = busy;
        if(changed)this.status();
        const pause = now-this.lastActivity;
        if (pause >= 2000 && pause < 10000 && !busy && now-this.lastTouch>10000) {
            if (this.returnPending) { this.say("return",true); this.returnPending=false; }
            else if (this.activeSince !== null && now-this.activeSince > 1800000 && this.settings.restEnabled) {
                if (this.say("rest",true)) this.activeSince=now;
            } else if (this.activeSince !== null && now-this.activeSince > 600000) this.say("encourage",true);
        }
        if (pause>=10000) this.returnPending=false; // Expire; never replay old notifications.
        this.clock=now;
    }
    apply() {
        const c = this.core, now=performance.now(); let fixedMouth=false;
        // Hand override suppresses fading automatic keyboard immediately, including its text.
        if(this.input.manualBusy && this.input.controls) c.setParameterValueByIndex(this.input.controls[0],0);
        for (const l of this.layers) {
            const elapsed = Math.max(0,now-l.start);
            const loop = l.hold && ["motion-music","motion-keyboard"].includes(l.r.id);
            const time = (loop ? elapsed%l.r.duration : Math.min(elapsed,l.r.duration))/1000;
            for (const v of l.values) {
                const weaponFollow = l.r.id === "motion-weapon" && this.input.settings.mouseFollow && !this.input.paused;
                const release = weaponFollow ? Math.max(0,Math.min(1,(elapsed-l.r.duration)/250)) : 0;
                // Finish the draw-sword gesture, then stop pinning the authored head angles to zero.
                // Gaze has already run in PetInteractions before this transient layer.
                const weight = l.weight;
                const base=c.getParameterValueByIndex(v.index);
                let value=v.value(time);
                if (weaponFollow && /^ParamAngle[XY]$/.test(v.id)) {
                    const axis=v.id==="ParamAngleX"?0:1;
                    const head=this.sword.followHead(axis,this.swordGazeTarget(v.index,axis,this.input.headWeight),this.dt,this.settings.swordHeadAmount,this.settings.swordHeadSpeed);
                    value=value*(1-release)+head*release;
                }
                if (weaponFollow && v.id === "ParamExpression9") value += this.sword.offset(this.settings.swordAmount)*release;
                const target=v.blend==="Add" ? base+value : v.blend==="Multiply" ? base*value : value;
                c.setParameterValueByIndex(v.index,target,weight);
                if(v.category==="嘴部" && l.weight>.01)fixedMouth=true;
            }
        }
        const index=this.input.index("ParamMouthOpenY");
        if(this.sword.active) (this.input.eyes || []).forEach((i,axis)=>{
            if(i>=0)c.setParameterValueByIndex(i,this.sword.followHead(axis+2,this.swordGazeTarget(i,axis,this.input.gazeWeight),this.dt,this.settings.swordHeadAmount,this.settings.swordHeadSpeed));
        });
        const talking=now<this.speechUntil && !fixedMouth;
        const amount=Number.isFinite(this.settings.mouthAmount) ? Math.max(10,Math.min(100,this.settings.mouthAmount))/100 : .75;
        const target=talking ? amount*(.08+.92*(.5+.5*Math.sin(now/125)))*(.85+.15*Math.cos(now/397)) : 0;
        this.mouth += (target-this.mouth)*(1-Math.exp(-(this.dt||16)/(talking?55:90)));
        if(index>=0 && !fixedMouth) c.setParameterValueByIndex(index,this.mouth);
    }
    swordGazeTarget(index,axis,weight) {
        const c=this.core, settings=this.input.settings;
        const sensitivity=Number.isFinite(this.settings.swordHeadSensitivity)?Math.max(.5,Math.min(4,this.settings.swordHeadSensitivity)):1;
        const position=this.input.input[axis===0?"x":"y"];
        const value=Math.max(-1,Math.min(1,position*settings.followSensitivity*sensitivity));
        const center=c.getParameterDefaultValue(index);
        const range=value<0?center-c.getParameterMinimumValue(index):c.getParameterMaximumValue(index)-center;
        return center+value*range*settings.followAmount/100*weight;
    }
    static get phrases() { return {
        welcome:["今天也一起慢慢来吧。","很高兴陪着你，按自己的节奏来就好。","我在这里，陪你开始新的一段。"],
        return:["回来啦，按你的节奏继续就好。","欢迎回来，我们慢慢来。","又见面啦，先找个舒服的姿势吧。"],
        encourage:["认真忙了一会儿，想歇一下也可以呀。","一步一步来，给自己留一点余地。","不用急着做完所有事，我陪着你。","也记得照顾一下认真忙碌的自己。"],
        rest:["辛苦啦，休息一下吧。","要不要喝口水，舒展一下肩膀？","可以看看远处，让眼睛也歇一会儿。"],
        focusEnd:["这一段专注结束啦，辛苦了！","计时结束啦，给自己一点休息时间吧。"],
        pat:["收到摸摸，给你也送一朵小花。","谢谢你的摸摸，今天也陪着你。","小花送给你，希望这一刻轻松一点。"],
        poke:["我在呢，有收到你的招呼。","收到，给你一个小小的回应。","在这里陪着你呀。"],
        surprise:["哎呀，有点痒啦。","收到好多招呼啦，让我缓一缓。"],
        protest:["好啦好啦，先让我歇一小下嘛。","有点痒，轻一点点就好啦。"],
        drink:["陪你喝口水，慢慢来。","水壶准备好啦，也照顾一下自己吧。"],
        eat:["吃点东西，给自己补充一点能量吧。","饭碗端好啦，愿你也好好吃饭。"],
        weapon:["我来陪你迎接接下来的挑战。","剑准备好啦，我们一步一步来。"]
    }; }
}
if (typeof module !== "undefined") module.exports=PetCompanion;
