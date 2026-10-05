"use strict";
const assert=require("node:assert/strict"), Sword=require("../Kedit.Console/PetWeb/pet-sword.js");
let now=1000;Object.defineProperty(global,"performance",{value:{now:()=>now},configurable:true});
const settings={swordSensitivity:1.5};
function run(fps,speed){const s=new Sword();s.update(0,true,settings);let peak=0;for(let t=0;t<1800;t+=1000/fps){now+=1000/fps;s.receive({mouseVX:t<200?speed:0,mouseVY:0});s.update(1000/fps,true,settings);peak=Math.max(peak,Math.abs(s.position));assert.ok(Math.abs(s.position)<=1);}assert.ok(Math.abs(s.position)<.001);return peak;}
const fast=run(120,2),slow=run(120,.1);assert.ok(fast>slow*3);assert.ok(Math.abs(run(30,2)-fast)<.08);
const s=new Sword();s.update(0,true,settings);s.receive({mouseVX:-100,mouseVY:0});for(let i=0;i<10;i++){now+=16;s.update(16,true,settings);}assert.ok(s.position<0);assert.ok(s.offset(100)>0&&s.offset(100)<=.2);
const previous=s.followHead(0,30,16,25);s.receive({mouseVX:2,mouseVY:0});assert.ok(s.followHead(0,-30,16,25)<previous,"Fast swipe must not freeze head");
const slower=new Sword(),faster=new Sword();assert.ok(faster.followHead(0,30,100,25,3)>slower.followHead(0,30,100,25,.5));
function headAt(fps){const h=new Sword();for(let i=0;i<fps;i++)h.followHead(0,30,1000/fps,25,1);return h.head[0];}
assert.ok(Math.abs(headAt(30)-headAt(120))<.00001);
now+=300;for(let i=0;i<40;i++)s.followHead(0,30,100,25);assert.ok(s.head[0]>7.4&&s.head[0]<=7.5);
for(let i=0;i<50;i++)s.followHead(0,30,100,100);assert.ok(s.head[0]>29.9);
for(let i=0;i<50;i++)s.followHead(0,30,100,0);assert.ok(s.head[0]<.001);
s.update(500,true,settings);assert.equal(s.position,0);s.receive({mouseVX:4,mouseVY:0});s.update(16,false,settings);assert.equal(s.position,0);assert.equal(s.drive,0);
s.update(0,true,settings);s.receive({mouseVX:4,mouseVY:0,sentAt:Date.now()-1000});assert.equal(s.drive,0);
console.log("PASS: directional bounded sword, settling, continuous head during fast swipes, adjustable head speed, 30/120 FPS consistency and state cleanup.");
