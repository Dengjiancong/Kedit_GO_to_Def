import json,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
import PrepareRelease as p
class PreparationTests(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup)
  self.root=Path(self.tmp.name);self.source=self.root/'Kedit_GO_to_Def.ahk'
  self.source.write_text('Global CurrentVersion := "old"\n',encoding='utf-8-sig')
  (self.root/'version.json').write_text(json.dumps(dict(major=19,minor=0,codename='Amiya',revision=101)))
  for name,value in [('ROOT',self.root),('SOURCE',self.source),('STATE',self.root/'Release_exe/.prepared-release.json')]:
   patcher=patch.object(p,name,value);patcher.start();self.addCleanup(patcher.stop)
 def test_version_sync_idempotent(self):
  self.assertEqual(p.sync_version(),'v19.00-Amiya.v101');before=self.source.read_bytes();p.sync_version();self.assertEqual(before,self.source.read_bytes());p.sync_version(True)
  v=p.read(self.root/'version.json');v['revision']=102;p.write(self.root/'version.json',v)
  with self.assertRaises(RuntimeError):p.sync_version(True)
 def test_remote_failure_never_increments(self):
  p.sync_version();notes=self.root/'notes.md';notes.write_text('changes');before=(self.root/'version.json').read_bytes()
  with patch('sys.argv',['prepare','--notes',str(notes)]),patch.object(p.mirror,'git') as git,patch.object(p,'remote_check',side_effect=RuntimeError('offline')):
   git.return_value.stdout=''
   with self.assertRaises(RuntimeError):p.main()
  self.assertEqual(before,(self.root/'version.json').read_bytes());self.assertFalse(p.STATE.exists());self.assertFalse((p.STATE.parent/'.prepare.lock').exists())
 def test_resume_verifies_artifacts_without_increment(self):
  folder=self.root/'Release_exe/bundle';folder.mkdir(parents=True);exe=folder/'app.exe';exe.write_bytes(b'fixture')
  p.write(p.STATE,dict(version='v19.00-Amiya.v101',folder='Release_exe/bundle',status='ready',fingerprint='same',sha256={'app.exe':p.mirror.sha(exe)}))
  with patch('sys.argv',['prepare','--resume']),patch.object(p,'fingerprint',return_value='same'),patch.object(p,'remote_check',side_effect=AssertionError('resume must not allocate a version')):
   p.main();exe.write_bytes(b'changed')
   with self.assertRaises(RuntimeError):p.main()
if __name__=='__main__':unittest.main()
