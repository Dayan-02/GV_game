"""Render the repaired source with a bounded Cycles preview, outside the game."""
import bpy,json,hashlib
from pathlib import Path
root=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(root/'Blender'/'Four_Enemies_Review.blend'))
scene=bpy.context.scene
scene.render.engine='CYCLES'
scene.cycles.samples=8
scene.cycles.use_denoising=True
scene.cycles.use_light_tree=False
scene.render.resolution_x=1440
scene.render.resolution_y=680
scene.render.resolution_percentage=100
scene.render.use_simplify=True
scene.cycles.texture_limit='512'
scene.render.filepath=str(root/'Previews'/'Repaired_Lineup.png')
report=[]
for kind in ('Hunter','Runner','Ambusher','Elite'):
    rig=bpy.data.objects.get(kind+'_Rig');mesh=bpy.data.objects.get(kind+'_CustomMesh')
    mesh.data.calc_loop_triangles()
    report.append(dict(name=kind,vertices=len(mesh.data.vertices),triangles=len(mesh.data.loop_triangles),bones=len(rig.data.bones),unweighted_vertices=sum(not v.groups for v in mesh.data.vertices)))
(root/'Evidence'/'mesh_report.json').write_text(json.dumps(report,indent=2))
bpy.ops.render.render(write_still=True)
hashes={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for sub in ('Exports','Textures') for p in (root/sub).glob('*') if p.is_file()}
(root/'Evidence'/'sha256.json').write_text(json.dumps(hashes,indent=2))
print('REPAIRED_LINEUP_COMPLETE',json.dumps(report),flush=True)
