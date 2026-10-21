import bpy,json
from pathlib import Path
p=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(p.parent/'CustomEnemies/SculptTextureRevision4/Blender/Hunter_Sculpt4.blend'))
rig=bpy.data.objects['Hunter_Rig']
print('RIG',tuple(rig.scale),flush=True)
for n in ('Hips','Spine','Chest','Head','LeftUpperLeg','LeftLowerLeg','LeftFoot','LeftUpperArm','LeftLowerArm','LeftHand'):
 b=rig.data.bones[n]
 print(n,tuple(b.head_local),tuple(b.tail_local),b.length,flush=True)
