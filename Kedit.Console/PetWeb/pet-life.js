"use strict";
// Optional companion features: finite gestures, isolated appearance and automatic music.
class PetLife {
    constructor(c) { this.c=c;this.rubbing=false;this.music={enabled:false,peak:0,at:-Infinity};this.energy=0;this.qualified=0;this.lastSound=-Infinity;this.auto=null;this.wardrobe=null; }
    rub(active,seconds=0) {
        const c=this.c;
        if(active===this.rubbing)return;
        this.rubbing=active;this.rubUntil=seconds?performance.now()+seconds*1000:Infinity;
        if(active) {
            c.layers.filter(l=>l.touch).forEach(l=>l.ending=true);
            for(const id of ["emote-shy","mark-flower"]) {
                if(c.select(id,true,true))c.layers[c.layers.length-1].rub=true;
            }
            c.say("pat");
        } else c.layers.filter(l=>l.rub).forEach(l=>l.ending=true);
    }
    manualAppearance() {
        if(!this.wardrobe)return;
        this.wardrobe=null;this.c.report({type:"wardrobeManual"});
    }
    prepare(d) {
        const c=this.c,r=c.resources.find(r=>r.id===d.action&&r.available);
        if(!r)return;
        const values={},original={};
        for(const v of r.values.filter(v=>v.category==="外观")) {
            values[v.id]=v.value(r.duration/1000);
            original[v.id]=this.wardrobe && this.wardrobe.original[v.id]!==undefined ? this.wardrobe.original[v.id] : this.appearance && this.appearance[v.id]!==undefined ? this.appearance[v.id] : c.core.getParameterValueByIndex(v.index);
        }
        c.report({type:"wardrobePrepared",token:d.token,values,original});
    }
    wardrobeSet(d) {
        const c=this.c;
        c.layers.filter(l=>/cloth off/.test(l.r.id)).forEach(l=>l.ending=true);
        if(!d.state) { this.wardrobe=null; return; }
        this.wardrobe={...d.state,start:performance.now(),from:{}};
        for(const id of Object.keys(d.state.values||{})) { const i=c.input.index(id); if(i>=0)this.wardrobe.from[id]=c.core.getParameterValueByIndex(i); }
        if(d.play) {
            // Keep the author's costume animation and text, but never claim the hands.
            const r=c.resources.find(r=>r.id==="motion-cloth off"&&r.available);
            if(r)c.layers.push({r,values:r.values.filter(v=>v.category!=="外观"),scheduled:true,hold:false,touch:false,hand:false,start:performance.now(),weight:0,ending:false});
            this.wardrobe.animated=true;
        }
    }
    receive(d) {
        if(d.type==="music")this.music={...d,at:performance.now()};
        if(d.type==="wardrobePrepare")this.prepare(d);
        if(d.type==="wardrobe")this.wardrobeSet(d);
    }
    typing() {
        if(!this.auto)return;
        this.auto.ending=true;this.auto=null;
        this.c.input.manualBusy=this.c.layers.some(l=>l.hand&&!l.automatic&&!l.ending);
    }
    update(dt) {
        const c=this.c,now=performance.now();
        if(this.rubbing && (now>=this.rubUntil||c.input.paused))this.rub(false);
        const m=this.music,live=m.enabled && now-m.at<2000 && !c.input.paused;
        const peak=live?Math.max(0,Math.min(1,m.peak||0)):0;
        const elapsed=Math.max(0,Math.min(100,dt));
        this.energy+=(Math.min(1,Math.sqrt(peak)*(m.sensitivity||1.5))-this.energy)*(1-Math.exp(-elapsed/(peak>this.energy?100:400)));
        if(peak>.001) { this.lastSound=now;this.qualified+=elapsed; }
        else if(now-this.lastSound>800)this.qualified=0;
        const others=c.layers.some(l=>l.hand&&!l.automatic&&!l.ending);
        const eligible=live&&!others&&now-c.lastActivity>3000;
        if(others)this.savedKey=-Infinity;
        if(this.auto && (!eligible||now-this.lastSound>8000)) {
            this.auto.ending=true;this.auto=null;c.input.manualBusy=others;
            if(!others&&!c.input.paused&&now-this.savedKey<60000)c.input.lastKey=Math.max(c.input.lastKey,this.savedKey);
        }
        if(!live) { this.qualified=0;this.lastSound=-Infinity; }
        if(!this.auto && eligible&&this.qualified>=3000) {
            this.savedKey=Math.max(c.input.lastKey||-Infinity,this.savedKey||-Infinity);
            if(c.select("motion-music",true)) { this.auto=c.layers[c.layers.length-1];this.auto.automatic=true; }
        }
    }
    apply() {
        const w=this.wardrobe,c=this.c;
        if(!w){this.captureAppearance();return;}
        const elapsed=performance.now()-w.start,r=c.resources.find(r=>r.id==="motion-cloth off");
        for(const id of Object.keys(w.values||{})) {
            const i=c.input.index(id);if(i<0)continue;
            let value=w.values[id],blend=Math.min(1,elapsed/400);
            if(w.animated && r && elapsed<r.duration) {
                const v=r.values.find(v=>v.id===id);if(v)value=v.value(elapsed/1000);
                blend=Math.min(1,elapsed/150);
            }
            c.core.setParameterValueByIndex(i,(w.from[id]||0)*(1-blend)+value*blend);
        }
        this.captureAppearance();
    }
    captureAppearance() {
        const c=this.c;this.appearance={};
        for(const id of ["ParamExpression8","ParamExpression10","ParamExpression11","ParamExpression43"]) {
            const i=c.input.index(id);if(i>=0)this.appearance[id]=c.core.getParameterValueByIndex(i);
        }
    }
}
if(typeof module!=="undefined")module.exports=PetLife;
