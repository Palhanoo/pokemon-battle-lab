"""Stage the documented Emerald move recordings/cries. Run with Unity closed."""
from pathlib import Path
import hashlib,json,shutil,struct,wave,array,sys
ROOT=Path(__file__).resolve().parents[1]
index=json.loads((ROOT/'Tools/audio-index.json').read_text())
source=ROOT.parent/'HoennVR/Assets/Generated/Audio';out=ROOT/'Assets/NativeAudio';out.mkdir(exist_ok=True)
for name in sorted({m['sound'] for m in index['moves']}):
    if name!='SE_M_GUST_SHORT':shutil.copy2(source/'sfx'/(name+'.wav'),out/(name+'.wav'));continue
    b=(source/'sfx/SE_M_GUST.wav').read_bytes();offset=b.index(b'data');samples=array.array('f');samples.frombytes(b[offset+8:offset+8+int(48000*.65)*8])
    if sys.byteorder!='little':samples.byteswap()
    count=len(samples)//2;pcm=array.array('h')
    for i,value in enumerate(samples):
        frame=i//2;fade=min(1,frame/479,(count-1-frame)/4799)
        pcm.append(int(max(-1,min(1,value*fade))*32767))
    if sys.byteorder!='little':pcm.byteswap()
    with wave.open(str(out/(name+'.wav')),'wb') as w:
        w.setnchannels(2);w.setsampwidth(2);w.setframerate(48000);w.writeframes(pcm.tobytes())
for species in index['cries']:shutil.copy2(source/'cries'/(species+'.wav'),out/('CRY_'+species+'.wav'))
for name in ['SE_SUPER_EFFECTIVE','SE_NOT_EFFECTIVE']:shutil.copy2(source/'sfx'/(name+'.wav'),out/(name+'.wav'))
(out/'index.json').write_text(json.dumps(index,indent=2))
manifest=[{'path':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in out.rglob('*') if p.is_file() and p.suffix!='.meta']
(ROOT/'Docs/audio-manifest.json').write_text(json.dumps(manifest,indent=2))
print(f'Staged audio for {len(index["moves"])} moves and {len(index["cries"])} cries.')
