"use strict";
// Optional companion features: finite gestures, isolated appearance and automatic music.
class PetLife {
    constructor(c) { this.c=c;this.rubbing=false;this.music={enabled:false,peak:0,at:-Infinity};this.energy=0;this.qualified=0;this.lastSound=-Infinity;this.auto=null;this.wardrobe=null; }
    rub(active,seconds=0) {
        const c=this.c;
        if(active===this.rubbing)return;
        this.rubbing=active;this.rubUntil=seconds?performance.now()+seconds*1000:Infinity;
        if(active) {
            this.rubStarted=performance.now();this.rubNextTalk=this.rubStarted+10000;
            this.rubVisits=(this.rubVisits||[]).filter(t=>this.rubStarted-t<20000);
            this.rubVisits.push(this.rubStarted);this.rubTroubled=this.rubVisits.length>=5;
            c.layers.filter(l=>l.touch).forEach(l=>l.ending=true);
            this.rubExpression();c.say(this.rubTroubled?"protest":"rubStart");
        } else {
            c.layers.filter(l=>l.rub).forEach(l=>l.ending=true);
            if(!c.input.paused && performance.now()-this.rubStarted>=1800 && performance.now()-(this.rubLastEnd||-Infinity)>12000) {
                c.say("rubEnd");this.rubLastEnd=performance.now();
            }
        }
    }
    rubExpression() {
        const c=this.c;
        for(const id of ["emote-shy",this.rubTroubled?"mark-sweat":"mark-flower"]) {
            const existing=c.layers.find(l=>l.rub&&l.r.id===id);
            if(existing){existing.ending=false;continue;}
            if(c.resources.some(r=>r.id===id&&r.available)&&c.select(id,true,true))c.layers[c.layers.length-1].rub=true;
        }
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
        if(d.type==="previewReminder")this.preview={r:this.c.resources.find(r=>r.id===d.id&&r.available),start:performance.now()};
        if(d.type==="resumeMusic") {
            this.c.layers.filter(l=>l.hand).forEach(l=>l.ending=true);this.auto=null;
            this.c.lastActivity=-Infinity;this.c.status();
        }
    }
    typing() {
        if(!this.auto)return;
        this.auto.ending=true;this.auto=null;
        this.c.input.manualBusy=this.c.layers.some(l=>l.hand&&!l.automatic&&!l.ending);
    }
    update(dt) {
        const c=this.c,now=performance.now();
        if(this.rubbing && (now>=this.rubUntil||c.input.paused))this.rub(false);
        if(this.rubbing && now>=this.rubNextTalk){c.say(this.rubTroubled?"protest":"rubContinue");this.rubNextTalk=now+12000;}
        this.rubWeight=(this.rubWeight||0)+((this.rubbing?1:0)-(this.rubWeight||0))*(1-Math.exp(-Math.max(0,Math.min(100,dt))/180));
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
            if(c.select("motion-music",true)) { this.auto=c.layers[c.layers.length-1];this.auto.automatic=true;if(c.status)c.status(); }
        }
        const state=!m.enabled?"自动打碟已关闭":c.input.paused?"快捷键已暂停":!live?"等待音量信号":others?"手动道具优先，点击“恢复自动打碟”释放手部":now-c.lastActivity<=3000?"打字优先，停手约 3 秒后恢复":this.auto?"正在自动打碟":peak>.001?"检测到声音，持续发声约 3 秒后进入（"+(this.qualified/1000).toFixed(1)+" 秒）":"已连接，等待播放器发声";
        if(state!==this.lastMusicState && now-(this.lastMusicReport||-Infinity)>300){this.lastMusicReport=now;this.lastMusicState=state;c.report({type:"musicBehavior",text:state});}
    }
    apply() {
        const w=this.wardrobe,c=this.c;
        if(this.rubbing || this.rubWeight>.001) {
            const weight=this.rubWeight;
            // Visually verified: (20=0,21=1) is > <; (20=1,21=1) is troubled.
            // Select branch 20 directly so happy rubs never drift into troubled eyes.
            const eye=c.input.index("ParamExpression20");if(eye>=0)c.core.setParameterValueByIndex(eye,this.rubTroubled?1:0);
            const shape=c.input.index("ParamExpression21");if(shape>=0){const value=c.core.getParameterValueByIndex(shape);c.core.setParameterValueByIndex(shape,value+(1-value)*(weight||0));}
            const z=c.input.index("ParamAngleZ");if(z>=0)c.core.setParameterValueByIndex(z,c.core.getParameterValueByIndex(z)+Math.sin(performance.now()/600)*2.5*weight);
        }
        if(!w){this.captureAppearance();this.applyPreview();return;}
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
        this.applyPreview();
    }
    applyPreview() {
        const p=this.preview,c=this.c;if(!p||!p.r)return;
        const age=performance.now()-p.start;if(age>=6000){this.preview=null;return;}
        const weight=Math.min(1,age/250,(6000-age)/400),t=Math.min(age,p.r.duration)/1000;
        for(const v of p.r.values) {
            if(v.category==="外观"&&!/cloth off/.test(p.r.id))continue;
            const base=c.core.getParameterValueByIndex(v.index),value=v.value(t);
            c.core.setParameterValueByIndex(v.index,v.blend==="Add"?base+value:v.blend==="Multiply"?base*value:value,weight);
        }
    }
    captureAppearance() {
        const c=this.c;this.appearance={};
        for(const id of ["ParamExpression8","ParamExpression10","ParamExpression11","ParamExpression43"]) {
            const i=c.input.index(id);if(i>=0)this.appearance[id]=c.core.getParameterValueByIndex(i);
        }
    }
}
if(typeof module!=="undefined")module.exports=PetLife;
