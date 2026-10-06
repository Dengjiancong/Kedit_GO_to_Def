"use strict";
// Coordinates are desktop DIPs; alpha/head hit testing stays in the renderer.
class PetGesture {
    constructor(hit,touch,rub,cursor=()=>{},trace=()=>{}) { this.hit=hit; this.touch=touch; this.rub=rub; this.cursor=cursor; this.trace=trace; this.state=null; }
    receive(d) {
        const now=performance.now();
        if(d.phase==="cancel") { this.cancel("native-cancel"); return; }
        if(d.phase==="down") {
            this.cancel(); const region=this.hit(d.x,d.y);
            this.trace("down hit="+region);
            if(region){this.state={region,x:d.px,y:d.py,start:now,moved:false,rubbing:false};this.cursor(region==="head");}
            return;
        }
        const s=this.state;if(!s)return;
        if(!s.moved && Math.hypot(d.px-s.x,d.py-s.y)>6){s.moved=true;this.trace("movement-threshold elapsedMs="+Math.round(now-s.start));}
        const region=this.hit(d.x,d.y);
        if(region!==s.region) { this.cancel("region-changed "+s.region+" -> "+region);return; }
        if(d.phase==="move" && s.region==="head" && s.moved && now-s.start>=180) {
            if(!s.rubbing) { s.rubbing=true;this.trace("rub-start elapsedMs="+Math.round(now-s.start));this.rub(true); }
        }
        if(d.phase==="up") {
            this.trace("up elapsedMs="+Math.round(now-s.start)+" moved="+s.moved+" rubbing="+s.rubbing);
            if(!s.rubbing && !s.moved && now-s.start<=500)this.touch(s.region);
            this.cancel();
        }
    }
    cancel(reason="reset-or-release") { if(this.state)this.trace("cancel reason="+reason+" rubbing="+this.state.rubbing);if(this.state && this.state.rubbing)this.rub(false);this.state=null;this.cursor(false); }
}
if(typeof module!=="undefined")module.exports=PetGesture;
