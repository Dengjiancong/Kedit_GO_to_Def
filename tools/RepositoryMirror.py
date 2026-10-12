"""Safe GitHub -> Gitea sync and explicit two-site release publishing.
Requires Python 3 and requests. Credentials stay in Git Credential Manager.
"""
import argparse, base64, hashlib, json, os, pathlib, re, subprocess, tempfile
from urllib.parse import quote, urlsplit
import requests

GH = 'https://github.com/Dengjiancong/Kedit_GO_to_Def'
GT = 'https://gitea.evadd.xyz:88/EVADD/Kedit_GO_to_Def'
APIS = [('gitea', 'https://gitea.evadd.xyz:88/api/v1/repos/EVADD/Kedit_GO_to_Def'),
        ('github', 'https://api.github.com/repos/Dengjiancong/Kedit_GO_to_Def')]
ENV = dict(os.environ, GIT_TERMINAL_PROMPT='0', GCM_INTERACTIVE='Never')

def git(*args, cwd=None, check=True):
    p = subprocess.run(['git', *args], cwd=cwd, env=ENV, capture_output=True, encoding='utf-8')
    if check and p.returncode:
        raise RuntimeError('git failed: '+p.stderr.strip())
    return p

def refs(url):
    return {line.split('\t')[1]:line.split('\t')[0] for line in git('ls-remote', '--heads', '--tags', url).stdout.splitlines()}

def credentials(host):
    p = subprocess.run(['git','credential','fill'], input='protocol=https\nhost='+host+'\n\n',
                       capture_output=True, encoding='utf-8', env=ENV, timeout=30)
    if p.returncode: raise RuntimeError('Configure Git credentials for '+host+' first')
    result = dict(line.split('=',1) for line in p.stdout.splitlines() if '=' in line)
    return result['username'], result['password']

class Api:
    def __init__(self, name, base):
        self.name, self.base = name, base
        token = os.environ.get('GITHUB_TOKEN' if name=='github' else 'GITEA_TOKEN')
        user, password = ('',token) if token else credentials('github.com' if name=='github' else 'gitea.evadd.xyz:88')
        self.session = requests.Session()
        self.session.headers['User-Agent'] = 'Kedit-Maintenance'
        if name=='github': self.session.headers['Authorization']='Bearer '+password
        elif token: self.session.headers['Authorization']='token '+password
        else: self.session.auth=(user,password)
    def call(self, method, path, **kw):
        response=self.session.request(method, self.base+path, timeout=(20,120), **kw)
        if response.status_code==404 and method=='GET': return None
        if not response.ok: raise RuntimeError(self.name+' '+method+' '+path+' HTTP '+str(response.status_code))
        return response.json() if response.content else None

def sync(apply=False, skip=(), git_only=False):
    with tempfile.TemporaryDirectory(prefix='kedit-sync-') as directory:
        git('clone','--bare',GH+'.git',directory)
        source,target=refs(GH+'.git'),refs(GT+'.git')
        actions=[]; errors=[]
        for ref,sha in source.items():
            if ref.endswith('^{}'): continue
            existing=target.get(ref)
            if existing==sha: continue
            if existing and ref.startswith('refs/tags/'):
                if ref[10:] in skip: print('PRESERVED conflicting tag: '+ref);continue
                errors.append('Conflicting tag: '+ref);continue
            if existing:
                git('fetch','--no-tags',GT+'.git',ref,cwd=directory)
                if git('merge-base','--is-ancestor','FETCH_HEAD',sha,cwd=directory,check=False).returncode:
                    errors.append('Diverged branch: '+ref);continue
            actions.append(ref)
        for ref in target.keys()-source.keys():
            if not ref.endswith('^{}'): print('PRESERVED target-only ref: '+ref)
        if errors: raise RuntimeError('\n'.join(errors))
        print('Git refs to sync: '+str(len(actions)))
        for ref in actions: print('  '+ref)
        if apply:
            for ref in actions: git('push',GT+'.git',ref+':'+ref,cwd=directory)
            current=refs(GT+'.git')
            for ref in actions:
                if current.get(ref)!=source[ref]: raise RuntimeError('Remote ref verification failed: '+ref)
    if git_only:
        print('Git refs verified' if apply else 'CHECK ONLY; no remote writes'); return
    # Wiki is separate. API initializes Gitea Wiki if absent; never delete extra pages.
    with tempfile.TemporaryDirectory(prefix='kedit-wiki-') as directory:
        git('clone',GH+'.wiki.git',directory)
        api=Api('gitea',APIS[0][1])
        for file in sorted(pathlib.Path(directory).glob('*.md')):
            title=file.stem; route='/wiki/page/'+quote(title,safe='')
            current=api.call('GET',route)
            body=file.read_bytes()
            if current and base64.b64decode(current.get('content_base64',''))==body: continue
            print('Wiki '+('update: ' if current else 'create: ')+title)
            if apply:
                data={'title':title,'content_base64':base64.b64encode(body).decode(), 'message':'Sync documentation from GitHub Wiki'}
                api.call('PATCH' if current else 'POST',route if current else '/wiki/new',json=data)
                saved=api.call('GET',route)
                if not saved or base64.b64decode(saved.get('content_base64',''))!=body: raise RuntimeError('Wiki verification failed: '+title)
    print('APPLIED and verified' if apply else 'CHECK ONLY; no remote writes')

def sha(path):
    h=hashlib.sha256()
    with open(path,'rb') as f:
        for chunk in iter(lambda:f.read(1024*1024),b''): h.update(chunk)
    return h.hexdigest()

def remote_sha(api, asset):
    # Use API asset endpoint for GitHub drafts; do not leak auth to another host.
    if api.name=='github':
        url=asset['url']; expected='api.github.com'
    else:
        url=asset['browser_download_url']; expected='gitea.evadd.xyz'
        old=urlsplit(url)
        if old.netloc=='192.168.124.224:3110' and old.path.startswith('/EVADD/Kedit_GO_to_Def/releases/download/'):
            url='https://gitea.evadd.xyz:88'+old.path
    parsed=urlsplit(url)
    if parsed.scheme!='https' or parsed.hostname!=expected: raise RuntimeError('Unexpected asset origin')
    with api.session.get(url,headers={'Accept':'application/octet-stream'},stream=True,timeout=(20,120)) as r:
        r.raise_for_status(); h=hashlib.sha256()
        for chunk in r.iter_content(1024*1024): h.update(chunk)
        return h.hexdigest()

def publish(args):
    if not re.fullmatch(r'v\d+\.\d+-(?:Amiya|Meme)\.v\d+',args.version): raise RuntimeError('Use the current supported release version format')
    exe=pathlib.Path(args.exe).resolve(); source=pathlib.Path(args.source).resolve()
    notes=pathlib.Path(args.notes).read_text(encoding='utf-8-sig')
    if not notes.strip(): raise RuntimeError('Release notes are empty')
    text=source.read_text(encoding='utf-8-sig')
    version=re.search(r'(?im)^\s*Global\s+CurrentVersion\s*:=\s*"([^"]+)"',text)
    if not version or version[1]!=args.version: raise RuntimeError('AHK CurrentVersion mismatch')
    with exe.open('rb') as f:
        if f.read(2)!=b'MZ': raise RuntimeError('Not an EXE')
    tag='refs/tags/'+args.version
    left,right=refs(GH+'.git'),refs(GT+'.git')
    if tag not in left or left.get(tag)!=right.get(tag): raise RuntimeError('Publish requires the same pre-existing tag on both sites')
    # Source must be from the tagged commit, not a different working tree.
    tagged=git('show',args.version+':'+source.name).stdout.lstrip('\ufeff').replace('\r\n','\n')
    if tagged!=text.replace('\r\n','\n'): raise RuntimeError('Source differs from tagged source')
    with tempfile.TemporaryDirectory(prefix='kedit-release-') as directory:
        digest=sha(exe); checksum=pathlib.Path(directory)/'Kedit_GO_to_Def.exe.sha256'
        checksum.write_text(digest+'  Kedit_GO_to_Def.exe\n',encoding='ascii')
        files={'Kedit_GO_to_Def.exe':exe,'Kedit_GO_to_Def_'+args.version+'.exe':exe,
               'Kedit_GO_to_Def_'+args.version+'.ahk':source,checksum.name:checksum}
        wanted={n:(p.stat().st_size,sha(p)) for n,p in files.items()}
        plans=[]
        for name,base in APIS:
            api=Api(name,base); release=api.call('GET','/releases/tags/'+quote(args.version,safe=''))
            if release is None:
                # Tag lookup can omit drafts. Find an existing draft before creating anything.
                page=1
                while True:
                    batch=api.call('GET','/releases?per_page=100&limit=100&page='+str(page)) or []
                    matches=[r for r in batch if r['tag_name']==args.version]
                    if matches: release=matches[0];break
                    if len(batch)<100:break
                    page+=1
            assets={a['name']:a for a in release['assets']} if release else {}
            if release and (release.get('prerelease') or release['name']!='Kedit GO to Def '+args.version):
                raise RuntimeError(name+' release title/type differs; review manually')
            if release and (release['body'] or '').replace('\r\n','\n')!=notes.replace('\r\n','\n'): raise RuntimeError(name+' release notes differ; review manually')
            for n,a in assets.items():
                if n in wanted and (a['size']!=wanted[n][0] or remote_sha(api,a)!=wanted[n][1]): raise RuntimeError(name+' conflicting asset: '+n)
            missing=[n for n in files if n not in assets]
            if release and not release['draft'] and missing: raise RuntimeError(name+' published release is incomplete; review manually')
            plans.append((api,release,missing))
            print(name+': '+('create draft' if not release else 'reuse release')+'; missing '+str(missing))
        print('EXE SHA-256: '+digest)
        if not args.apply: print('CHECK ONLY; use --apply to create/upload drafts');return
        ready=[]
        for api,release,missing in plans:
            if not release:
                release=api.call('POST','/releases',json={'tag_name':args.version,'name':'Kedit GO to Def '+args.version,'body':notes,'draft':True,'prerelease':False})
            for n in missing:
                with files[n].open('rb') as f:
                    if api.name=='gitea':
                        api.call('POST','/releases/'+str(release['id'])+'/assets?name='+quote(n),files={'attachment':(n,f,'application/octet-stream')})
                    else:
                        url=release['upload_url'].split('{')[0]+'?name='+quote(n)
                        if urlsplit(url).hostname!='uploads.github.com': raise RuntimeError('Unexpected upload origin')
                        response=api.session.post(url,data=f,headers={'Content-Type':'application/octet-stream'},timeout=(20,600))
                        if not response.ok: raise RuntimeError('GitHub upload HTTP '+str(response.status_code))
            result=api.call('GET','/releases/'+str(release['id']))
            byname={a['name']:a for a in result['assets']}
            for n,(size,digest) in wanted.items():
                a=byname.get(n)
                if not a or a['size']!=size or remote_sha(api,a)!=digest: raise RuntimeError('Uploaded asset verification failed: '+n)
            ready.append((api,result))
        # Both drafts are complete before either becomes visible. Retrying reuses verified assets.
        if args.publish:
            for api,release in ready:
                if release['draft']: api.call('PATCH','/releases/'+str(release['id']),json={'draft':False})
                result=api.call('GET','/releases/'+str(release['id']))
                if result['draft']: raise RuntimeError('Release still draft: '+api.name)
                print('Published: '+result['html_url'])
        else: print('Verified drafts ready on both sites; rerun with --apply --publish to publish')

def main():
    p=argparse.ArgumentParser(); sub=p.add_subparsers(dest='mode',required=True)
    s=sub.add_parser('sync');s.add_argument('--apply',action='store_true');s.add_argument('--skip-tag',action='append',default=[]);s.add_argument('--git-only',action='store_true')
    r=sub.add_parser('publish')
    for name in ['version','exe','source','notes']:r.add_argument('--'+name,required=True)
    r.add_argument('--apply',action='store_true');r.add_argument('--publish',action='store_true')
    a=p.parse_args()
    if a.mode=='sync':sync(a.apply,a.skip_tag,a.git_only)
    else:
        if a.publish and not a.apply:p.error('--publish requires --apply')
        publish(a)
if __name__=='__main__':
    try:main()
    except Exception as ex:
        import sys
        print('STOP: '+str(ex),file=sys.stderr);sys.exit(1)
