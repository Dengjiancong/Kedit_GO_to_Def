"use strict";
// Velocity-driven, bounded spring. Units are 1000 desktop DIPs/second, not pet-relative position.
class PetSword {
    constructor() { this.reset(); }
    reset() { this.position=0; this.velocity=0; this.drive=0; this.lastSample=-Infinity; this.head=[0,0,0,0]; this.active=false; }
    receive(data) {
        if (!this.active || !Number.isFinite(data.mouseVX) || !Number.isFinite(data.mouseVY) ||
            (Number.isFinite(data.sentAt) && Math.abs(Date.now()-data.sentAt)>250)) return;
        this.drive=Math.max(-4,Math.min(4,data.mouseVX + .18*data.mouseVY));
        this.lastSample=performance.now();
    }
    update(dt,active,settings) {
        if (!active) { this.reset(); return; }
        if (!this.active) this.reset();
        this.active=true;
        if (!Number.isFinite(dt) || dt>250) { this.reset(); this.active=true; return; }
        const age=performance.now()-this.lastSample;
        const sensitivity=Number.isFinite(settings.swordSensitivity) ? Math.max(.5,Math.min(3,settings.swordSensitivity)) : 1.5;
        const target=Math.max(-1,Math.min(1,this.drive*sensitivity))*Math.exp(-Math.max(0,age-50)/65);
        // Integrate in small steps so 30 and 120 FPS have essentially the same response.
        let remaining=Math.max(0,dt)/1000;
        while(remaining>0) {
            const h=Math.min(remaining,1/240); remaining-=h;
            this.velocity+=(196*(target-this.position)-20*this.velocity)*h;
            this.position+=this.velocity*h;
            if(Math.abs(this.position)>1) { this.position=Math.sign(this.position); if(this.position*this.velocity>0)this.velocity=0; }
        }
        if(age>1000 && Math.abs(this.position)<.0001 && Math.abs(this.velocity)<.001) this.position=this.velocity=0;
    }
    followHead(axis,target,dt,amount,speed=1) {
        const factor=Number.isFinite(amount) ? Math.max(0,Math.min(100,amount))/100 : .25;
        const rate=Number.isFinite(speed)?Math.max(.5,Math.min(3,speed)):1;
        this.head[axis]+=(target*factor-this.head[axis])*(1-Math.exp(-Math.max(0,Math.min(100,dt))/(180/rate)));
        return this.head[axis];
    }
    offset(amount) {
        const fraction=Number.isFinite(amount)?Math.max(0,Math.min(100,amount))/100:.65;
        // Author's arm-turn range is 0..1 with resting sword at .8: leave room on both sides.
        // Increasing this author's arm parameter moves the blade LEFT on screen.
        return -this.position*(this.position>0?.65:.2)*fraction;
    }
}
if(typeof module!=="undefined")module.exports=PetSword;
