"use strict";
const assert=require("assert");let now=0;global.performance={now:()=>now};
const Gesture=require("../Kedit.Console/PetWeb/pet-gesture.js"),Life=require("../Kedit.Console/PetWeb/pet-life.js");
let touches=[],rubs=[];const gesture=new Gesture((x,y)=>x<0?null:y<.5?"head":"body",k=>touches.push(k),a=>rubs.push(a));
function send(phase,x=0,y=.2,px=0){gesture.receive({phase,x,y,px,py:0});}
send("down");now=100;send("up");assert.deepEqual(touches,["head"]);
send("down");now+=200;send("move",0,.2,9);send("up",0,.2,9);assert.deepEqual(rubs,[true,false]);assert.equal(touches.length,1);
send("down");now+=200;send("move",0,.2,9);send("move",0,.8,10);send("up",0,.2);assert.deepEqual(rubs,[true,false,true,false]);
send("down",0,.8);now+=200;send("move",0,.8,10);send("up",0,.8,10);assert.equal(touches.length,1);
send("down",-1);now+=200;send("move",0,.2,10);assert.equal(gesture.state,null);
send("down");now+=200;send("move",0,.2,9);send("cancel");send("up");assert.equal(touches.length,1);
const report=[],c={layers:[],lastActivity:-Infinity,input:{paused:false,manualBusy:false,lastKey:-Infinity,index:()=>0},resources:[],core:{getParameterValueByIndex:()=>.2,setParameterValueByIndex:(i,v)=>{c.value=v;}},report:d=>report.push(d),say:()=>{},select(id,hold,touch){this.layers.push({r:{id},hand:id==="motion-music",ending:false,touch});this.input.manualBusy=id==="motion-music";if(this.input.manualBusy)this.input.lastKey=-Infinity;return true;}};
const life=new Life(c);c.life=life;
function step(ms,peak=.1,enabled=true){for(let i=0;i<ms;i+=20){now+=20;life.receive({type:"music",enabled,peak,amount:70,sensitivity:1.5});life.update(20);}}
step(2800);assert(!life.auto);step(300);assert(life.auto);const low=life.energy;
step(1000,.9);assert(life.energy>low);step(4000,0);assert(life.auto,"short silence should retain deck");step(4200,0);assert(!life.auto);
step(3200);assert(life.auto);c.lastActivity=now;life.typing();c.input.lastKey=now;const keyTime=now;assert(!life.auto&&!c.input.manualBusy,"first keystroke must get keyboard immediately");step(2900);assert(!life.auto);step(200);assert(life.auto);
step(8500,0);assert(!life.auto);assert.equal(c.input.lastKey,keyTime,"music stop must restore unexpired keyboard retention");step(3200);assert(life.auto);
c.layers.push({hand:true,ending:false,r:{id:"manual"}});step(100);assert(!life.auto);step(3200);assert(!life.auto,"manual prop priority");
c.layers=[];step(3200);assert(life.auto);step(100,.1,false);assert(!life.auto);assert.equal(life.qualified,0);
life.rub(true);assert(life.rubbing);assert(c.layers.some(l=>l.rub));life.rub(false);assert(c.layers.filter(l=>l.rub).every(l=>l.ending));
life.rub(true,3);now+=3100;life.update(20);assert(!life.rubbing);
const resource={id:"motion-cloth off",available:true,duration:2400,values:[{id:"coat",index:0,category:"外观",value:t=>Math.min(1,t)},{id:"bg",index:1,category:"手部／道具",value:()=>1}]};c.resources=[resource];
life.prepare({token:"one",action:resource.id});assert.equal(report.at(-1).original.coat,.2);assert.equal(report.at(-1).values.coat,1);
life.wardrobeSet({state:{values:{coat:1},original:{coat:.2}},play:true});assert(c.layers.some(l=>l.scheduled&&!l.hand&&l.values.some(v=>v.id==="bg")));
now+=3000;life.apply();assert.equal(c.value,1);life.prepare({token:"two",action:resource.id});assert.equal(report.at(-1).original.coat,.2);
life.wardrobeSet({state:{values:{coat:.2},original:{coat:.2}},play:false});now+=500;life.apply();assert.equal(c.value,.2);
life.manualAppearance();assert.equal(life.wardrobe,null);assert.equal(report.at(-1).type,"wardrobeManual");
console.log("PASS: tap/rub/cancel/outside/body separation, music qualification/energy/silence/typing/manual priority/disable, appearance snapshot/restoration/manual override.");
