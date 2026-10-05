"use strict";
const assert=require("node:assert/strict");
const Companion=require("../Kedit.Console/PetWeb/pet-companion.js");
global.PetSword=require("../Kedit.Console/PetWeb/pet-sword.js");
global.PetInteractions=require("../Kedit.Console/PetWeb/pet-interactions.js");
let now=100;
Object.defineProperty(global,"performance",{value:{now:()=>now},configurable:true});
const ids=["ParamExpression28","ParamExpression29","ParamExpression31","ParamExpression32","ParamExpression35",
    "ParamExpression26","ParamExpression24","ParamExpression23","ParamExpression11","ParamMouthOpenY","Param10","Param11","ParamExpression7"];
const values=ids.map(()=>0), reports=[];
const core={getParameterIndex:id=>ids.indexOf(id),getParameterCount:()=>ids.length,
    getParameterValueByIndex:i=>values[i],setParameterValueByIndex:(i,v,w=1)=>{values[i]+=(v-values[i])*w;}};
const input={index:id=>ids.indexOf(id),cancel(){this.pulses=[];},pulses:[1],controls:[12]};
const definitions={
    "emote-shy":{"ParamExpression28":1},"emote-shy2":{"ParamExpression28":-1},"emote-shy3":{"ParamExpression28":-2},
    "emote-angry":{"ParamExpression29":1},"mark-flower":{"ParamExpression31":1},"mark-shock":{"ParamExpression32":1},
    "mark-sweat":{"ParamExpression35":1},"hand-pot":{"ParamExpression26":1,"ParamExpression24":.85},
    "hand-rice":{"ParamExpression23":1,"ParamExpression11":0,"Param10":-30,"Param11":30},
    "cloth off":{"ParamExpression11":1},"missing":{"NotAvailable":1}};
global.fetch=async url=>({ok:true,json:async()=>({Parameters:Object.entries(definitions[decodeURIComponent(new URL(url).pathname.slice(1))]).map(([Id,Value])=>({Id,Value,Blend:"Add"}))})});
(async()=>{
    const c=new Companion({internalModel:{coreModel:core,on(){}}},input,{FileReferences:{Expressions:Object.keys(definitions).map(Name=>({Name,File:Name}))}},"https://model.kedit.local/model.json",m=>reports.push(m));
    await c.ready;
    assert.equal(Companion.names["motion-cloth off"],"丧失戰衣（换装动画）");
    const gaze={core:{getParameterDefaultValue:()=>0,getParameterMinimumValue:()=>-30,getParameterMaximumValue:()=>30},
        settings:{swordHeadSensitivity:.5},input:{input:{x:.1,y:-.1},settings:{followSensitivity:2,followAmount:45}}};
    const low=Companion.prototype.swordGazeTarget.call(gaze,0,0,1);
    gaze.settings.swordHeadSensitivity=4;
    assert.ok(Companion.prototype.swordGazeTarget.call(gaze,0,0,1)>low*7);
    gaze.input.input.x=1;assert.equal(Companion.prototype.swordGazeTarget.call(gaze,0,0,1),13.5);
    assert.equal(c.resources.find(r=>r.id==="missing").available,false);
    assert.equal(Companion.category("ParamExpression21"),"面部");
    c.configure({careEnabled:false,careMinutes:20});
    assert.equal(reports.filter(r=>r.type==="bubble").length,0);
    c.touch("head"); assert.equal(c.hits.length,1);
    assert.ok(c.layers.some(l=>!l.ending&&l.r.id==="emote-shy"));
    now+=400;c.touch("body");
    assert.ok(c.layers.some(l=>!l.ending&&l.r.id==="emote-shy3"));
    assert.ok(!c.layers.some(l=>!l.ending&&l.r.id==="emote-shy2"));
    c.touch("body"); assert.equal(c.hits.length,2,"350ms cooldown shared by buttons and body");
    for(let i=0;i<4;i++){now+=400;c.touch(i%2?"head":"body");c.update(100);}
    assert.ok(c.layers.some(l=>!l.ending&&l.r.id==="emote-angry"));
    now+=4500;for(let i=0;i<4;i++)c.update(100);
    assert.equal(c.layers.length,0,"Negative feedback must expire");
    c.select("cloth off",true);c.select("hand-pot",true);c.select("emote-shy",true);
    for(let i=0;i<3;i++)c.update(100);values.fill(0);c.apply();
    assert.equal(values[ids.indexOf("ParamExpression11")],1,"Prop must preserve chosen appearance");
    assert.equal(input.manualBusy,true);assert.equal(input.pulses.length,0);
    assert.equal(c.combination().length,3);
    c.select("hand-rice",true);for(let i=0;i<3;i++)c.update(100);
    assert.ok(!c.combination().includes("hand-pot"));
    assert.ok(c.combination().includes("cloth off"));
    c.speech(3000);values.fill(0);c.apply();assert.equal(c.mouth,0,"Fixed mouth wins over speech");
    const saved=c.combination();c.reset();for(let i=0;i<3;i++)c.update(100);
    assert.equal(input.manualBusy,false);assert.equal(c.layers.length,0);
    c.speech(3000);c.update(100);c.apply();assert.ok(c.mouth>.05);
    c.speech(0);for(let i=0;i<10;i++){now+=100;c.update(100);c.apply();}assert.ok(c.mouth<.001);
    c.restore(saved);assert.deepEqual(c.combination(),saved);
    c.reset();for(let i=0;i<3;i++)c.update(100);
    c.configure({careEnabled:true,careMinutes:20,quietUntil:Date.now()+60000});
    assert.equal(c.say("rest",true),false,"Quiet blocks automatic messages");
    assert.equal(c.say("pat"),true,"Quiet does not block manual interaction");
    c.configure({careEnabled:true,careMinutes:20,quietUntil:0});
    assert.equal(c.say("return",true),false,"Manual response also starts global care cooldown");
    now+=1200001;assert.equal(c.say("return",true),true);
    now+=1200001;assert.equal(c.say("return",true),false,"Context cooldown independent of global cooldown");
    now+=600001;assert.equal(c.say("return",true),true);
    const bubbles=reports.filter(r=>r.type==="bubble");assert.notEqual(bubbles.at(-1).text,bubbles.at(-2).text);
    c.lastCare=-Infinity;c.contextTimes={};c.activeSince=now-700000;c.lastActivity=now;
    c.update(100);const before=reports.length;
    now+=2500;c.update(100);assert.equal(reports.length,before+1,"Encouragement waits for typing pause");
    c.lastCare=-Infinity;c.contextTimes={};c.returnPending=true;c.lastActivity=now-15000;c.activeSince=now;
    c.update(100);assert.equal(c.returnPending,false,"Expired return message discarded");
    console.log("PASS: resource isolation, shared touch cooldown/escalation/recovery, prop conflicts and appearance preservation, favorite combinations, fixed-mouth priority, speech settling, quiet/manual independence, global/context cooldown, deduplication and pause/expiry scheduling.");
})().catch(e=>{console.error(e);process.exitCode=1;});
