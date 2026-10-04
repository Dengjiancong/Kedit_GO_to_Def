"use strict";
const assert = require("node:assert/strict");
const PetInteractions = require("../Kedit.Console/PetWeb/pet-interactions.js");
let now = 0;
global.performance = { now: () => now };
const defaults = { mouseFollow:true, headFollow:true, typingEnabled:true, typingScope:"editors", followAmount:45, followSensitivity:2, followSpeed:1.5 };
async function fixture(eyes = true, keyboard = true) {
    const ids = ["ParamAngleX","ParamAngleY","ParamExpression7","ParamExpression12","ParamExpression13","ParamExpression14","ParamExpression15","Clothes", ...(eyes ? ["ParamEyeBallX","ParamEyeBallY"] : [])];
    const values = ids.map(() => 0); values[3]=.2; values[7]=.8;
    const base = values.slice(), events = {}, messages = [];
    const core = {
        getParameterIndex: id => ids.indexOf(id), getParameterCount: () => ids.length,
        getParameterValueByIndex: i => values[i], setParameterValueByIndex: (i,v,w=1) => { values[i] = values[i]*(1-w)+v*w; },
        getParameterDefaultValue: i => base[i], getParameterMinimumValue: i => i<2 || i>=8 ? -1 : 0,
        getParameterMaximumValue: i => i===6 ? 60 : 1,
        getDrawableCount: () => 2, getDrawableVertices: i => [i===0 ? values[6] : 1,0], getDrawableOpacity: () => 1,
        saveParameters() {}, update() {}
    };
    const manager = { finished:true, isFinished() { return this.finished; } };
    const model = { internalModel:{ coreModel:core, motionManager:manager, on:(n,fn)=>{events[n]=fn;} } };
    global.fetch = async () => ({ok:true,json:async()=>({Curves:ids.slice(2,7).map(Id=>({Target:"Parameter",Id}))})});
    const subject = new PetInteractions(model,{FileReferences:{Motions:keyboard ? {"": [{File:"motion-keyboard.motion3.json"}]} : {}}},"https://model.kedit.local/test.model3.json",m=>messages.push(m));
    await subject.ready; subject.configure(defaults);
    const press = (count=1) => subject.receive({x:1,y:-1,presses:count});
    const frame = (dt=1000/60, heartbeat=true) => {
        now+=dt;
        if(heartbeat) subject.receive({x:1,y:-1,presses:0});
        subject.update(dt); base.forEach((v,i)=>{values[i]=v;}); events.beforeModelUpdate();
    };
    const advance = (ms, fps=60, heartbeat=true) => { let left=ms; while(left>.00001) {const dt=Math.min(left,1000/fps);frame(dt,heartbeat);left-=dt;} };
    return {subject,values,core,press,advance,frame,messages,manager};
}
(async()=>{
    const f=await fixture(), s=f.subject;
    assert.equal(f.messages[0].typing,true); assert.equal(s.textDrawables.size,1);
    s.receive({presses:1,sentAt:Date.now()-1000});f.advance(50);assert.equal(s.strokeCount,0,"Delayed bridge messages must not replay input");
    f.advance(700); assert(f.values[0]>0 && f.values[8]>0 && f.values[9]<0);
    f.press(); f.advance(90); assert.equal(s.typing,true); const mid=f.values[3];
    f.advance(150); assert.equal(s.typing,false); assert.equal(s.strokeCount,1);
    assert.equal(s.keyboardWeight,1); assert.equal(f.values[3],1); assert(mid<1);
    assert.equal(s.textWeight,0); assert.equal(f.core.getDrawableOpacity(0),0); assert.equal(f.core.getDrawableOpacity(1),1);
    f.advance(900); assert(f.values[0]>0,"Head must follow while keyboard is retained");
    for(let i=0;i<4;i++){f.press();f.advance(400);assert.equal(s.textWeight,0,"Slow input must not reveal text");}
    for(let i=0;i<8;i++){f.press();f.advance(110);}
    assert(s.textWeight>.5,"Fast input must reveal text");
    const strokes=s.strokeCount; f.advance(250); assert(s.textWeight>0 && s.textWeight<1,"Text should fade instead of vanish");
    assert(f.core.getDrawableOpacity(0)>0 && f.core.getDrawableOpacity(0)<1);assert.equal(f.core.getDrawableOpacity(1),1);
    f.advance(250); assert.equal(s.typing,false); assert.equal(s.textWeight,0); assert.equal(s.strokeCount,strokes);
    assert.equal(s.keyboardWeight,1); assert.equal(f.values[7],.8,"Appearance must not be reset");
    now=s.lastKey+59990; f.frame(1); assert.equal(s.keyboardWeight,1);
    f.press(); f.advance(200); assert.equal(s.keyboardWeight,1,"A new key must renew retention");
    now=s.lastKey+60001; f.advance(300); assert.equal(s.keyboardWeight,0); assert.equal(f.values[2],0); assert.equal(f.values[3],.2);
    f.press(); f.advance(50); await s.beforeCue("paused");s.afterCue();f.press();f.advance(400);
    assert.equal(s.typing,false); assert.equal(s.keyboardWeight,0);
    await s.beforeCue("resumed");s.afterCue();f.advance(100);assert.equal(s.typing,false,"No replay of keys received during pause");
    f.press();f.advance(40);assert.equal(s.typing,true);
    s.configure({...defaults,typingScope:"all"});f.advance(300);assert.equal(s.keyboardWeight,0,"Scope changes clear prior work");
    f.press();f.advance(100);s.configure({...defaults,typingScope:"all",typingEnabled:false});f.advance(300);assert.equal(s.keyboardWeight,0);
    s.configure({...defaults,followAmount:999,followSensitivity:Infinity,followSpeed:-1});
    assert.equal(s.settings.followAmount,100);assert.equal(s.settings.followSensitivity,2);assert.equal(s.settings.followSpeed,.5);
    s.configure({...defaults,headFollow:false});f.advance(1000);assert(Math.abs(f.values[0])<.001);
    s.configure({...defaults,mouseFollow:false});f.advance(1000);assert(Math.abs(f.values[8])<.001);
    const fallback=await fixture(false,false);fallback.advance(700);assert(fallback.values[0]>0);fallback.press();fallback.advance(100);assert.equal(fallback.subject.typing,false);
    for(const fps of [30,60,90,120]){
        const g=await fixture();g.press();g.advance(250,fps);assert.equal(g.subject.typing,false);assert.equal(g.subject.keyboardWeight,1);
        g.advance(800,fps,false);assert.equal(g.subject.typing,false);
        now=g.subject.lastKey+60001;g.advance(300,fps);assert.equal(g.subject.keyboardWeight,0);
    }
    console.log("PASS: one key/one stroke, slow/fast text separation and opacity, no backlog, 60s renewal/expiry, retained-keyboard gaze, defaults/limits, pause/resume/scope/disable cleanup, appearance preservation, missing mapping and 30/60/90/120 timing.");
})().catch(error=>{console.error(error);process.exitCode=1;});
