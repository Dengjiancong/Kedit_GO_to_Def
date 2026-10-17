"""Prepare a local immutable release bundle. Never push, tag or publish remotely."""
import argparse, hashlib, json, os, re, shutil, subprocess
from pathlib import Path
from urllib.parse import quote
import RepositoryMirror as mirror
ROOT=Path(__file__).resolve().parent.parent
SOURCE=ROOT/'Kedit_GO_to_Def.ahk'
STATE=ROOT/'Release_exe/.prepared-release.json'
def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def write(path,value):
    path.parent.mkdir(parents=True,exist_ok=True)
    temp=path.with_suffix(path.suffix+'.tmp');temp.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');temp.replace(path)
def version(v): return 'v%d.%02d-%s.v%d'%(v['major'],v['minor'],v['codename'],v['revision'])
def sync_version(check=False):
    v=read(ROOT/'version.json')
    if any(type(v[k]) is not int or not 0<=v[k]<=65535 for k in ('major','minor','revision')) or not re.fullmatch('[A-Za-z0-9]+',v['codename']): raise RuntimeError('Invalid version.json')
    tag=version(v);text=SOURCE.read_text(encoding='utf-8-sig')
    text,n=re.subn(r'Global CurrentVersion := "[^"]+"','Global CurrentVersion := "'+tag+'"',text,count=1)
    if n!=1: raise RuntimeError('Missing CurrentVersion')
    text=re.sub(r';@Ahk2Exe-Set(?:Version|ProductVersion) .*\n','',text)
    directives=';@Ahk2Exe-SetVersion %d.%d.0.%d\n;@Ahk2Exe-SetProductVersion %s\n'%(v['major'],v['minor'],v['revision'],tag)
    text=directives+text
    if check:
        if SOURCE.read_text(encoding='utf-8-sig')!=text: raise RuntimeError('Version metadata out of sync; run python tools/PrepareRelease.py --sync-version')
    else: SOURCE.write_text(text,encoding='utf-8-sig')
    return tag
def fingerprint():
    names=mirror.git('ls-files','-z',cwd=ROOT).stdout.split('\0');h=hashlib.sha256()
    for name in sorted(set(names+['version.json','Kedit_GO_to_Def.ahk'])):
        p=ROOT/name
        if name and p.is_file(): h.update(name.encode());h.update(p.read_bytes())
    return h.hexdigest()
def require_committed_source():
    # Only allow the exact metadata edits produced by this tool.
    rows=mirror.git('status','--porcelain','-z',cwd=ROOT).stdout.split('\0')
    for row in filter(None,rows):
        name=row[3:]
        if name not in ('version.json','Kedit_GO_to_Def.ahk'):
            raise RuntimeError('Commit source changes before resuming: '+name)
        previous=mirror.git('show','HEAD:'+name,cwd=ROOT).stdout
        if name=='version.json':
            old=json.loads(previous.lstrip('\ufeff'));new=read(ROOT/name)
            old.pop('revision',None);new.pop('revision',None)
            valid=old==new
        else:
            def body(text):
                text=text.lstrip('\ufeff').replace('\r\n','\n')
                text=re.sub(r'^;@Ahk2Exe-Set(?:Version|ProductVersion) .*\n','',text,flags=re.M)
                return re.sub(r'Global CurrentVersion := "[^"]+"','Global CurrentVersion := "VERSION"',text,count=1)
            valid=body(previous)==body(SOURCE.read_text(encoding='utf-8-sig'))
        if not valid:raise RuntimeError('Commit source changes before resuming: '+name)

def remote_check(tag):
    # Read authenticated release lists (including drafts) and refs on BOTH sites.
    if not os.environ.get('GITEA_TOKEN'): os.environ['GITEA_TOKEN']=mirror.credentials('gitea.evadd.xyz:88')[1]
    for name,base in mirror.APIS:
        api=mirror.Api(name,base)
        if api.call('GET','') is None: raise RuntimeError(name+' repository unavailable')
        refs=mirror.refs((mirror.GH if name=='github' else mirror.GT)+'.git')
        if 'refs/tags/'+tag in refs: raise RuntimeError(name+' tag already exists: '+tag)
        for ref in refs:
            match=re.fullmatch('refs/tags/'+re.escape(tag.rsplit('.v',1)[0])+r'\.v(\d+)',ref)
            if match and int(match[1])>=int(tag.rsplit('.v',1)[1]): raise RuntimeError(name+' already has a newer tag; reconcile version.json first')
        page=1
        while True:
            rows=api.call('GET','/releases?per_page=100&limit=100&page='+str(page))
            if rows is None: raise RuntimeError(name+' release listing unavailable')
            for release in rows:
                if release['tag_name']==tag: raise RuntimeError(name+' release already exists: '+tag)
                match=re.fullmatch(re.escape(tag.rsplit('.v',1)[0])+r'\.v(\d+)',release['tag_name'])
                if match and int(match[1])>=int(tag.rsplit('.v',1)[1]): raise RuntimeError(name+' already has a newer revision; reconcile version.json first')
            if len(rows)<100:break
            page+=1
    print('Both sites verified: version is unused')
def run(*args): subprocess.run(list(map(str,args)),cwd=ROOT,check=True)
def main():
    ap=argparse.ArgumentParser();ap.add_argument('--resume',action='store_true');ap.add_argument('--sync-version',action='store_true');ap.add_argument('--check-version',action='store_true');ap.add_argument('--notes',type=Path);args=ap.parse_args()
    if args.sync_version or args.check_version: print(sync_version(args.check_version));return
    STATE.parent.mkdir(exist_ok=True);lock=STATE.parent/'.prepare.lock'
    with lock.open('x') as lock_file:
      try:
        pending=read(STATE) if STATE.exists() else None
        resume=args.resume or (pending is not None and pending['status']=='building')
        if resume:
            state=pending
            if state is None:raise RuntimeError('No preparation exists to resume.')
            if sync_version(True)!=state['version']:raise RuntimeError('Version differs from pending preparation; restore its version before resuming.')
            changed=fingerprint()!=state['fingerprint']
            if changed and state['status']=='ready':raise RuntimeError('Source changed since completed preparation; prepare a new version.')
            if changed:require_committed_source()
            print('Resuming '+state['version']+' (no version increment; build and tests will run again if incomplete).')
            folder=ROOT/state['folder'];tag=state['version']
            if state['status']=='ready':
                for name,digest in state['sha256'].items():
                    if mirror.sha(folder/name)!=digest:raise RuntimeError('Prepared artifact changed: '+name)
                print('Verified existing bundle (no increment): '+str(folder));return
            remote_check(tag)
            if changed:
                state['fingerprint']=fingerprint()
                state['source_commit']=mirror.git('rev-parse','HEAD',cwd=ROOT).stdout.strip()
                write(STATE,state)
        else:
            if mirror.git('status','--porcelain',cwd=ROOT).stdout.strip():raise RuntimeError('Commit working-tree changes before preparing a release.')
            if not args.notes or not args.notes.is_file():raise RuntimeError('--notes must specify the release notes file')
            sync_version(True)
            v=read(ROOT/'version.json')
            if v['revision']>=65535:raise RuntimeError('Revision limit reached; choose a new major/minor version')
            v['revision']+=1;tag=version(v);remote_check(tag)
            folder=ROOT/'Release_exe'/('Kedit GO to Def '+tag)
            folder.mkdir(exist_ok=False)
            notes=args.notes.read_text(encoding='utf-8-sig');(folder/'release-notes.md').write_text('# Kedit GO to Def '+tag+'\n\n'+notes,encoding='utf-8')
            write(ROOT/'version.json',v);sync_version()
            state=dict(version=tag,folder=str(folder.relative_to(ROOT)),status='building',fingerprint=fingerprint(),source_commit=mirror.git('rev-parse','HEAD',cwd=ROOT).stdout.strip())
            write(STATE,state)
        run('powershell','-NoProfile','-ExecutionPolicy','Bypass','-File','tools/Build-Kedit.ps1','-OutputDirectory',folder)
        for test in ['Test-Updates.ps1','Test-ConsoleLifetime.ps1','Test-ColumnFill.ps1','Test-UsageStatistics.ps1']:
            run('powershell','-NoProfile','-ExecutionPolicy','Bypass','-File','tools/'+test)
        run('python','tools/Test-FlowInsertion.py')
        run('python','tools/Test-UpdateStartup.py')
        exe=folder/('Kedit_GO_to_Def_'+tag+'.exe');shutil.copy2(exe,folder/'Kedit_GO_to_Def.exe')
        shutil.copy2(SOURCE,folder/('Kedit_GO_to_Def_'+tag+'.ahk'))
        digest=mirror.sha(exe);(folder/'Kedit_GO_to_Def.exe.sha256').write_text(digest+'  Kedit_GO_to_Def.exe\n',encoding='ascii')
        state['sha256']={p.name:mirror.sha(p) for p in folder.iterdir() if p.is_file()};state['status']='ready'
        write(folder/'release-manifest.json',state);write(STATE,state)
        print('READY (not published): '+str(folder)+'\nCommit version.json and generated source before tagging '+tag)
      finally: lock_file.close();lock.unlink()
if __name__=='__main__':
    try:main()
    except Exception as ex:raise SystemExit('STOP: '+str(ex))
