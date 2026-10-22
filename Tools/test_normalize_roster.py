import json,struct,tempfile,unittest
from pathlib import Path
from normalize_roster import normalize
class NormalizeTests(unittest.TestCase):
    def fixture(self,cubic=False):
        times=[0.,.5,.5,1.];factor=3 if cubic else 1;values=[float(i) for i in range(4*factor)]
        binary=struct.pack('<4f',*times)+struct.pack('<%df'%len(values),*values)
        d={'buffers':[{'byteLength':len(binary)}],'bufferViews':[{'buffer':0,'byteOffset':0,'byteLength':16},{'buffer':0,'byteOffset':16,'byteLength':len(values)*4}], 'accessors':[{'bufferView':0,'componentType':5126,'type':'SCALAR','count':4},{'bufferView':1,'componentType':5126,'type':'SCALAR','count':len(values)}], 'animations':[{'samplers':[{'input':0,'output':1,'interpolation':'CUBICSPLINE' if cubic else 'LINEAR'}]}]}
        j=json.dumps(d).encode();j+=b' '*(-len(j)%4)
        return struct.pack('<III',0x46546c67,2,28+len(j)+len(binary))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(binary),0x004e4942)+binary
    def check(self,cubic):
        with tempfile.TemporaryDirectory() as folder:
            p=Path(folder)/'test.glb';p.write_bytes(self.fixture(cubic));self.assertEqual(normalize(p),1);b=p.read_bytes();n=struct.unpack_from('<I',b,12)[0];d=json.loads(b[20:20+n]);binary=b[28+n:]
            sampler=d['animations'][0]['samplers'][0]
            def read(key):
                a=d['accessors'][sampler[key]];v=d['bufferViews'][a['bufferView']];return struct.unpack_from('<%df'%a['count'],binary,v['byteOffset'])
            self.assertEqual(read('input'),(0.,.5,1.))
            expected=(0.,1.,2.,6.,7.,8.,9.,10.,11.) if cubic else (0.,2.,3.)
            self.assertEqual(read('output'),expected);self.assertEqual(normalize(p),0);self.assertEqual(p.read_bytes(),b)
    def test_linear_last_pose_and_idempotence(self):self.check(False)
    def test_cubic_keeps_tangents_with_each_pose(self):self.check(True)
if __name__=='__main__':unittest.main()
