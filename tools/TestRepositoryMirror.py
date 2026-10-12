import pathlib, tempfile, unittest
from unittest.mock import patch
import RepositoryMirror as m

class SyncTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.root=pathlib.Path(self.temp.name)
        self.source=str(self.root/'source'); self.target=str(self.root/'target')
        for base in [self.source,self.target]:m.git('init','--bare',base+'.git')
        self.work=self.root/'work';m.git('clone',self.source+'.git',str(self.work))
        m.git('config','user.email','fixture@example.invalid',cwd=self.work);m.git('config','user.name','Fixture',cwd=self.work)
        m.git('checkout','-b','main',cwd=self.work)
        (self.work/'a').write_text('one');m.git('add','a',cwd=self.work);m.git('commit','-m','one',cwd=self.work)
        m.git('push','origin','main',cwd=self.work);m.git('symbolic-ref','HEAD','refs/heads/main',cwd=self.source+'.git')
        self.a=patch.object(m,'GH',self.source);self.b=patch.object(m,'GT',self.target);self.a.start();self.b.start()
    def tearDown(self):self.a.stop();self.b.stop();self.temp.cleanup()
    def test_check_does_not_write(self):
        m.sync(git_only=True);self.assertEqual(m.refs(self.target+'.git'),{})
    def test_apply_and_rerun(self):
        m.sync(True,git_only=True);m.sync(True,git_only=True)
        self.assertEqual(m.refs(self.source+'.git'),m.refs(self.target+'.git'))
    def test_conflicting_tag_stops_before_write(self):
        m.git('tag','v1',cwd=self.work);m.git('push','origin','v1',cwd=self.work);m.sync(True,git_only=True)
        (self.work/'a').write_text('two');m.git('commit','-am','two',cwd=self.work)
        m.git('tag','-f','v1',cwd=self.work);m.git('push',self.target+'.git','refs/tags/v1:refs/tags/v1','--force',cwd=self.work)
        before=m.refs(self.target+'.git')
        with self.assertRaises(RuntimeError):m.sync(True,git_only=True)
        self.assertEqual(before,m.refs(self.target+'.git'))
        m.sync(True,['v1'],True);self.assertEqual(before,m.refs(self.target+'.git'))
    def test_target_only_tag_preserved(self):
        m.sync(True,git_only=True);m.git('tag','local-only',cwd=self.work);m.git('push',self.target+'.git','local-only',cwd=self.work)
        m.sync(True,git_only=True);self.assertIn('refs/tags/local-only',m.refs(self.target+'.git'))


class ReleaseTests(unittest.TestCase):
    def test_dry_run_never_creates_release(self):
        self.check(False)
    def test_existing_mismatched_notes_stop(self):
        self.check(True)
    def check(self, mismatch):
        from types import SimpleNamespace
        with tempfile.TemporaryDirectory() as directory:
            root=pathlib.Path(directory);version='v19.00-Amiya.v009'
            source=root/'test.ahk';text='Global CurrentVersion := "'+version+'"\n';source.write_text(text)
            exe=root/'test.exe';exe.write_bytes(b'MZ'+b'0'*128)
            notes=root/'notes.md';notes.write_text('Release notes')
            calls=[]
            class FakeApi:
                def __init__(self,name,base):self.name=name
                def call(self,method,path,**kw):
                    calls.append(method)
                    if '/tags/' in path:
                        if not mismatch:return None
                        return {'assets':[],'tag_name':version,'body':'different notes','draft':True,'prerelease':False,'name':'Kedit GO to Def '+version}
                    return []
            args=SimpleNamespace(version=version,exe=str(exe),source=str(source),notes=str(notes),apply=False,publish=False)
            with patch.object(m,'refs',return_value={'refs/tags/'+version:'abc'}),patch.object(m,'git',return_value=SimpleNamespace(stdout=text)),patch.object(m,'Api',FakeApi):
                if mismatch:
                    with self.assertRaises(RuntimeError):m.publish(args)
                else:m.publish(args)
            self.assertTrue(calls);self.assertEqual(set(calls),{'GET'})

if __name__=='__main__':unittest.main()
