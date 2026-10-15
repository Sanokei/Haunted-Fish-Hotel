import re
class Doc:
    def __init__(s,ty,fid,hdr,body,stripped): s.ty,s.fid,s.hdr,s.body,s.stripped=ty,fid,hdr,body,stripped
    def get(s,key,default=None):
        m=re.search(r'^  '+re.escape(key)+r': (.*)$',s.body,re.M); return m.group(1) if m else default
    def ref(s,key):
        m=re.search(r'^  '+re.escape(key)+r': \{fileID: (-?\d+)',s.body,re.M); return m.group(1) if m else None
    @property
    def name(s):
        m=re.search(r'^  m_Name: (.*)$',s.body,re.M); return m.group(1) if m else None
class File:
    def __init__(s,path):
        s.path=path; t=open(path,encoding='utf-8').read(); s.text=t
        parts=re.split(r'(?m)^(--- !u!\d+ &-?\d+[^\n]*)\n',t)
        s.head=parts[0]; s.docs={}; s.order=[]
        for i in range(1,len(parts),2):
            m=re.match(r'--- !u!(\d+) &(-?\d+)( stripped)?',parts[i])
            d=Doc(m.group(1),m.group(2),parts[i],parts[i+1],bool(m.group(3))); s.docs[d.fid]=d; s.order.append(d.fid)
    def go_of(s,fid): return s.docs[s.docs[fid].ref('m_GameObject')]
    def comps(s,goid): return [c for c in re.findall(r'component: \{fileID: (-?\d+)\}',s.docs[goid].body)]
    def children(s,tid):
        b=s.docs[tid].body
        if 'm_Children:' not in b: return []
        return re.findall(r'\{fileID: (-?\d+)\}',b.split('m_Children:')[1].split('m_Father')[0])
    def tree(s,tid,ind=0,out=None,maxd=12):
        d=s.docs[tid]
        if d.ty!='4': return
        go=s.docs.get(d.ref('m_GameObject'))
        nm=go.name if go and go.name else '<stripped>'
        cs=[s.docs[c].ty for c in s.comps(go.fid)] if go and not go.stripped else []
        print(' '*ind+f'{tid} "{nm}" pos={d.get("m_LocalPosition")} comps={cs}')
        if ind//2<maxd:
            for c in s.children(tid): s.tree(c,ind+2,out,maxd)
