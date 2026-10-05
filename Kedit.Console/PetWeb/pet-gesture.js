"use strict";
// Coordinates are desktop DIPs; alpha/head hit testing stays in the renderer.
class PetGesture {
    constructor(hit,touch,rub) { this.hit=hit; this.touch=touch; this.rub=rub; this.state=null; }
    receive(d) {
        const now=performance.now();
        if(d.phase==="cancel") { this.cancel(); return; }
        if(d.phase==="down") {
            this.cancel(); const region=this.hit(d.x,d.y);
            if(region)this.state={region,x:d.px,y:d.py,start:now,moved:false,rubbing:false};
            return;
        }
        const s=this.state;if(!s)return;
        if(Math.hypot(d.px-s.x,d.py-s.y)>6)s.moved=true;
        const region=this.hit(d.x,d.y);
        if(region!==s.region) { this.cancel();return; }
        if(d.phase==="move" && s.region==="head" && s.moved && now-s.start>=180) {
            if(!s.rubbing) { s.rubbing=true;this.rub(true); }
        }
        if(d.phase==="up") {
            if(!s.rubbing && !s.moved && now-s.start<=500)this.touch(s.region);
            this.cancel();
        }
    }
    cancel() { if(this.state && this.state.rubbing)this.rub(false);this.state=null; }
}
if(typeof module!=="undefined")module.exports=PetGesture;
