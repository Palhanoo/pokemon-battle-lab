"""Regression fixtures for native overlapping colour masks and disabled layers."""
from pathlib import Path
import io,sys,unittest
from unittest.mock import patch
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[1];sys.path[:0]=[str(ROOT/'Tools/vendor/arceusx')]
import convert
class Material:
    def string(self,i):return 'body' if i==0 else ''
    def tables(self,i):return []
class Layers(unittest.TestCase):
    def bake(self,alpha,overlap):
        glb=convert.GLB();baker=convert.MaterialBaker(None,glb,{},False)
        base=np.full((2,2,4),255,np.uint8);mask=np.zeros((2,2,4),np.uint8);mask[...,0]=255
        if overlap:mask[...,1]=255
        baker.tex=lambda name:base if name=='base' else mask
        settings=({'BaseColorLayer1':(1,0,0,alpha),'BaseColorLayer2':(0,1,0,1)}, {}, {'BaseColorMultiply':'True'})
        with patch.object(convert,'params',return_value=settings),patch.object(convert,'tex_by_binding',return_value={'BaseColorMap':'base','LayerMaskMap':'mask'}):baker.build(Material())
        view=glb.j['bufferViews'][glb.j['images'][0]['bufferView']];pixels=np.asarray(Image.open(io.BytesIO(glb.bin[view['byteOffset']:view['byteOffset']+view['byteLength']])))
        return pixels[0,0,:3].tolist()
    def test_later_green_mask_overrides_red_instead_of_multiplying_to_black(self):self.assertEqual(self.bake(1,True),[0,255,0])
    def test_disabled_red_layer_keeps_albedo(self):self.assertEqual(self.bake(0,False),[255,255,255])
    def test_single_red_layer_still_tints_albedo(self):self.assertEqual(self.bake(1,False),[255,0,0])
if __name__=='__main__':unittest.main()
