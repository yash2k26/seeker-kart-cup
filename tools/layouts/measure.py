#!/usr/bin/env python3
"""Measures the shipped (formula) circuits the way TrackSetup.Sample builds them: lap, radius percentiles."""
import math, numpy as np
def cr(p0,p1,p2,p3,t):
    return 0.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t**3)
def formula(radius, ampA, harmA, ampB, harmB, cp):
    ctrl=[]
    for i in range(cp):
        t=i/cp*2*math.pi; r=radius*(1+ampA*math.sin(harmA*t)+ampB*math.cos(harmB*t))
        ctrl.append(np.array([math.sin(t)*r, math.cos(t)*r]))
    per=max(8,384//cp); pts=[]
    for i in range(cp*per):
        s=i//per; t=(i%per)/per
        pts.append(cr(ctrl[(s-1)%cp],ctrl[s],ctrl[(s+1)%cp],ctrl[(s+2)%cp],t))
    return np.array(pts)
def radii(c, k=4):
    n=len(c); out=[]
    for i in range(n):
        a,b,d=c[(i-k)%n],c[i],c[(i+k)%n]
        ab=np.linalg.norm(b-a); bd=np.linalg.norm(d-b); ad=np.linalg.norm(d-a)
        cross=abs((b-a)[0]*(d-a)[1]-(b-a)[1]*(d-a)[0])
        out.append(1e4 if cross<1e-6 else ab*bd*ad/(2*cross))
    return np.array(out)
if __name__=="__main__":
    for name,args in [("beach",(215,.10,2,.06,3,12)),("alpine",(160,.34,2,.06,5,16)),("forest",(118,.30,5,.12,3,20))]:
        c=formula(*args); L=sum(np.linalg.norm(c[(i+1)%len(c)]-c[i]) for i in range(len(c)))
        r=radii(c)
        print(f"{name}: lap {L:.0f} m, ds {L/len(c):.2f}, min radius {r.min():.1f}, p5 {np.percentile(r,5):.1f}, p25 {np.percentile(r,25):.1f}")
